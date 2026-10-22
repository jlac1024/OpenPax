using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>One scripted input for checks without a person: a click at a point, or a key command.</summary>
internal readonly record struct ScriptStep(int X, int Y, UiCommand? Command);
