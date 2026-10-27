using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A Material menu trigger: a pill-shaped label-and-chevron button that owns a
/// <see cref="Button.Flyout"/> (normally a <see cref="MenuFlyout"/>).
/// </summary>
/// <remarks>
/// <para>
/// All button behavior is native: pointer press/release, Space and Enter activation, Escape to
/// dismiss an open flyout, access keys, commands, and the ToggleButton automation pattern.
/// This control adds the menu conventions on top: <see cref="ToggleButton.IsChecked"/> mirrors the
/// flyout's open state (so themes and bindings can react), Down/Up Arrow opens the menu for the
/// native presenter to navigate, and the chevron rotates through the theme while the flyout is open.
/// </para>
/// <para>
/// Placement follows the source menu rules (MenuPosition.kt): prefer below the anchor, flip above
/// when the work area has no room, then slide inside it. The adjustments are only applied when the
/// application has not configured the flyout itself, so an authored placement always wins.
/// </para>
/// </remarks>
public class MaterialMenuButton : ToggleButton
{
    private PopupFlyoutBase? _flyout;

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
        else if (change.Property == FlyoutProperty)
            RewireFlyout(change.GetOldValue<FlyoutBase?>(), change.GetNewValue<FlyoutBase?>());
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RewireFlyout(null, Flyout);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        RewireFlyout(Flyout, null);
        base.OnDetachedFromVisualTree(e);
    }

    private void RewireFlyout(FlyoutBase? oldFlyout, FlyoutBase? newFlyout)
    {
        if (ReferenceEquals(oldFlyout, newFlyout))
            return;

        if (_flyout is not null)
            _flyout.Opening -= OnFlyoutOpening;

        _flyout = newFlyout as PopupFlyoutBase;

        if (_flyout is not null)
            _flyout.Opening += OnFlyoutOpening;
    }

    /// <summary>Applies the source placement adjustments unless the application set its own.</summary>
    private void OnFlyoutOpening(object? sender, EventArgs args)
    {
        if (sender is not PopupFlyoutBase popup)
            return;

        var constraint = PopupPositionerConstraintAdjustment.FlipX
            | PopupPositionerConstraintAdjustment.FlipY
            | PopupPositionerConstraintAdjustment.SlideX
            | PopupPositionerConstraintAdjustment.SlideY;

        if (!popup.IsSet(PopupFlyoutBase.PlacementConstraintAdjustmentProperty))
            popup.PlacementConstraintAdjustment = constraint;

        if (!popup.IsSet(PopupFlyoutBase.PlacementProperty))
            popup.Placement = PlacementMode.Bottom;
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
