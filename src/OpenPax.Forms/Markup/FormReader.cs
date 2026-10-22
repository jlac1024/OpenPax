using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using OpenPax.Forms.Controls;

namespace OpenPax.Forms.Markup;

/// <summary>
/// Reads a form from XAML-like markup (docs/ui/forms.md): an element per
/// control, attributes for properties, Grid.Row and the like for placement,
/// event attributes naming a method of the code-behind (or of the data
/// context), and "{Binding Path}" for bound properties.
/// </summary>
public sealed class FormReader(ControlRegistry? registry = null)
{
    private static readonly HashSet<string> _twoWay = ["Text", "IsChecked", "SelectedIndex", "SelectedItem", "Value"];

    private readonly ControlRegistry _registry = registry ?? ControlRegistry.Default;

    public Form Read(string markup, object? codeBehind = null)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(markup, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            throw new MarkupException(e.Message, e.LineNumber);
        }

        var root = doc.Root ?? throw new MarkupException("The markup is empty.", 0);
        var form = Make(root) as Form ?? throw new MarkupException("The root element must be a Form.", Line(root));
        form.CodeBehind = codeBehind;
        Fill(form, root, form);
        return form;
    }

    public Form ReadFile(string path, object? codeBehind = null) => Read(File.ReadAllText(path), codeBehind);

    private Element Make(XElement x) =>
        _registry.Make(x.Name.LocalName) ?? throw new MarkupException($"Unknown element {x.Name.LocalName}.", Line(x));

    private void Fill(Element element, XElement x, Form form)
    {
        foreach (var attribute in x.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.LocalName == "Class")
            {
                continue;
            }

            Set(element, attribute.Name.LocalName, attribute.Value, form, Line(x));
        }

        foreach (var child in x.Elements())
        {
            AddChild(element, child, form);
        }

        // Text inside an element is its text, as <TextBlock>Hello</TextBlock>.
        if (!x.HasElements && x.Value.Trim() is { Length: > 0 } inner && element.GetType().GetProperty("Text") is { CanWrite: true } text)
        {
            text.SetValue(element, inner);
        }
    }

    private void AddChild(Element parent, XElement x, Form form)
    {
        // Items of lists and nodes of trees are data, not controls.
        if (x.Name.LocalName == "Item" && parent is ItemsControl items)
        {
            items.Items.Add((string?)x.Attribute("Text") ?? x.Value.Trim());
            return;
        }

        if (x.Name.LocalName == "TreeViewItem" && parent is TreeView tree)
        {
            tree.Items.Add(Node(x));
            return;
        }

        var child = Make(x);
        Fill(child, x, form);
        switch (parent)
        {
            case Panel panel:
                panel.Add(child);
                break;
            case TabControl tabs when child is TabItem page:
                tabs.Add(page);
                break;
            case Form f when f.Content is null:
                f.Content = child;
                break;
            case Border b when b.Child is null:
                b.Child = child;
                break;
            case ScrollViewer s when s.Content is null:
                s.Content = child;
                break;
            case GroupBox g when g.Content is null:
                g.Content = child;
                break;
            case TabItem t when t.Content is null:
                t.Content = child;
                break;
            default:
                throw new MarkupException($"{parent.GetType().Name} cannot hold {x.Name.LocalName} here.", Line(x));
        }
    }

    private static TreeViewItem Node(XElement x)
    {
        var node = new TreeViewItem
        {
            Header = (string?)x.Attribute("Header") ?? "",
            IsExpanded = (string?)x.Attribute("IsExpanded") is "True" or "true",
            Tag = (string?)x.Attribute("Tag"),
        };
        node.Items.AddRange(x.Elements().Where(e => e.Name.LocalName == "TreeViewItem").Select(Node));
        return node;
    }

    private static void Set(Element element, string name, string value, Form form, int line)
    {
        // Attached properties, as XAML names them.
        name = name switch
        {
            "Grid.Row" => "GridRow",
            "Grid.Column" => "GridColumn",
            "Grid.RowSpan" => "GridRowSpan",
            "Grid.ColumnSpan" => "GridColumnSpan",
            "DockPanel.Dock" => "Dock",
            "Canvas.Left" => "CanvasLeft",
            "Canvas.Top" => "CanvasTop",
            _ => name,
        };
        var type = element.GetType();
        if (type.GetEvent(name) is { } handler)
        {
            Wire(element, handler, value, form, line);
            return;
        }

        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MarkupException($"{type.Name} has no property {name}.", line);
        if (value.StartsWith("{Binding", StringComparison.Ordinal) && value.EndsWith('}'))
        {
            var (path, mode) = ParseBinding(value[8..^1]);
            form.Bindings.Add(new Binding(element, property, path, mode ?? _twoWay.Contains(property.Name)));
            return;
        }

        if (!property.CanWrite || !Values.TryConvert(value, property.PropertyType, out var converted))
        {
            throw new MarkupException($"{type.Name}.{name} cannot be \"{value}\".", line);
        }

        property.SetValue(element, converted);
    }

    /// <summary>"Path", "Path=X" and ", Mode=TwoWay" or ", Mode=OneWay".</summary>
    private static (string Path, bool? TwoWay) ParseBinding(string inside)
    {
        var path = "";
        bool? twoWay = null;
        foreach (var part in inside.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("Mode=", StringComparison.Ordinal))
            {
                twoWay = part[5..] == "TwoWay";
            }
            else
            {
                path = part.StartsWith("Path=", StringComparison.Ordinal) ? part[5..] : part;
            }
        }

        return (path, twoWay);
    }

    /// <summary>An event attribute names a method of the code-behind, or of the data context when the event fires.</summary>
    private static void Wire(Element element, EventInfo handler, string method, Form form, int line)
    {
        void Invoke(object? sender, EventArgs e)
        {
            foreach (var owner in new[] { form.CodeBehind, element.DataContext })
            {
                if (owner?.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not { } m)
                {
                    continue;
                }

                var args = m.GetParameters().Length switch
                {
                    0 => [],
                    1 => new object?[] { sender },
                    _ => new object?[] { sender, e },
                };
                m.Invoke(owner, args);
                return;
            }

            throw new MarkupException($"No method {method} for {handler.Name}.", line);
        }

        handler.AddEventHandler(element, new EventHandler(Invoke));
    }

    private static int Line(XElement x) => x is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
