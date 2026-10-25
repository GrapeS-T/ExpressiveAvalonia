using Avalonia;
using Avalonia.Controls;

namespace ExpressiveAvalonia.Theming;

/// <summary>Material options for the native selection controls styled by <c>Selection.axaml</c>.</summary>
/// <remarks>
/// The native controls keep their own input, automation and grouping behavior: <see cref="CheckBox"/>
/// cycles through its three states, <see cref="RadioButton"/> enforces its group and
/// <see cref="ToggleSwitch"/> drags its knob. This class only adds the source anatomy that the
/// stock control has no property for.
/// </remarks>
public sealed class MaterialSelection : AvaloniaObject
{
    // Adds the built-in handle glyphs and widens the off handle to the source 24 units.
    private const string SwitchIconsClass = "switch-icons";

    private MaterialSelection() { }

    /// <summary>
    /// Draws the Material switch's built-in on/off glyphs inside the handle, as in the source
    /// "Icon=True" variants. The handle then keeps its 24-unit icon-bearing size in both positions
    /// instead of shrinking to 16 when off.
    /// </summary>
    /// <remarks>
    /// Built-in glyphs are drawn only while <see cref="ToggleSwitch.OnContent"/>,
    /// <see cref="ToggleSwitch.OffContent"/> and their templates are all null, so consumer content
    /// always wins; that content should stay within the 16-unit icon slot. The template reacts
    /// through the <c>switch-icons</c> class.
    /// </remarks>
    public static readonly AttachedProperty<bool> ShowSwitchIconsProperty =
        AvaloniaProperty.RegisterAttached<MaterialSelection, ToggleSwitch, bool>("ShowSwitchIcons");

    static MaterialSelection()
    {
        ShowSwitchIconsProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OnContentProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OffContentProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OnContentTemplateProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
        ToggleSwitch.OffContentTemplateProperty.Changed.AddClassHandler<ToggleSwitch>((control, _) => UpdateClass(control));
    }

    public static bool GetShowSwitchIcons(ToggleSwitch control) => control.GetValue(ShowSwitchIconsProperty);

    public static void SetShowSwitchIcons(ToggleSwitch control, bool value) =>
        control.SetValue(ShowSwitchIconsProperty, value);

    private static void UpdateClass(ToggleSwitch control)
    {
        var show = GetShowSwitchIcons(control)
                   && control.OnContent is null && control.OffContent is null
                   && control.OnContentTemplate is null && control.OffContentTemplate is null;
        var has = control.Classes.Contains(SwitchIconsClass);
        if (show == has) return;
        if (show) control.Classes.Add(SwitchIconsClass);
        else control.Classes.Remove(SwitchIconsClass);
    }
}
