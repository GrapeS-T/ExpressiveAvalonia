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
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>Hosts a <see cref="MaterialNavigationDrawer"/> inline or as a dismissible modal overlay.</summary>
/// <remarks>
/// <para>
/// In inline mode (<see cref="IsModal"/> false) an open drawer is docked to the leading edge and
/// the hosted <see cref="Content"/> is inset by the drawer width, so the drawer never covers
/// content. In modal mode the drawer floats over the content above a scrim; tapping the scrim
/// closes it when <see cref="IsLightDismissEnabled"/> is true, Escape always closes it, and
/// choosing a destination closes it when <see cref="CloseOnSelection"/> is true.
/// </para>
/// <para>
/// Opening a modal drawer remembers the focused element, moves focus to the selected destination
/// (or the first focusable one) and traps Tab and Shift+Tab inside the drawer. Closing restores
/// the previous focus. The slide and scrim fade use the Material emphasized curve and the
/// medium-2 duration token, so reduced motion (the theme's Motion setting or the platform
/// preference) removes both.
/// </para>
/// </remarks>
[PseudoClasses(":drawer-open", ":modal")]
[TemplatePart("PART_Overlay", typeof(Panel))]
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_DrawerHost", typeof(Border))]
[TemplatePart("PART_ContentPresenter", typeof(ContentPresenter))]
public sealed class MaterialNavigationDrawerHost : ContentControl
{
    /// <summary>Defines the <see cref="Drawer"/> property.</summary>
    public static readonly StyledProperty<MaterialNavigationDrawer?> DrawerProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, MaterialNavigationDrawer?>(nameof(Drawer));

    /// <summary>Defines the <see cref="IsDrawerOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsDrawerOpenProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(
            nameof(IsDrawerOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsModal"/> property.</summary>
    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(nameof(IsModal), true);

    /// <summary>Defines the <see cref="IsLightDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>Defines the <see cref="CloseOnSelection"/> property.</summary>
    public static readonly StyledProperty<bool> CloseOnSelectionProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, bool>(nameof(CloseOnSelection), true);

    /// <summary>Defines the <see cref="DrawerMargin"/> property.</summary>
    public static readonly StyledProperty<Thickness> DrawerMarginProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, Thickness>(nameof(DrawerMargin), new Thickness(12));

    /// <summary>Defines the <see cref="ScrimOpacity"/> property.</summary>
    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<MaterialNavigationDrawerHost, double>(
            nameof(ScrimOpacity), 0.32d,
            validate: static value => double.IsFinite(value) && value >= 0 && value <= 1);

    private Panel? _overlay;
    private Border? _scrim;
    private Border? _drawerHost;
    private ContentPresenter? _contentPresenter;
    private TranslateTransform? _slide;
    private CancellationTokenSource? _animation;
    private InputElement? _previousFocus;
    private MaterialNavigationDrawer? _subscribedDrawer;

    static MaterialNavigationDrawerHost()
    {
        DrawerProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.OnDrawerChanged());
        IsDrawerOpenProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.OnOpenChanged());
        IsModalProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        DrawerMarginProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.UpdatePresentation(animate: false));
        ScrimOpacityProperty.Changed.AddClassHandler<MaterialNavigationDrawerHost>(
            static (host, _) => host.UpdateScrim());
    }

    public MaterialNavigationDrawerHost()
    {
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        PseudoClasses.Set(":modal", IsModal);
        AddHandler(KeyDownEvent, OnHostKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnHostKeyDownBubble, RoutingStrategies.Bubble);
    }

    /// <summary>Raised after the drawer has been opened.</summary>
    public event EventHandler? DrawerOpened;

    /// <summary>Raised after the drawer has been closed.</summary>
    public event EventHandler? DrawerClosed;

    /// <summary>The drawer surface shown by this host.</summary>
    public MaterialNavigationDrawer? Drawer
    {
        get => GetValue(DrawerProperty);
        set => SetValue(DrawerProperty, value);
    }

    /// <summary>Whether the drawer is open. Two-way by default.</summary>
    public bool IsDrawerOpen
    {
        get => GetValue(IsDrawerOpenProperty);
        set => SetValue(IsDrawerOpenProperty, value);
    }

    /// <summary>
    /// Whether the drawer floats over the content with a scrim. When false the drawer is docked
    /// and pushes the content.
    /// </summary>
    public bool IsModal
    {
        get => GetValue(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>Whether tapping the scrim dismisses a modal drawer. Escape always dismisses.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>Whether choosing a destination dismisses an open modal drawer.</summary>
    public bool CloseOnSelection
    {
        get => GetValue(CloseOnSelectionProperty);
        set => SetValue(CloseOnSelectionProperty, value);
    }

    /// <summary>Inset around the drawer in modal mode. Ignored while docked.</summary>
    public Thickness DrawerMargin
    {
        get => GetValue(DrawerMarginProperty);
        set => SetValue(DrawerMarginProperty, value);
    }

    /// <summary>Target scrim opacity while a modal drawer is open, from 0 to 1.</summary>
    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>Closes a modal drawer as if the user had dismissed it.</summary>
    public void CloseDrawer() => SetCurrentValue(IsDrawerOpenProperty, false);

    /// <summary>Opens the drawer.</summary>
    public void OpenDrawer() => SetCurrentValue(IsDrawerOpenProperty, true);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPointerPressed;

        base.OnApplyTemplate(e);

        _overlay = e.NameScope.Find<Panel>("PART_Overlay");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _drawerHost = e.NameScope.Find<Border>("PART_DrawerHost");
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _slide = _drawerHost?.RenderTransform as TranslateTransform;

        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPointerPressed;

        UpdatePresentation(animate: false);

        // A drawer opened in XAML has no OnOpenChanged transition after the template is applied.
        if (IsDrawerOpen && IsModal)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
            Dispatcher.UIThread.Post(FocusInitialDrawerElement, DispatcherPriority.Loaded);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateDrawerSubscription();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animation?.Cancel();
        _animation = null;
        UpdateDrawerSubscription();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDrawerChanged()
    {
        UpdateDrawerSubscription();
        UpdatePresentation(animate: false);
    }

    // The drawer usually belongs to the host, but a shared drawer must not keep a detached host
    // alive through its SelectionChanged handler.
    private void UpdateDrawerSubscription()
    {
        var target = this.IsAttachedToVisualTree() ? Drawer : null;
        if (ReferenceEquals(_subscribedDrawer, target))
            return;

        if (_subscribedDrawer is { } previous)
            previous.SelectionChanged -= OnDrawerSelectionChanged;

        _subscribedDrawer = target;

        if (target is not null)
            target.SelectionChanged += OnDrawerSelectionChanged;
    }

    private void OnDrawerSelectionChanged(object? sender, MaterialNavigationSelectionChangedEventArgs e)
    {
        if (CloseOnSelection && IsModal && IsDrawerOpen && e.AddedItem is not null)
            CloseDrawer();
    }

    private void OnOpenChanged()
    {
        var open = IsDrawerOpen;
        PseudoClasses.Set(":drawer-open", open);
        UpdatePresentation(animate: true);
        if (open)
        {
            if (IsModal)
            {
                _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
                Dispatcher.UIThread.Post(FocusInitialDrawerElement, DispatcherPriority.Loaded);
            }

            DrawerOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RestoreFocus();
            DrawerClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePresentation(bool animate)
    {
        if (_drawerHost is null || _overlay is null)
            return;

        var open = IsDrawerOpen;
        PseudoClasses.Set(":modal", IsModal);

        if (!IsModal)
        {
            _overlay.IsVisible = false;
            _drawerHost.Margin = default;
            _drawerHost.IsVisible = open;
            if (_slide is not null)
                _slide.X = 0;
            SetContentInset(open ? DrawerWidth() : 0);
            return;
        }

        SetContentInset(0);
        _drawerHost.Margin = DrawerMargin;

        if (!open)
        {
            HideModal();
            return;
        }

        ShowModal(animate);
    }

    private void ShowModal(bool animate)
    {
        if (_drawerHost is null || _overlay is null || _scrim is null)
            return;

        var wasVisible = _overlay.IsVisible && _drawerHost.IsVisible;
        _overlay.IsVisible = true;
        _drawerHost.IsVisible = true;

        var target = 0d;
        if (_slide is not null)
        {
            if (!wasVisible)
                _slide.X = -SlideDistance();
        }

        var scrim = ScrimOpacity;
        if (!animate || MaterialMotion.GetIsReduced(this) || _slide is null)
        {
            if (_slide is not null)
                _slide.X = target;
            _scrim.Opacity = scrim;
            return;
        }

        _ = AnimateAsync(target, scrim, hideWhenDone: false);
    }

    private void HideModal()
    {
        if (_drawerHost is null || _overlay is null || _scrim is null)
            return;

        if (!_overlay.IsVisible)
            return;

        if (!_drawerHost.IsVisible || _slide is null || MaterialMotion.GetIsReduced(this))
        {
            _drawerHost.IsVisible = false;
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

        if (hideWhenDone && !IsDrawerOpen && !source.IsCancellationRequested)
        {
            if (_drawerHost is not null)
                _drawerHost.IsVisible = false;
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
        if (_scrim is null || !IsModal || !IsDrawerOpen)
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
        var width = _drawerHost is { Bounds.Width: > 0 } host ? host.Bounds.Width : DrawerWidth();
        return width + DrawerMargin.Left + DrawerMargin.Right;
    }

    private double DrawerWidth()
    {
        if (Drawer is { } drawer && double.IsFinite(drawer.Width) && drawer.Width > 0)
            return drawer.Width;
        if (_drawerHost is { Bounds.Width: > 0 } host)
            return host.Bounds.Width;
        return 360;
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
        if (!IsModal || !IsLightDismissEnabled || !IsDrawerOpen)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        CloseDrawer();
        e.Handled = true;
    }

    private void OnHostKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsDrawerOpen)
            return;

        if (e.Key == Key.Tab)
            TrapFocus(e);
    }

    private void OnHostKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsModal || !IsDrawerOpen)
            return;

        if (e.Key == Key.Escape)
        {
            CloseDrawer();
            e.Handled = true;
        }
    }

    private void TrapFocus(KeyEventArgs e)
    {
        var focusables = GetDrawerFocusables();
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

    private void FocusInitialDrawerElement()
    {
        if (!IsModal || !IsDrawerOpen)
            return;

        var target = Drawer?.SelectedItem ?? GetDrawerFocusables().FirstOrDefault();
        target?.Focus(NavigationMethod.Tab);
    }

    private List<Control> GetDrawerFocusables()
    {
        var result = new List<Control>();
        if (Drawer is not { } drawer)
            return result;

        if (drawer.Focusable)
            result.Add(drawer);

        foreach (var control in drawer.GetVisualDescendants().OfType<Control>())
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
        var focusInDrawer = focused is not null && Drawer is { } drawer &&
            (ReferenceEquals(focused, drawer) || drawer.IsVisualAncestorOf(focused));

        if (focusInDrawer || focused is null)
            _previousFocus?.Focus();
        _previousFocus = null;
    }
}
