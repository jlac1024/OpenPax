using System.Collections.ObjectModel;
using System.Globalization;
using OpenPax.Forms;

namespace OpenPax.Ui.Forms;

/// <summary>The control gallery's view model and code-behind: what its controls bind to and its buttons call.</summary>
public sealed class GalleryModel(UiApp app) : Observable
{
    private int _pushes;

    public bool Animate
    {
        get;
        set => Set(ref field, value);
    } = true;

    public string Name
    {
        get;
        set => Set(ref field, value);
    } = "Commander";

    public ObservableCollection<string> Species { get; } = [.. app.Assets.LoadStockSpecies().Select(s => s.Name)];

    public int SpeciesIndex
    {
        get;
        set => Set(ref field, value);
    }

    public int Stars
    {
        get;
        set => Set(ref field, value);
    } = 30;

    public int Volume
    {
        get;
        set => Set(ref field, value);
    } = 6;

    public string Status
    {
        get;
        private set => Set(ref field, value);
    } = "";

    /// <summary>Opens the gallery over whatever is showing.</summary>
    public static Form Show(UiApp app)
    {
        var model = new GalleryModel(app);
        var form = GameForms.Read("Gallery", model);
        form.Bind(model);
        model.Form = form;
        app.Forms.Show(form);
        return form;
    }

    private Form? Form { get; set; }

    public void OnPush()
    {
        _pushes++;
        Status = string.Format(CultureInfo.InvariantCulture, "Pushed {0} times", _pushes);
    }

    public void OnClose()
    {
        if (Form is not null)
        {
            app.Forms.Close(Form);
        }
    }
}
