namespace OpenPax.Data.Views;

/// <summary>
/// Shared by value controls (read): the message sent with
/// the value whenever it changes, the value and its limits.
/// </summary>
public sealed record ValueRange(int ChangeMessage, int Value, int Min, int Max);
