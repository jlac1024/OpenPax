using OpenPax.Sim.Combat;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Players' commands carried out (docs/netcode/modern.md). Every action a
/// screen takes comes through here, so single player, a game server and a
/// replay all change the game the same way.
/// </summary>
public sealed partial class Game
{
    /// <summary>Carries out a command between ticks. One naming a player, object or slot that is gone or not the player's does nothing.</summary>
    public StepEvents Apply(Command command)
    {
        // Kept by how many steps had run: battle steps leave the tick as it is.
        Recording?.Commands.Add((Recording.Hashes.Count, command));
        if (command.Player < 0 || command.Player >= Players.Count)
        {
            return StepEvents.None;
        }

        var player = Players[command.Player];
        switch (command)
        {
            case FleetOrderCommand c when OwnFleet(player, c.Fleet) is { } fleet:
                FleetOrder(fleet, c.Kind, c.Kind == FleetOrderKind.ToggleIntercept ? null : c.Target < 0 ? Order.ToPoint(c.X, c.Y) : SpaceObject(c.Target) is { } target ? Order.To(target) : null, c.Replace);
                break;
            case FleetRouteCommand c when OwnFleet(player, c.Fleet) is { } fleet && c.Star >= 0 && c.Star < Universe.Stars.Count:
                FleetRoute(fleet, c.Star, c.Kind, c.Replace, c.SplitOff);
                break;
            case SplitFleetCommand c when OwnFleet(player, c.Fleet) is { } fleet:
                SplitFleet(fleet, c.Ships);
                break;
            case BattleGroupCommand c:
                foreach (var id in c.Ships)
                {
                    if (FindShip(id) is { } ship && ship.Owner == player)
                    {
                        ship.CombatGroup = c.Group;
                    }
                }

                break;
            case BattleOrderCommand c when MayOrder(player, c.Order):
                ApplyBattleOrder(c.Order);
                break;
            case StopWorkCommand c when OwnPlanet(player, c.Planet) is { } planet:
                StopWork(planet);
                break;
            case ColonyPlanCommand c when OwnPlanet(player, c.Planet) is { } planet:
                SetColonyPlan(planet, c.Plan);
                break;
            case FundingCommand c when OwnPlanet(player, c.Planet) is { } planet:
                ChangeFunding(player, planet, c.Value);
                break;
            case OrderShipsCommand c when OwnPlanet(player, c.Planet) is { } planet:
                OrderShips(player, planet, c.DesignSlot, c.Count);
                break;
            case OrbitalCommand c when OwnPlanet(player, c.Planet) is { } planet:
                OrderOrbital(planet, c.Tech);
                break;
            case ScrapStructureCommand c when OwnPlanet(player, c.Planet) is { } planet:
                Scrap(planet, c.Kind);
                break;
            case AbandonCommand c when OwnPlanet(player, c.Planet) is { } planet:
                return Abandon(planet);
            case ScrapShipCommand c:
                ScrapShip(player, c.Ship);
                break;
            case RefitCommand c:
                MarkRefit(player, c.Ship, c.DesignSlot);
                break;
            case SaveDesignCommand c:
                SaveDesign(player, c.Design.Build(Tables, player));
                break;
            case DeleteDesignCommand c:
                DeleteDesign(player, c.Slot);
                break;
            case AutoDesignCommand c when c.Hull >= 0 && c.Hull < Tables.Hulls.Count:
                player.Draft = AutoDesign(player, c.Hull, c.Name);
                player.DraftNumber++;
                break;
            case ResearchCommand c:
                ResearchTech(player, c.Tech, c.Field);
                break;
            case StopResearchCommand c:
                StopResearch(player, c.Field);
                break;
            case AllocationCommand c:
                SetAllocation(player, c.Field, c.Value);
                break;
            case SpendingCommand c:
                SetSpending(player, c.Field, c.Value);
                break;
            case CopyPlanCommand c:
                CopyPlan(player, c.From, c.To, c.Name);
                break;
            case RenamePlanCommand c:
                RenamePlan(player, c.Slot, c.Name);
                break;
            case DeletePlanCommand c:
                DeletePlan(player, c.Slot);
                break;
            case TreatyClickCommand c when OtherPlayer(player, c.Other) is { } other:
                return TreatyClick(player, other, c.State);
            case TreatyReplyCommand c when OtherPlayer(player, c.Other) is { } other:
                return ReplyTreaty(player, other, c.State, c.Value);
            case TradeOfferCommand c when OtherPlayer(player, c.Target) is { } receiver:
                return OfferPackage(Package(player, receiver, c.Offered, c.Asked));
            case TradeAnswerCommand c when OtherPlayer(player, c.Proposer) is { } proposer:
                return AnswerPackage(Package(proposer, player, c.Offered, c.Asked), c.Accept);
            case AddMissionCommand c when OtherPlayer(player, c.Target) is { } spiedOn:
                AddMission(player, spiedOn, c.Kind, c.Level);
                break;
            case CancelMissionCommand c:
                CancelMission(player, c.Mission);
                break;
            case AppointGovernorCommand c:
                AppointGovernor(player, c.Advisor, c.Role);
                break;
            case FireGovernorCommand c:
                FireGovernor(player, c.Advisor);
                break;
            case ObserveCommand:
                Observe(player);
                break;
            case CheatCommand c:
                return ApplyCheat(player, c.Cheat);
        }

        return StepEvents.None;
    }

    /// <summary>A battle order may name only the player's own ships and orbitals in the battles under way.</summary>
    private bool MayOrder(Player player, BattleOrder order)
    {
        var side = player.Index;
        if (side >= Combat.Battle.Players || Battles.FirstOrDefault(b => b.Id == order.BattleId) is not { } battle)
        {
            return false;
        }

        var own = Battles.SelectMany(b => b.Ships[side]).Select(s => s.Id).ToHashSet();
        var selfTargeted = order.Kind == BattleOrderKind.Horizontal && order.ShipIds.Count == 0;
        return order.ShipIds.All(own.Contains)
            && (order.OrbitalId == -1 || battle.Orbitals[side].Any(o => o.Id == order.OrbitalId))
            && (!selfTargeted || own.Contains(order.TargetId));
    }

    /// <summary>The recording for a replay, once started; not saved or sent with the game.</summary>
    [field: Saving.NotSaved]
    public GameRecording? Recording { get; private set; }

    /// <summary>Records commands and every tick's hash from now on (for a game from its first tick).</summary>
    public GameRecording StartRecording() => Recording ??= new GameRecording();

    private static Fleet? OwnFleet(Player player, int id) => player.Fleets.FirstOrDefault(f => f.ObjectId == id);

    private Planet? OwnPlanet(Player player, int id) =>
        Universe.Stars.SelectMany(s => s.Planets).FirstOrDefault(p => p.ObjectId == id && p.Colony?.Owner == player);

    private Player? OtherPlayer(Player player, int index) => index >= 0 && index < Players.Count && index != player.Index ? Players[index] : null;

    private static TradePackage Package(Player proposer, Player target, TradeTerms offered, TradeTerms asked)
    {
        var package = new TradePackage(proposer, target);
        offered.CopyTo(package.Offered);
        asked.CopyTo(package.Asked);
        return package;
    }

    /// <summary>
    /// What a fleet order can name, by object id: a star (its center), a
    /// planet, a wormhole or a fleet.
    /// </summary>
    public ISpaceObject? SpaceObject(int id)
    {
        foreach (var star in Universe.Stars)
        {
            if (star.ObjectId == id)
            {
                return star.Center;
            }

            if (star.Planets.FirstOrDefault(p => p.ObjectId == id) is { } planet)
            {
                return planet;
            }

            if (star.Wormholes.FirstOrDefault(w => w.ObjectId == id) is { } hole)
            {
                return hole;
            }

            if (star.Fleets.FirstOrDefault(f => f.ObjectId == id) is { } fleet)
            {
                return fleet;
            }
        }

        return null;
    }

    /// <summary>The object id a fleet order names its target by (<see cref="SpaceObject"/>); -1 for none.</summary>
    public int ObjectIdOf(ISpaceObject? target) => target switch
    {
        StarCenter c => Universe.Stars[c.Star].ObjectId,
        Planet p => p.ObjectId,
        Wormhole w => w.ObjectId,
        Fleet f => f.ObjectId,
        _ => -1,
    };
}
