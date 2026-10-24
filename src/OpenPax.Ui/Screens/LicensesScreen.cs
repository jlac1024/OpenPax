using OpenPax.Forms.Controls;
using OpenPax.Ui.Forms;

namespace OpenPax.Ui.Screens;

/// <summary>The port's own (mode 6): the open source and attributions window, from Game Start (Forms/Markup/Licenses.xml).</summary>
public sealed class LicensesScreen : FormScreen
{
    public LicensesScreen(UiApp app)
        : this(app, new LicensesModel(app))
    {
    }

    private LicensesScreen(UiApp app, LicensesModel model)
        : base(app, GameForms.Read("Licenses", model))
    {
        Form.Bind(model);
        model.Page = Form.Get<ScrollViewer>("Page");
    }
}
