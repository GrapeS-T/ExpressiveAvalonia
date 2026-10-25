using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>A Material split button: a primary action plus a trailing menu anchor.</summary>
/// <remarks>
/// <para>Both halves are real Avalonia buttons, so pointer, Space/Enter, focus visuals, access
/// keys and automation work natively. The primary half raises <see cref="Click"/> and executes
/// <see cref="Command"/>; the trailing half opens <see cref="Flyout"/> with a click, Down,
/// Alt+Down or F4 and reports its open state through <see cref="IsMenuOpen"/> and the
/// <c>:menuopen</c> state layer. Escape and light dismiss are handled by the flyout itself.</para>
/// <para>Geometry follows the source variants: five sizes with the 32/40/56/96/136 DIP visual
/// heights inset inside the 48/48/56/96/136 DIP targets, a 2-DIP gap between the halves, fully
/// rounded outer corners with the size corner radius on the seam, and the size-specific chevron.</para>
/// <para>The theme duration tokens drive the state-layer transitions, so Motion="Reduced"
/// removes the animation without changing the colors.</para>
/// </remarks>
[TemplatePart("PART_PrimaryButton", typeof(Button))]
[TemplatePart("PART_TrailingButton", typeof(Button))]
public class MaterialSplitButton : ContentControl
{
    /// <summary>Optional leading icon; accepts an icon control, image or any other content.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialSplitButton, object?>(nameof(Icon));

    /// <summary>The color treatment: Filled, Tonal, Elevated or Outlined.</summary>
    public static readonly StyledProperty<MaterialButtonVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialButtonVariant>(
            nameof(Variant), MaterialButtonVariant.Filled);

    /// <summary>The Material Expressive size.</summary>
    public static readonly StyledProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialControlSize>(
            nameof(ControlSize), MaterialControlSize.Small);

    /// <summary>Round uses pill silhouettes; square uses the size corner radius.</summary>
    public static readonly StyledProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, MaterialButtonShape>(nameof(ButtonShape));

    /// <summary>The menu shown by the trailing half; a MenuFlyout or any other popup flyout.</summary>
    public static readonly StyledProperty<FlyoutBase?> FlyoutProperty =
        AvaloniaProperty.Register<MaterialSplitButton, FlyoutBase?>(nameof(Flyout));

    /// <summary>Whether the trailing menu is open; can be set to open or close it.</summary>
    public static readonly StyledProperty<bool> IsMenuOpenProperty =
        AvaloniaProperty.Register<MaterialSplitButton, bool>(
            nameof(IsMenuOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Executed when the primary half is activated.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialSplitButton, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialSplitButton, object?>(nameof(CommandParameter));

    /// <summary>The edge length of the leading icon.</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(IconSize), 20d);

    /// <summary>The edge length of the trailing chevron.</summary>
    public static readonly StyledProperty<double> TrailingIconSizeProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(TrailingIconSize), 22d);

    /// <summary>The gap between the leading icon and the label.</summary>
    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<MaterialSplitButton, double>(nameof(ContentSpacing), 8d);

    /// <summary>Raised when the primary half is activated.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MaterialSplitButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private Button? _primaryButton;
    private Button? _trailingButton;
    private FlyoutBase? _attachedFlyout;
    private MaterialSplitButtonAutomationPeer? _peer;
    private bool _syncingMenu;

    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public MaterialButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public MaterialControlSize ControlSize { get => GetValue(ControlSizeProperty); set => SetValue(ControlSizeProperty, value); }
    public MaterialButtonShape ButtonShape { get => GetValue(ButtonShapeProperty); set => SetValue(ButtonShapeProperty, value); }
    public FlyoutBase? Flyout { get => GetValue(FlyoutProperty); set => SetValue(FlyoutProperty, value); }
    public bool IsMenuOpen { get => GetValue(IsMenuOpenProperty); set => SetValue(IsMenuOpenProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public double TrailingIconSize { get => GetValue(TrailingIconSizeProperty); set => SetValue(TrailingIconSizeProperty, value); }
    public double ContentSpacing { get => GetValue(ContentSpacingProperty); set => SetValue(ContentSpacingProperty, value); }

    /// <summary>Raised when the primary half is activated by pointer, keyboard or automation.</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _primaryButton = e.NameScope.Find<Button>("PART_PrimaryButton");
        _trailingButton = e.NameScope.Find<Button>("PART_TrailingButton");

        if (_primaryButton is not null)
            _primaryButton.Click += OnPrimaryClick;
        if (_trailingButton is not null)
        {
            _trailingButton.Click += OnTrailingClick;
            _trailingButton.AddHandler(KeyDownEvent, OnTrailingKeyDown, RoutingStrategies.Tunnel);
        }

        ApplyMetrics();
        AttachFlyout();
        UpdateMenuState(Flyout?.IsOpen == true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachFlyout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachFlyout();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FlyoutProperty)
        {
            AttachFlyout();
            UpdateMenuState(Flyout?.IsOpen == true);
        }
        else if (change.Property == IsMenuOpenProperty && !_syncingMenu)
        {
            if (IsMenuOpen)
                ShowMenu();
            else
                HideMenu();
        }
        else if (change.Property == ControlSizeProperty || change.Property == ButtonShapeProperty ||
                 change.Property == VariantProperty)
        {
            ApplyMetrics();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new MaterialSplitButtonAutomationPeer(this);

    private void DetachParts()
    {
        if (_primaryButton is not null)
            _primaryButton.Click -= OnPrimaryClick;
        if (_trailingButton is not null)
        {
            _trailingButton.Click -= OnTrailingClick;
            _trailingButton.RemoveHandler(KeyDownEvent, OnTrailingKeyDown);
        }
        _primaryButton = null;
        _trailingButton = null;
    }

    private void AttachFlyout()
    {
        DetachFlyout();
        if (Flyout is not { } flyout)
            return;

        _attachedFlyout = flyout;
        flyout.PropertyChanged += OnFlyoutPropertyChanged;
    }

    private void DetachFlyout()
    {
        if (_attachedFlyout is not null)
            _attachedFlyout.PropertyChanged -= OnFlyoutPropertyChanged;
        _attachedFlyout = null;
    }

    private void OnFlyoutPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FlyoutBase.IsOpenProperty)
            UpdateMenuState(Flyout?.IsOpen == true);
    }

    private void UpdateMenuState(bool open)
    {
        _syncingMenu = true;
        SetCurrentValue(IsMenuOpenProperty, open);
        _syncingMenu = false;
        _trailingButton?.Classes.Set("menuopen", open);
        _peer?.NotifyMenuStateChanged(open);
    }

    private void ShowMenu()
    {
        if (Flyout is not { } flyout)
            return;

        flyout.ShowAt(_trailingButton ?? (Control)this);
    }

    // PopupFlyoutBase.Hide respects the flyout's own cancel handling; MenuFlyout and other
    // FlyoutBase implementations are closed through their IsOpen state.
    private void HideMenu()
    {
        if (Flyout is PopupFlyoutBase popup)
            popup.Hide();
        else if (Flyout is { } flyout)
            flyout.IsOpen = false;
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => InvokePrimary();

    private void InvokePrimary()
    {
        if (Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
        RaiseEvent(new RoutedEventArgs(ClickEvent));
    }

    private void OnTrailingClick(object? sender, RoutedEventArgs e)
    {
        if (Flyout?.IsOpen == true)
            HideMenu();
        else
            ShowMenu();
    }

    private void OnTrailingKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        var opens = e.Key == Key.F4 ||
                    e.Key == Key.Down && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Alt;
        if (!opens)
            return;

        ShowMenu();
        e.Handled = true;
    }

    private void ApplyMetrics()
    {
        var (totalHeight, segmentHeight, inset, primaryPadding, trailingWidth,
            iconSize, trailingIconSize, spacing, fontSize, fontWeight, outer, inner) = Metrics(ControlSize, ButtonShape);

        SetCurrentValue(MinHeightProperty, totalHeight);
        SetCurrentValue(PaddingProperty, new Thickness(0, inset));
        SetCurrentValue(FontSizeProperty, fontSize);
        SetCurrentValue(FontWeightProperty, fontWeight);
        SetCurrentValue(CornerRadiusProperty, new CornerRadius(outer));
        SetCurrentValue(IconSizeProperty, iconSize);
        SetCurrentValue(TrailingIconSizeProperty, trailingIconSize);
        SetCurrentValue(ContentSpacingProperty, spacing);

        var dimmed = Variant != MaterialButtonVariant.Outlined;
        _primaryButton?.Classes.Set("dimmed", dimmed);
        _trailingButton?.Classes.Set("dimmed", dimmed);

        if (_primaryButton is not null)
        {
            _primaryButton.MinHeight = segmentHeight;
            _primaryButton.Padding = new Thickness(primaryPadding, 0);
            _primaryButton.CornerRadius = new CornerRadius(outer, inner, inner, outer);
        }

        if (_trailingButton is not null)
        {
            _trailingButton.MinHeight = segmentHeight;
            _trailingButton.Width = trailingWidth;
            _trailingButton.CornerRadius = new CornerRadius(inner, outer, outer, inner);
        }
    }

    // Source geometry: visual heights 32/40/56/96/136 inside 48/48/56/96/136 targets, seam radius
    // 4/4/4/8/12 for round shapes, chevron 22/22/26/38/50, label 14/14/16/24/32.
    private static (double TotalHeight, double SegmentHeight, double Inset, double PrimaryPadding,
        double TrailingWidth, double IconSize, double TrailingIconSize, double Spacing, double FontSize,
        FontWeight FontWeight, double Outer, double Inner) Metrics(
        MaterialControlSize size, MaterialButtonShape shape)
    {
        var (total, segment, inset, padding, trailing, icon, chevron, spacing, font, weight, square, inner) = size switch
        {
            MaterialControlSize.ExtraSmall => (48d, 32d, 8d, 16d, 48d, 20d, 22d, 4d, 14d, FontWeight.Medium, 12d, 4d),
            MaterialControlSize.Small => (48d, 40d, 4d, 16d, 48d, 20d, 22d, 8d, 14d, FontWeight.Medium, 12d, 4d),
            MaterialControlSize.Medium => (56d, 56d, 0d, 24d, 56d, 24d, 26d, 8d, 16d, FontWeight.Medium, 16d, 4d),
            MaterialControlSize.Large => (96d, 96d, 0d, 48d, 96d, 32d, 38d, 12d, 24d, FontWeight.Normal, 28d, 8d),
            _ => (136d, 136d, 0d, 64d, 136d, 40d, 50d, 16d, 32d, FontWeight.Normal, 28d, 12d)
        };

        var outer = shape == MaterialButtonShape.Round ? segment / 2 : square;
        var seam = shape == MaterialButtonShape.Round ? inner : outer;
        return (total, segment, inset, padding, trailing, icon, chevron, spacing, font, weight, outer, seam);
    }

    private sealed class MaterialSplitButtonAutomationPeer(MaterialSplitButton owner)
        : ControlAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState =>
            owner.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => owner.Flyout is not null;

        public void Invoke()
        {
            if (!owner.IsEffectivelyEnabled)
                throw new InvalidOperationException("The split button is disabled.");
            owner.InvokePrimary();
        }

        public void Expand()
        {
            if (!owner.IsEffectivelyEnabled)
                throw new InvalidOperationException("The split button is disabled.");
            owner.ShowMenu();
        }

        public void Collapse() => owner.HideMenu();

        public void NotifyMenuStateChanged(bool open) =>
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(MaterialSplitButton);
    }
}
