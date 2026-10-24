using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExpressiveAvalonia.Theming;

/// <summary>Adds focus and Escape activation to native noninteractive tooltips.</summary>
public sealed class MaterialToolTip : AvaloniaObject
{
    public static readonly AttachedProperty<bool> KeyboardEnabledProperty = AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("KeyboardEnabled");
    static MaterialToolTip()
    {
        KeyboardEnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
            {
                control.GotFocus += GotFocus;
                control.LostFocus += LostFocus;
                control.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
                control.DetachedFromVisualTree += Detached;
            }
            else
            {
                control.GotFocus -= GotFocus; control.LostFocus -= LostFocus;
                control.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
                control.DetachedFromVisualTree -= Detached;
                ToolTip.SetIsOpen(control, false);
            }
        });
    }
    public static bool GetKeyboardEnabled(Control control) => control.GetValue(KeyboardEnabledProperty);
    public static void SetKeyboardEnabled(Control control, bool enabled) => control.SetValue(KeyboardEnabledProperty, enabled);
    private static void GotFocus(object? sender, FocusChangedEventArgs args)
    {
        if (sender is Control control && args.Source == control && args.NavigationMethod != NavigationMethod.Pointer && ToolTip.GetTip(control) is not null)
            ToolTip.SetIsOpen(control, true);
    }
    private static void LostFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is Control control && !control.IsKeyboardFocusWithin) ToolTip.SetIsOpen(control, false);
    }
    private static void KeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is Control control && args.Key == Key.Escape && ToolTip.GetIsOpen(control))
        { ToolTip.SetIsOpen(control, false); args.Handled = true; }
    }
    private static void Detached(object? sender, VisualTreeAttachmentEventArgs args)
    { if (sender is Control control) ToolTip.SetIsOpen(control, false); }
}
