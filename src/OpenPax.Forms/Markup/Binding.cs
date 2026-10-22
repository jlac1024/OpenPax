using System.ComponentModel;
using System.Reflection;

namespace OpenPax.Forms.Markup;

/// <summary>
/// A property of an element kept equal to a property of its data context
/// ("{Binding Path}"): the element follows the source, and in two-way mode
/// the source follows the element. A dotted path follows each step.
/// </summary>
public sealed class Binding
{
    private readonly Element _target;
    private readonly PropertyInfo _property;
    private readonly string[] _path;
    private readonly List<INotifyPropertyChanged> _watched = [];
    private bool _updating;

    public Binding(Element target, PropertyInfo property, string path, bool twoWay)
    {
        _target = target;
        _property = property;
        _path = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        TwoWay = twoWay;
        target.PropertyChanged += TargetChanged;
    }

    public bool TwoWay { get; }

    /// <summary>Reads the source into the element, and watches the source for changes.</summary>
    public void Refresh()
    {
        foreach (var w in _watched)
        {
            w.PropertyChanged -= SourceChanged;
        }

        _watched.Clear();
        object? at = _target.DataContext;
        foreach (var step in _path)
        {
            if (at is INotifyPropertyChanged watch)
            {
                watch.PropertyChanged += SourceChanged;
                _watched.Add(watch);
            }

            at = at?.GetType().GetProperty(step)?.GetValue(at);
        }

        if (_path.Length == 0)
        {
            at = _target.DataContext;
        }

        _updating = true;
        try
        {
            if (_property.CanWrite)
            {
                _property.SetValue(_target, Values.Coerce(at, _property.PropertyType));
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void SourceChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void TargetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!TwoWay || _updating || e.PropertyName != _property.Name || _path.Length == 0)
        {
            return;
        }

        object? owner = _target.DataContext;
        foreach (var step in _path[..^1])
        {
            owner = owner?.GetType().GetProperty(step)?.GetValue(owner);
        }

        if (owner?.GetType().GetProperty(_path[^1]) is { CanWrite: true } source)
        {
            source.SetValue(owner, Values.Coerce(_property.GetValue(_target), source.PropertyType));
        }
    }
}
