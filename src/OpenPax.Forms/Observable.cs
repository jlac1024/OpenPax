using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenPax.Forms;

/// <summary>A base for view models: properties set through <see cref="Set{T}"/> tell bindings they changed.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Changed(property);
        return true;
    }

    protected void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
