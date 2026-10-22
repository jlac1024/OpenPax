namespace OpenPax.Sim.Players;

/// <summary>
/// The seven treaty states one player holds toward another (player +0x10 +
/// other * 0x20): 0 War, 1 Neutral, 2 Trade Routes, 3 Repair and Resupply,
/// 4 Allied, 5 Joint Research, 6 Share Information (docs/sim/diplomacy.md).
/// Each slot is <see cref="Off"/>, <see cref="InForce"/>, or pending: offered
/// by this player, or offered to it.
/// </summary>
public sealed class Treaty
{
    public const int States = 7;

    /// <summary>"No state set": the level of a pair with no contact.</summary>
    public const int None = 200;

    public const int War = 0;
    public const int Neutral = 1;
    public const int TradeRoutes = 2;
    public const int Resupply = 3;
    public const int Allied = 4;
    public const int JointResearch = 5;
    public const int ShareInformation = 6;

    public const int Off = 0;
    public const int InForce = 1;
    public const int OfferedByMe = 2;
    public const int OfferedToMe = 3;

    private readonly int[] _slots = new int[States];

    /// <summary>The seven slots as an original save holds them, with no side effects.</summary>
    internal void Restore(int state, int value) => _slots[state] = value;

    /// <summary>A slot's raw value: off, in force or pending.</summary>
    public int Slot(int state)
    {
        return _slots[state];
    }

    /// <summary>
    /// Whether a state is in force; for <see cref="None"/>, whether every
    /// slot is off (a pending offer is contact).
    /// </summary>
    public bool Has(int state)
    {
        return state == None ? _slots.All(s => s == Off) : _slots[state] == InForce;
    }

    /// <summary>The highest state in force, or <see cref="None"/>.</summary>
    public int Level()
    {
        for (var state = States - 1; state >= 0; state--)
        {
            if (_slots[state] == InForce)
            {
                return state;
            }
        }

        return None;
    }

    /// <summary>
    /// Writes one slot. Putting Neutral in force ends a war (or ends an
    /// alliance with what came with it); putting Allied in force ends
    /// Neutral; writing War clears everything else, whatever the value.
    /// </summary>
    internal void Write(int state, int value)
    {
        switch (state)
        {
            case None:
                Array.Clear(_slots);
                break;
            case War:
                Array.Clear(_slots);
                _slots[War] = value;
                break;
            case Neutral:
                if (value == InForce && _slots[War] == InForce)
                {
                    _slots[War] = Off;
                    _slots[Neutral] = InForce;
                    return;
                }

                if (value == InForce && _slots[Allied] == InForce)
                {
                    _slots[Allied] = _slots[JointResearch] = _slots[ShareInformation] = Off;
                }

                _slots[Neutral] = value;
                break;
            case Allied:
                if (value == InForce)
                {
                    _slots[Neutral] = Off;
                }

                _slots[Allied] = value;
                break;
            case TradeRoutes or Resupply or JointResearch or ShareInformation:
                _slots[state] = value;
                break;
        }
    }

    public void AddTo(StateHash hash)
    {
        foreach (var s in _slots)
        {
            hash.Add(s);
        }
    }
}
