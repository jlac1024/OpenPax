namespace OpenPax.Data.Views;

/// <summary>
/// Fields a widget class adds after the shared base. Picture fields are
/// <c>PPIC</c> ids; by convention id + 100 is the pressed or checked
/// variant and id + 200 the disabled one. Sound ids play on press and
/// id + 100 on release. See docs/formats/views.md.
/// </summary>
public abstract record WidgetDetails;
