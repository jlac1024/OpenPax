namespace OpenPax.Sim.Players;

/// <summary>
/// Some number of one tech fitted to a design or ship.
/// </summary>
/// <param name="mode">Weapon mode, the firing arc: 0 bow, 1 port, 2 starboard, 3 point defense; -1 for anything not a weapon.</param>
public sealed class Component(TechInfo tech, int mode)
{
    public const int NotWeapon = -1;

    public TechInfo Tech { get; } = tech;

    public int Mode { get; } = mode;

    public int Count { get; internal set; } = 1;

    /// <summary>Damage taken, against count times the tech's hit points.</summary>
    public int Damage { get; internal set; }

    /// <summary>
    /// Rounds left in the volley being fired (+0x1C): the full count when a
    /// battle starts, then the working count at each reload.
    /// </summary>
    public int VolleyLeft { get; internal set; } = 1;

    /// <summary>Fire phases until it can fire again (+0x14).</summary>
    public int ReloadLeft { get; internal set; }

    /// <summary>Ready to fire: loaded, a weapon, and working.</summary>
    public bool Ready => ReloadLeft == 0 && Mode != NotWeapon && Condition != 0;

    /// <summary>
    /// Fires up to n rounds: what is left of the volley when it
    /// is n or less, and the reload starts (half for point defense), else n.
    /// None while reloading.
    /// </summary>
    internal int TakeVolley(int n)
    {
        if (ReloadLeft != 0)
        {
            return 0;
        }

        var left = VolleyLeft;
        if (left <= n)
        {
            ReloadLeft = Mode == 3 ? Tech.Reload / 2 : Tech.Reload;
            VolleyLeft = Operational;
            return left;
        }

        VolleyLeft = left - n;
        return n;
    }

    /// <summary>100 when undamaged, integer arithmetic.</summary>
    public int Condition => 100 - Damage * 100 / (Count * Tech.HitPoints);

    /// <summary>How many still work.</summary>
    public int Operational => Count * Condition / 100;

    public Component Copy()
    {
        return new Component(Tech, Mode) { Count = Count, Damage = Damage, VolleyLeft = Count };
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Tech.Id).Add(Mode).Add(Count).Add(Damage).Add(VolleyLeft);
    }
}
