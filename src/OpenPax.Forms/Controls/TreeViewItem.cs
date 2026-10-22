namespace OpenPax.Forms.Controls;

/// <summary>A node of a tree view: its header, the nodes under it, and whether they show.</summary>
public sealed class TreeViewItem
{
    public string Header { get; set; } = "";

    public bool IsExpanded { get; set; }

    /// <summary>Anything the program keeps with the node.</summary>
    public object? Tag { get; set; }

    public List<TreeViewItem> Items { get; } = [];
}
