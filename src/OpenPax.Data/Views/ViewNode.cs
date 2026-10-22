namespace OpenPax.Data.Views;

/// <summary>
/// One element of a window layout: a window (class 8000), a group (8200),
/// or a widget. Panels drawn entirely by code have no <see cref="Details"/>.
/// </summary>
/// <param name="Title">Windows only.</param>
/// <param name="BackgroundPicture">Groups only; 0 means transparent.</param>
public sealed record ViewNode(int ClassId, WidgetBase Base, WidgetDetails? Details, IReadOnlyList<ViewNode> Children, string? Title, int? BackgroundPicture)
{
    /// <summary>
    /// The first element with this id, searching children in order and into
    /// groups depth first. Ids can repeat within a view (11300 and 32000 have
    /// repeated labels); the original's lookup also returns the first match.
    /// Whether it searches inside groups too is settled when the window code
    /// is ported.
    /// </summary>
    public ViewNode? Find(int id)
    {
        foreach (var child in Children)
        {
            if (child.Base.Id == id)
            {
                return child;
            }

            if (child.Find(id) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }
}
