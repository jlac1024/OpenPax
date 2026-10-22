namespace OpenPax.Forms.Controls;

/// <summary>A page of a tab control: its tab's text and its content.</summary>
public sealed class TabItem : Element
{
    private Element? _content;

    public string Header { get; set; } = "";

    public Element? Content
    {
        get => _content;
        set => _content = value is null ? null : Adopt(value);
    }

    public override IEnumerable<Element> Children => _content is null ? [] : [_content];
}
