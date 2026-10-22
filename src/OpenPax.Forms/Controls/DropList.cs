namespace OpenPax.Forms.Controls;

/// <summary>A combo box's open list: a pick on release closes it and sets the combo box.</summary>
internal sealed class DropList(ComboBox owner) : ListBox
{
    public override Element? OverlayOwner => owner;

    protected internal override void OnMouseUp(int x, int y, bool over)
    {
        if (over && IndexAt(x, y) is >= 0 and var i)
        {
            owner.Picked(i);
        }
    }

    protected internal override bool OnKey(Input.FormKey key, bool shift)
    {
        if (key == Input.FormKey.Return && SelectedIndex >= 0)
        {
            owner.Picked(SelectedIndex);
            return true;
        }

        return base.OnKey(key, shift);
    }
}
