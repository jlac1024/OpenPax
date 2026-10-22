using System.Collections;
using System.Collections.Specialized;

namespace OpenPax.Forms.Controls;

/// <summary>
/// A control showing a list of items: its own Items, or an ItemsSource it
/// follows (an ObservableCollection updates it). Each item shows as its
/// DisplayMemberPath property, or as text.
/// </summary>
public abstract class ItemsControl : Element
{
    private IEnumerable? _source;
    private int _selected = -1;

    public List<object> Items { get; } = [];

    public IEnumerable? ItemsSource
    {
        get => _source;
        set
        {
            if (_source is INotifyCollectionChanged old)
            {
                old.CollectionChanged -= SourceChanged;
            }

            _source = value;
            if (value is INotifyCollectionChanged now)
            {
                now.CollectionChanged += SourceChanged;
            }

            Reload();
        }
    }

    public string? DisplayMemberPath { get; set; }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, Math.Clamp(value, -1, Items.Count - 1)))
            {
                Changed(nameof(SelectedItem));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? SelectedItem
    {
        get => _selected >= 0 && _selected < Items.Count ? Items[_selected] : null;
        set => SelectedIndex = value is null ? -1 : Items.IndexOf(value);
    }

    public event EventHandler? SelectionChanged;

    /// <summary>The text an item shows as.</summary>
    public string TextOf(object? item)
    {
        if (item is null)
        {
            return "";
        }

        if (DisplayMemberPath is { Length: > 0 } path && item.GetType().GetProperty(path) is { } p)
        {
            return p.GetValue(item)?.ToString() ?? "";
        }

        return item.ToString() ?? "";
    }

    private void SourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Reload();

    private void Reload()
    {
        var keep = SelectedItem;
        Items.Clear();
        if (_source is not null)
        {
            Items.AddRange(_source.Cast<object>());
        }

        _selected = keep is null ? -1 : Items.IndexOf(keep);
        Changed(nameof(Items));
    }
}
