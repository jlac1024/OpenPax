using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Children taking the edges in turn (DockPanel.Dock), the last filling what is left.</summary>
public sealed class DockPanel : Panel
{
    public bool LastChildFill { get; set; } = true;

    protected override Size MeasureOverride(Size available)
    {
        int usedW = 0, usedH = 0, needW = 0, needH = 0;
        foreach (var child in Shown)
        {
            var d = child.Measure(new Size(Math.Max(0, available.Width - usedW), Math.Max(0, available.Height - usedH)));
            if (child.Dock is Dock.Left or Dock.Right)
            {
                needH = Math.Max(needH, usedH + d.Height);
                usedW += d.Width;
            }
            else
            {
                needW = Math.Max(needW, usedW + d.Width);
                usedH += d.Height;
            }
        }

        return new Size(Math.Max(needW, usedW), Math.Max(needH, usedH));
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var room = bounds;
        var shown = Shown.ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var child = shown[i];
            var d = child.DesiredSize;
            if (LastChildFill && i == shown.Count - 1)
            {
                child.Arrange(room);
                break;
            }

            switch (child.Dock)
            {
                case Dock.Left:
                    child.Arrange(room with { Width = d.Width });
                    room = new Rect(room.X + d.Width, room.Y, Math.Max(0, room.Width - d.Width), room.Height);
                    break;
                case Dock.Right:
                    child.Arrange(new Rect(room.Right - d.Width, room.Y, d.Width, room.Height));
                    room = room with { Width = Math.Max(0, room.Width - d.Width) };
                    break;
                case Dock.Top:
                    child.Arrange(room with { Height = d.Height });
                    room = new Rect(room.X, room.Y + d.Height, room.Width, Math.Max(0, room.Height - d.Height));
                    break;
                case Dock.Bottom:
                    child.Arrange(new Rect(room.X, room.Bottom - d.Height, room.Width, d.Height));
                    room = room with { Height = Math.Max(0, room.Height - d.Height) };
                    break;
            }
        }
    }
}
