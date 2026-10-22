# Forms toolkit (port only)

The port's own controls and the markup they are written in
(decisions.md, "A modern UI toolkit, drawn in true color"). Code:
`src/OpenPax.Forms` (no game knowledge, for mods too),
`src/OpenPax.Ui/Forms` (the game's theme, its forms and their view
models), `src/OpenPax.Render/TrueColor` (the canvas).

## Drawing

The window shows a true-color picture at its own resolution. The original
screens still draw into their 640x480 palette picture, which is scaled by
the largest whole number that fits the window (at least 1), with nearest
pixels, centered with black bars. Forms draw over the whole window on the
canvas. Text in forms is measured as it is drawn (the modern font's own
widths), so forms are laid out for it and never shrink it. Their layout unit is one canvas pixel times that same whole
number, so the game's pixel art and bitmap fonts stay sharp and a form
fills any window shape (960x540 units in a 1920x1080 window). The game's
animated pointer is drawn on the canvas too, at that scale. `--screenshot`
writes the whole window as an RGBA PNG.

## Look

Skins come from the game's own art, each converted picture in its own
palette (`GameTheme`):

| Part | Art |
|---|---|
| Behind a form | the Sector Display's starfield (Universe 1 at 2x), repeated at the window's own pixels |
| Panels | black inside the frame of 44300's list well (Jeff found gray metal panels jarring) |
| Text boxes, combo boxes, header strips | 44300's clamped black field strip, nine-sliced |
| Lists, trees | 44300's framed list well, nine-sliced |
| Buttons, tabs | Main 44501, pressed 44601 |
| Check boxes | Game Options' green box, Main 32033 and 32133 |
| Radio buttons | Player Design's indicator light, Main 25004 and 25104 |
| Spinner arrows | Main 44502, 44504 and their pressed 44602, 44604 |
| Scroll bars | track Main 44313 (its arrows at the ends), thumb 44310 |
| Combo box arrow | Main 44311 |

Colors are palette 5's as the network windows use them: white text in
wells, the titles' red, the buttons' text colors; the accent green is
taken from the check box. Text on the panels and the starfield is light gray.

## Markup

A form is XML. The root is `Form`; an element names a control, attributes
set its properties, and child elements are its content.

```xml
<Form xmlns="https://openpax.local/forms" xmlns:x="https://openpax.local/forms/x">
  <Grid RowDefinitions="Auto,*" ColumnDefinitions="*,2*" Margin="8">
    <TextBlock Text="Name" Look="Dim" />
    <TextBox Grid.Column="1" x:Name="Name" Text="{Binding Name}" Submitted="OnSave" />
  </Grid>
</Form>
```

- Panels: `StackPanel` (Orientation, Spacing), `Grid` (RowDefinitions,
  ColumnDefinitions as "Auto,*,2*,40", RowSpacing, ColumnSpacing),
  `DockPanel` (LastChildFill), `Canvas`, `Border` (Look Panel, Well,
  Strip; Background, BorderBrush, Padding), `ScrollViewer`, `GroupBox`
  (Header), `TabControl` with `TabItem` (Header).
- Controls: `TextBlock` (Text, Look Normal, Dim, Bright, Muted, Heading;
  TextWrapping, TextAlignment, Font, Bold), `Button` (Text, Click),
  `CheckBox` and `RadioButton` (Text, IsChecked, GroupName,
  CheckedChanged), `TextBox` (Text, MaxLength, IsPassword, Placeholder,
  TextChanged, Submitted), `ListBox` (Items as `<Item>` children or
  ItemsSource, DisplayMemberPath, SelectedIndex, SelectedItem,
  FollowLast, SelectionChanged, ItemActivated), `ComboBox` (the same,
  opening a list over everything), `TreeView` with `TreeViewItem`
  (Header, IsExpanded, Tag; SelectedItem, SelectedItemChanged),
  `NumericUpDown`, `Slider` (Minimum, Maximum, Step, Value,
  ValueChanged), `ProgressBar`, `Image` (Source "Main:44300" or a
  mod's, Stretch), `Separator`.
- Every element: Name (or x:Name), Width, Height, Min and Max sizes,
  Margin ("4", "4,2" or "l,t,r,b"), HorizontalAlignment,
  VerticalAlignment, Visibility (Visible, Hidden, Collapsed), IsEnabled,
  ToolTip; Grid.Row, Grid.Column, Grid.RowSpan, Grid.ColumnSpan,
  DockPanel.Dock, Canvas.Left, Canvas.Top.
- Values: numbers, True and False, names of choices, colors "#RRGGBB"
  or "#AARRGGBB".
- Events name a method of the form's code-behind, or failing that of its
  data context; it may take nothing, the sender, or the sender and the
  event's arguments.
- `{Binding Path}` keeps a property equal to one of the data context's
  (a dotted path follows each step). Text, IsChecked, SelectedIndex,
  SelectedItem and Value bind both ways, the rest one way; `Mode=TwoWay`
  or `Mode=OneWay` says otherwise. A view model tells bindings of changes
  through INotifyPropertyChanged (`Observable` helps); an
  ObservableCollection as ItemsSource keeps a list up to date.
- Errors in markup name the line.

A mod adds controls of its own with `ControlRegistry.Register` and reads
forms with `FormReader`; how mods are loaded is decided in Phase 7.

## Input

The top form gets the mouse first, in canvas pixels; where it has nothing
the original screen underneath gets it. A form with a focused element
takes the keys and typing, and the game's key commands wait. Tab moves
the focus. The wheel scrolls lists, trees and scroll views and steps
spinners and sliders.

## Gallery

The developer console's `gallery` shows every control
(`Forms/Markup/Gallery.xml`).
