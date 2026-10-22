namespace OpenPax.Data.Views;

/// <summary>
/// Screen-specific panels that store one byte: 9000 map view (draw mode 0
/// or 1, read), 10002 fleet color panel (1: six ships,
/// 0: three), 10003 flag selector (1: a strip of seven flags, 0: one),
/// 18001 research slot (text color).
/// </summary>
public sealed record ByteOptionDetails(byte Value) : WidgetDetails;
