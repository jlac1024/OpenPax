using OpenPax.Ui.Forms;

namespace OpenPax.Ui.Screens;

/// <summary>The port's own (mode 5): Game Start's Options, as a form (Forms/Markup/Options.xml).</summary>
public sealed class OptionsScreen : FormScreen
{
    public OptionsScreen(UiApp app)
        : this(app, new OptionsModel(app))
    {
    }

    private OptionsScreen(UiApp app, OptionsModel model)
        : base(app, GameForms.Read("Options", model))
    {
        Form.Bind(model);
    }
}
