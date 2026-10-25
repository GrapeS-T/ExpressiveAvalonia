using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material menu trigger: a pill-shaped label-and-chevron button that owns a
/// <see cref="Button.Flyout"/> (normally a <see cref="MenuFlyout"/>).
/// </summary>
/// <remarks>
/// All button behavior is native: pointer press/release, Space and Enter activation, Escape to
/// dismiss an open flyout, access keys, commands, and the ToggleButton automation pattern.
/// This control adds the two menu conventions on top: <see cref="ToggleButton.IsChecked"/> mirrors
/// the flyout's open state (so themes and bindings can react), and Down Arrow opens the menu.
/// The chevron rotates through the theme while the flyout is open.
/// </remarks>
public class MaterialMenuButton : ToggleButton
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateAutomationName();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
            UpdateAutomationName();
    }

    /// <inheritdoc />
    protected override void OnFlyoutOpened()
    {
        base.OnFlyoutOpened();
        SetCurrentValue(IsCheckedProperty, true);
    }

    /// <inheritdoc />
    protected override void OnFlyoutClosed()
    {
        base.OnFlyoutClosed();
        SetCurrentValue(IsCheckedProperty, false);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.Key is Key.Down or Key.Up) && Flyout is { IsOpen: false })
        {
            OpenFlyout();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void UpdateAutomationName()
    {
        if (!string.IsNullOrEmpty(AutomationProperties.GetName(this)))
            return;

        if (Content is string text && !string.IsNullOrWhiteSpace(text))
            AutomationProperties.SetName(this, text.Replace("_", string.Empty, StringComparison.Ordinal));
    }
}
