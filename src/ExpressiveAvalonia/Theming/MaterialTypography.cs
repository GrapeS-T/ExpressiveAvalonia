using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;

namespace ExpressiveAvalonia.Theming;

public enum MaterialTypeStyle
{
    DisplayLarge, DisplayMedium, DisplaySmall, HeadlineLarge, HeadlineMedium, HeadlineSmall,
    TitleLarge, TitleMedium, TitleSmall, BodyLarge, BodyMedium, BodySmall, LabelLarge, LabelMedium, LabelSmall
}

/// <summary>Applies dynamic type tokens without replacing content or templates.</summary>
public sealed class MaterialTypography : AvaloniaObject
{
    public static readonly AttachedProperty<MaterialTypeStyle> StyleProperty = AvaloniaProperty.RegisterAttached<MaterialTypography, Control, MaterialTypeStyle>("Style", MaterialTypeStyle.BodyLarge);
    public static readonly AttachedProperty<bool> EmphasizedProperty = AvaloniaProperty.RegisterAttached<MaterialTypography, Control, bool>("Emphasized");
    private static readonly AttachedProperty<IDisposable[]?> BindingsProperty = AvaloniaProperty.RegisterAttached<MaterialTypography, Control, IDisposable[]?>("Bindings");

    static MaterialTypography()
    {
        StyleProperty.Changed.AddClassHandler<Control>((control, _) => Apply(control));
        EmphasizedProperty.Changed.AddClassHandler<Control>((control, _) => Apply(control));
    }

    public static MaterialTypeStyle GetStyle(Control control) => control.GetValue(StyleProperty);
    public static void SetStyle(Control control, MaterialTypeStyle value) { control.SetValue(StyleProperty, value); Apply(control); }
    public static bool GetEmphasized(Control control) => control.GetValue(EmphasizedProperty);
    public static void SetEmphasized(Control control, bool value) => control.SetValue(EmphasizedProperty, value);

    private static void Apply(Control control)
    {
        if (control.GetValue(BindingsProperty) is { } previous)
            foreach (var binding in previous) binding.Dispose();
        var prefix = $"Material{GetStyle(control)}{(GetEmphasized(control) ? "Emphasized" : "")}";
        var bindings = new List<IDisposable>
        {
            control.Bind(TextElement.FontSizeProperty, control.GetResourceObservable(prefix + "FontSize"), BindingPriority.Style),
            control.Bind(TextElement.FontWeightProperty, control.GetResourceObservable(prefix + "FontWeight"), BindingPriority.Style),
            control.Bind(TextElement.FontFamilyProperty, control.GetResourceObservable("MaterialFontFamily"), BindingPriority.Style),
            control.Bind(TextElement.LetterSpacingProperty, control.GetResourceObservable(prefix + "LetterSpacing"), BindingPriority.Style)
        };
        if (control is TextBlock text)
            bindings.Add(text.Bind(TextBlock.LineHeightProperty, text.GetResourceObservable(prefix + "LineHeight"), BindingPriority.Style));
        control.SetValue(BindingsProperty, bindings.ToArray());
    }
}
