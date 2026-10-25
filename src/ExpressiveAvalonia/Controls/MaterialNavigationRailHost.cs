using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>Where an open expanded rail sits relative to the hosted content.</summary>
public enum MaterialNavigationRailHostPlacement
{
    /// <summary>Pinned to the leading edge at full height; the source <c>Type=Docked</c>.</summary>
    Docked,

    /// <summary>Inset from the edges as a rounded surface; the source <c>Type=Floating</c>.</summary>
    Floating
}

/// <summary>
/// Hosts a <see cref="MaterialNavigationRail"/> as the expanded, modal navigation surface of a
/// screen: open/close lifecycle, scrim, Escape and light dismissal, focus trap and restore, and
/// the source Docked/Floating placement.
/// </summary>
/// <remarks>
/// <para>
/// The expanded rail is a modal destination picker in the source: it covers the screen behind a
/// scrim and returns to the content when a destination is chosen. This host owns that contract.
/// While open in modal mode the rail blocks pointer input to the content, Tab and Shift+Tab cycle
/// inside the rail, Escape closes it, and a primary press on the scrim closes it when
/// <see cref="IsLightDismissEnabled"/> is true. Opening remembers the focused element and focuses
/// the selected destination (or the first focusable control in the rail); closing restores focus.
/// </para>
/// <para>
/// <see cref="Placement"/> mirrors the source <c>Type</c> axis: <see cref="MaterialNavigationRailHostPlacement.Docked"/>
/// pins the rail to the leading edge and insets the content instead of covering it when
/// <see cref="IsModal"/> is false, and <see cref="MaterialNavigationRailHostPlacement.Floating"/>
/// insets it by <see cref="RailMargin"/> as a rounded surface. The host also forwards the
/// placement to the rail's <see cref="MaterialNavigationRail.IsFloating"/> property, because the
/// floating surface treatment belongs to the rail itself.
/// </para>
/// <para>
/// The slide and scrim fade use the emphasized curve and the medium-2 duration token, so reduced
/// motion removes both. Animations are cancelled when the host detaches, so a recycled host never
/// keeps a running storyboard.
/// </para>
/// </remarks>
[PseudoClasses(":rail-open", ":modal", ":floating")]
[TemplatePart("PART_Overlay", typeof(Panel))]
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_RailHost", typeof(Border))]
[TemplatePart("PART_ContentPresenter", typeof(ContentPresenter))]
public sealed class MaterialNavigationRailHost : ContentControl
{
    /// <summary>Defines the <see cref="Rail"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationRail?> RailProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, MaterialNavigationRail?>(nameof(Rail));

    /// <summary>Defines the <see cref="IsRailOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsRailOpenProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(
            nameof(IsRailOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsModal"/> property.</summary>
    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(IsModal), true);

    /// <summary>Defines the <see cref="Placement"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationRailHostPlacement> PlacementProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, MaterialNavigationRailHostPlacement>(nameof(Placement));

    /// <summary>Defines the <see cref="IsLightDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>Defines the <see cref="CloseOnSelection"/> property.</summary>
    public static readonly StyledProperty<bool> CloseOnSelectionProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, bool>(nameof(CloseOnSelection), true);

    /// <summary>Defines the <see cref="RailMargin"/> property.</summary>
    public static readonly StyledProperty<Thickness> RailMarginProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, Thickness>(nameof(RailMargin), new Thickness(12));

    /// <summary>Defines the <see cref="ScrimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialNavigationRailHost, double>(
            nameof(ScrimOpacity), 0.32d,
            validate: static value => double.IsFinite(value) && value >= 0 && value <= 1);

    private Panel? _overlay;
    private Border? _scrim;
    private Border? _railHost;
    private ContentPresenter? _contentPresenter;
    private TranslateTransform? _slide;
    private CancellationTokenSource? _animation;
    private InputElement? _previousFocus;

    static MaterialNavigationRailHost()
    {
        RailProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, change) => host.OnRailChanged(change));
        IsRailOpenProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnOpenChanged());
        IsModalProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        PlacementProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.OnPlacementChanged());
        RailMarginProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        ScrimOpacityProperty.Changed.AddClassHandler<MaterialNavigationRailHost>(
            static (host, _) => host.UpdateScrim());
    }

    public MaterialNavigationRailHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        PseudoClasses.Set(":modal", IsModal);
        AddHandler(KeyDownEvent, OnHostKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnHostKeyDownBubble, RoutingStrategies.Bubble);
    }

    /// <summary>Raised after the rail has been opened.</summary>
    public event EventHandler? RailOpened;

    /// <summary>Raised after the rail has been closed.</summary>
    public event EventHandler? RailClosed;

    /// <summary>The expanded rail shown by this host.</summary>
    public MaterialNavigationRail? Rail
    {
        get => GetValue(RailProperty);
        set => SetValue(RailProperty, value);
    }

    /// <summary>Whether the rail is open. Two-way by default.</summary>
    public bool IsRailOpen
    {
        get => GetValue(IsRailOpenProperty);
        set => SetValue(IsRailOpenProperty, value);
    }

    /// <summary>
    /// Whether the rail covers the content behind a scrim and traps focus. The source expanded
    /// rail is modal, so this defaults to true.
    /// </summary>
    public bool IsModal
    {
        get => GetValue(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>The source <c>Type</c> axis: docked to the leading edge, or floating with margins.</summary>
    public MaterialNavigationRailHostPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Whether tapping the scrim dismisses a modal rail. Escape always dismisses.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>
    /// Whether activating a destination (pointer release, Enter, or Space) dismisses an open
    /// modal rail. Arrow-key browsing only moves the selection and never dismisses it.
    /// </summary>
    public bool CloseOnSelection
    {
        get => GetValue(CloseOnSelectionProperty);
        set => SetValue(CloseOnSelectionProperty, value);
    }

    /// <summary>Inset around a floating rail. Ignored while docked.</summary>
    public Thickness RailMargin
    {
        get => GetValue(RailMarginProperty);
        set => SetValue(RailMarginProperty, value);
    }

    /// <summary>Target scrim opacity while the modal rail is open, from 0 to 1.</summary>
    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>Opens the rail and starts the modal lifecycle.</summary>
    public void OpenRail() => SetCurrentValue(IsRailOpenProperty, true);

    /// <summary>Closes the rail as if the user had dismissed it.</summary>
    public void CloseRail() => SetCurrentValue(IsRailOpenProperty, false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPointerPressed;

        base.OnApplyTemplate(e);

        _overlay = e.NameScope.Find<Panel>("PART_Overlay");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _railHost = e.NameScope.Find<Border>("PART_RailHost");
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _slide = _railHost?.RenderTransform as TranslateTransform;

        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPointerPressed;

        UpdatePresentation(animate: false);

        // A rail opened in XAML has no OnOpenChanged transition after the template is applied.
        if (IsRailOpen && IsModal)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
            Dispatcher.UIThread.Post(FocusInitialRailElement, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animation?.Cancel();
        _animation = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnRailChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.GetOldValue<MaterialNavigationRail?>() is { } oldRail)
            oldRail.ItemInvoked -= OnRailItemInvoked;

        if (change.GetNewValue<MaterialNavigationRail?>() is { } newRail)
        {
            newRail.ItemInvoked += OnRailItemInvoked;
            // The floating surface treatment lives on the rail; the host owns placement.
            newRail.IsFloating = Placement == MaterialNavigationRailHostPlacement.Floating;
        }

        UpdatePresentation(animate: false);
    }

    private void OnPlacementChanged()
    {
        PseudoClasses.Set(":floating", Placement == MaterialNavigationRailHostPlacement.Floating);
        if (Rail is { } rail)
            rail.IsFloating = Placement == MaterialNavigationRailHostPlacement.Floating;
        UpdatePresentation(animate: false);
    }

    /// <summary>
    /// Closes on a destination activation, not on every selection change: arrow navigation in a
    /// rail moves the selection continuously, and dismissing the surface on each step would make
    /// keyboard browsing impossible.
    /// </summary>
    private void OnRailItemInvoked(object? sender, MaterialNavigationRailItemInvokedEventArgs e)
    {
        if (CloseOnSelection && IsModal && IsRailOpen)
            CloseRail();
    }

    private void OnOpenChanged()
    {
        var open = IsRailOpen;
        PseudoClasses.Set(":rail-open", open);
        UpdatePresentation(animate: true);

        if (open)
        {
            if (IsModal)
            {
                _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
                Dispatcher.UIThread.Post(FocusInitialRailElement, DispatcherPriority.Loaded);
            }

            RailOpened?.Invoke(this, EventArgs.Empty);
            return;
        }

        RestoreFocus();
        RailClosed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePresentation(bool animate)
    {
        if (_railHost is null || _overlay is null)
            return;

        var open = IsRailOpen;
        var floating = Placement == MaterialNavigationRailHostPlacement.Floating;
        PseudoClasses.Set(":modal", IsModal);
        PseudoClasses.Set(":floating", floating);

        _railHost.HorizontalAlignment = HorizontalAlignment.Left;
        _railHost.VerticalAlignment = VerticalAlignment.Stretch;
        _railHost.Margin = floating ? RailMargin : default;

        if (!IsModal)
        {
            _overlay.IsVisible = false;
            _railHost.IsVisible = open;
            if (_slide is not null)
                _slide.X = 0;
            SetContentInset(open && !floating ? RailWidth() : 0);
            return;
        }

        SetContentInset(0);
        if (!open)
        {
            HideModal();
            return;
        }

        ShowModal(animate);
    }

    private void ShowModal(bool animate)
    {
        if (_railHost is null || _overlay is null || _scrim is null)
            return;

        var wasVisible = _overlay.IsVisible && _railHost.IsVisible;
        _overlay.IsVisible = true;
        _railHost.IsVisible = true;

        if (_slide is not null && !wasVisible)
            _slide.X = -SlideDistance();

        var scrim = ScrimOpacity;
        if (!animate || MaterialMotion.GetIsReduced(this) || _slide is null)
        {
            if (_slide is not null)
                _slide.X = 0;
            _scrim.Opacity = scrim;
            return;
        }

        _ = AnimateAsync(0, scrim, hideWhenDone: false);
    }

    private void HideModal()
    {
        if (_railHost is null || _overlay is null || _scrim is null)
            return;

        if (!_overlay.IsVisible)
            return;

        if (!_railHost.IsVisible || _slide is null || MaterialMotion.GetIsReduced(this))
        {
            _railHost.IsVisible = false;
            _overlay.IsVisible = false;
            _scrim.Opacity = 0;
            return;
        }

        _ = AnimateAsync(-SlideDistance(), 0, hideWhenDone: true);
    }

    private async Task AnimateAsync(double targetX, double targetOpacity, bool hideWhenDone)
    {
        if (_slide is null || _scrim is null)
            return;

        _animation?.Cancel();
        var source = new CancellationTokenSource();
        _animation = source;
        var duration = SlideDuration();

        try
        {
            await Task.WhenAll(
                RunAsync(_slide, TranslateTransform.XProperty, targetX, duration, source.Token),
                RunAsync(_scrim, Visual.OpacityProperty, targetOpacity, duration, source.Token));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_animation, source))
                _animation = null;
        }

        if (hideWhenDone && !IsRailOpen && !source.IsCancellationRequested)
        {
            if (_railHost is not null)
                _railHost.IsVisible = false;
            if (_overlay is not null)
                _overlay.IsVisible = false;
        }
    }

    private static Task RunAsync(
        Animatable target,
        StyledProperty<double> property,
        double to,
        TimeSpan duration,
        CancellationToken token)
    {
        var from = target.GetValue(property);
        if (duration <= TimeSpan.Zero || Math.Abs(from - to) < 0.001)
        {
            target.SetValue(property, to);
            return Task.CompletedTask;
        }

        var animation = new Animation
        {
            Duration = duration,
            Easing = new SplineEasing(0.2, 0, 0, 1),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(property, to) } },
            },
        };
        return animation.RunAsync(target, token);
    }

    private void UpdateScrim()
    {
        if (_scrim is null || !IsModal || !IsRailOpen)
            return;
        _scrim.Opacity = ScrimOpacity;
    }

    private void SetContentInset(double left)
    {
        if (_contentPresenter is null)
            return;
        _contentPresenter.Margin = new Thickness(left, 0, 0, 0);
    }

    private double SlideDistance()
    {
        if (_railHost is not { } host)
            return RailWidth();

        var width = host.Bounds.Width > 0 ? host.Bounds.Width : RailWidth();
        return width + host.Margin.Left + host.Margin.Right;
    }

    private double RailWidth()
    {
        if (Rail is { } rail && double.IsFinite(rail.Width) && rail.Width > 0)
            return rail.Width;
        if (_railHost is { Bounds.Width: > 0 } host)
            return host.Bounds.Width;
        // The expanded rail is 220 wide; the compact one is 96.
        return Rail?.IsExpanded == false ? 96 : 220;
    }

    private TimeSpan SlideDuration()
    {
        if (MaterialMotion.GetIsReduced(this))
            return TimeSpan.Zero;
        if (this.TryFindResource("MaterialDurationMedium2", out var value) && value is TimeSpan duration)
            return duration;
        return MaterialDurationTokens.All["MaterialDurationMedium2"];
    }

    private void OnScrimPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsModal || !IsLightDismissEnabled || !IsRailOpen)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        CloseRail();
        e.Handled = true;
    }

    private void OnHostKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsRailOpen)
            return;

        if (e.Key == Key.Tab)
            TrapFocus(e);
    }

    private void OnHostKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsRailOpen)
            return;

        if (e.Key == Key.Escape)
        {
            CloseRail();
            e.Handled = true;
        }
    }

    private void TrapFocus(KeyEventArgs e)
    {
        var focusables = GetRailFocusables();
        if (focusables.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var index = focused is null ? -1 : focusables.IndexOf(focused);
        if (index < 0)
        {
            focusables[0].Focus(NavigationMethod.Tab, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        var backwards = (e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
        var next = index + (backwards ? -1 : 1);
        if (next >= focusables.Count)
            next = 0;
        else if (next < 0)
            next = focusables.Count - 1;

        focusables[next].Focus(NavigationMethod.Tab, e.KeyModifiers);
        e.Handled = true;
    }

    private void FocusInitialRailElement()
    {
        if (!IsModal || !IsRailOpen || Rail is not { } rail)
            return;

        var target = rail.SelectedItem as Control ?? GetRailFocusables().FirstOrDefault();
        target?.Focus(NavigationMethod.Tab);
    }

    private List<Control> GetRailFocusables()
    {
        var result = new List<Control>();
        if (Rail is not { } rail)
            return result;

        if (rail.Focusable)
            result.Add(rail);

        foreach (var control in rail.GetVisualDescendants().OfType<Control>())
        {
            if (control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible)
                result.Add(control);
        }

        return result;
    }

    private void RestoreFocus()
    {
        if (!IsModal)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var focusInRail = focused is not null && Rail is { } rail &&
            (ReferenceEquals(focused, rail) || rail.IsVisualAncestorOf(focused));

        if (focusInRail || focused is null)
            _previousFocus?.Focus();
        _previousFocus = null;
    }
}
