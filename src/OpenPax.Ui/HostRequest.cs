namespace OpenPax.Ui;

/// <summary>Something the screens ask of the host, which owns the window, the movies and the simulation.</summary>
/// <param name="Movie">For <see cref="HostRequestKind.PlayMovie"/>: a file in data/movies.</param>
/// <param name="Choices">For <see cref="HostRequestKind.StartGame"/>: hand the new game back through <see cref="UiApp.GameStarted"/>.</param>
/// <param name="Network">For <see cref="HostRequestKind.StartNetworkGame"/>.</param>
public sealed record HostRequest(HostRequestKind Kind, string? Movie = null, NewGameChoices? Choices = null, NetworkChoices? Network = null);
