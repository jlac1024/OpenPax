namespace OpenPax.Sim;

/// <summary>Events for mods (docs/modding.md): raised where the rules make them, for every player.</summary>
public sealed partial class Game
{
    // Set while a colony changes hands, so its end and new start read as one event.
    [Saving.NotSaved]
    private bool _handingOver;

    /// <summary>Everything a <see cref="ModEvent"/> tells of, as it happens. Not saved: the mods subscribe again.</summary>
    [ModApi]
    [field: Saving.NotSaved]
    public event Action<Game, ModEvent>? Happened;

    private void Happen(ModEvent e)
    {
        if (_handingOver && e.Kind is ModEventKind.ColonyFounded or ModEventKind.ColonyDestroyed)
        {
            return;
        }

        Happened?.Invoke(this, e);
    }
}
