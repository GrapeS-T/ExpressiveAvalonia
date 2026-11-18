using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A carousel tile: artwork with an optional scrim caption, used by the example home and gallery
/// screens.
/// </summary>
/// <remarks>
/// Source anatomy (Building blocks/General item): a 316x204 rounded tile with an image fill and a
/// bottom gradient carrying the label and supporting text. The caption only shows when
/// <see cref="ShowCarouselText"/> is set, matching the source boolean. The tile is a real
/// interactive target: pointer press, Enter/Space activation, focus ring, hover/press state layer
/// and a selection outline. Assign <see cref="ContentControl.Content"/> to any artwork control
/// (Image, drawing, another composition).
/// <para>
/// This is a library extension from the general-item/carousel design package, not an upstream
/// Compose Material3 component. It is a composition surface: <see cref="IsSelected"/> is a visual
/// state only and does not implement or replace any list selection model.
/// </para>
/// </remarks>
public sealed class MaterialGeneralItem : ContentControl
{
    /// <summary>Primary caption.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, string?>(nameof(Label));

    /// <summary>Supporting caption.</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, string?>(nameof(SupportingText));

    /// <summary>Shows the supporting caption (source default is false).</summary>
    public static readonly StyledProperty<bool> ShowSupportingTextProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, bool>(nameof(ShowSupportingText));

    /// <summary>Shows the scrim caption block (source default is false).</summary>
    public static readonly StyledProperty<bool> ShowCarouselTextProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, bool>(nameof(ShowCarouselText));

    /// <summary>Draws the selected outline.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, bool>(nameof(IsSelected));

    /// <summary>Tile corner radius.</summary>
    public static readonly StyledProperty<CornerRadius> TileCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialGeneralItem, CornerRadius>(nameof(TileCornerRadius), new CornerRadius(16));

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private Border? _tile;
    private MaterialRipple? _stateLayer;
    private Border? _focusRing;
    private Border? _selectionRing;
    private Control? _caption;
    private bool _pressed;
    private IPointer? _pressedPointer;
    private Key? _activationKey;
    private IDisposable? _autoAutomationName;

    static MaterialGeneralItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateVisualState());
        TileCornerRadiusProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCornerRadii());
        ShowCarouselTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        ShowSupportingTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        LabelProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        SupportingTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
    }

    public MaterialGeneralItem()
    {
        ClipToBounds = false;
        Focusable = true;
        IsTabStop = true;
        Classes.CollectionChanged += (_, _) => UpdateVisualState();
        ResourcesChanged += (_, _) => UpdateAutomationName();
        UpdateAutomationName();
        UpdateVisualState();
        Template = new FuncControlTemplate<MaterialGeneralItem>((item, _) => item.Build());
    }

    /// <summary>Raised on pointer release inside the tile, Enter or Space.</summary>
    public event EventHandler? Invoked;

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
    public bool ShowSupportingText { get => GetValue(ShowSupportingTextProperty); set => SetValue(ShowSupportingTextProperty, value); }
    public bool ShowCarouselText { get => GetValue(ShowCarouselTextProperty); set => SetValue(ShowCarouselTextProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public CornerRadius TileCornerRadius { get => GetValue(TileCornerRadiusProperty); set => SetValue(TileCornerRadiusProperty, value); }

    /// <summary>Invokes the tile as if the user activated it.</summary>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
            return;
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialGeneralItemAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled || !IsFocused || e.KeyModifiers != KeyModifiers.None ||
            e.Key is not (Key.Enter or Key.Space)) return;
        _activationKey ??= e.Key;
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_activationKey != e.Key) return;
        _activationKey = null;
        if (!e.Handled && IsFocused && IsEffectivelyEnabled && e.KeyModifiers == KeyModifiers.None)
            Invoke();
        e.Handled = true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _activationKey = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
                CancelActivation();
            UpdateVisualState();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAutomationName();
        UpdateVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelActivation();
        base.OnDetachedFromVisualTree(e);
    }

    private void CancelActivation()
    {
        _activationKey = null;
        _pressed = false;
        var pointer = _pressedPointer;
        _pressedPointer = null;
        if (pointer?.Captured == this) pointer.Capture(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled || e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        _pressed = true;
        _pressedPointer = e.Pointer;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed) return;
        _pressed = false;
        _pressedPointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        _pressedPointer = null;
    }

    private Control Build()
    {
        var artwork = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        artwork[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        artwork[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];

        var label = new TextBlock { Foreground = Brushes.White };
        MaterialTypography.SetStyle(label, MaterialTypeStyle.TitleSmall);
        var supporting = new TextBlock { Foreground = Brushes.White };
        MaterialTypography.SetStyle(supporting, MaterialTypeStyle.BodySmall);

        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Bottom };
        text.Children.Add(label);
        text.Children.Add(supporting);

        var scrim = new Border
        {
            Padding = new Thickness(16, 0, 16, 12),
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(204, 0, 0, 0), 1)
                }
            },
            Child = text
        };
        _caption = scrim;

        _stateLayer = new MaterialRipple
        {
            IsBounded = true,
            CornerRadius = TileCornerRadius,
            InteractionTarget = this,
            IsHitTestVisible = false
        };
        _stateLayer.Bind(MaterialRipple.FillProperty,
            _stateLayer.GetResourceObservable("MaterialOnSurfaceBrush"));

        _focusRing = new Border
        {
            Margin = new Thickness(-5),
            BorderThickness = new Thickness(3),
            CornerRadius = OffsetCornerRadius(TileCornerRadius, 5),
            IsVisible = false,
            IsHitTestVisible = false
        };
        _focusRing.Bind(Border.BorderBrushProperty, _focusRing.GetResourceObservable("MaterialSecondaryBrush"));

        _selectionRing = new Border
        {
            BorderThickness = new Thickness(3),
            CornerRadius = TileCornerRadius,
            IsVisible = false,
            IsHitTestVisible = false
        };
        _selectionRing.Bind(Border.BorderBrushProperty, _selectionRing.GetResourceObservable("MaterialPrimaryBrush"));

        var layers = new Grid();
        layers.Children.Add(artwork);
        layers.Children.Add(scrim);
        layers.Children.Add(_stateLayer);
        layers.Children.Add(_selectionRing);

        _tile = new Border
        {
            CornerRadius = TileCornerRadius,
            ClipToBounds = true,
            Child = layers
        };
        _tile.Bind(Border.BackgroundProperty, _tile.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));

        _tile.SizeChanged += (_, _) => UpdateClip();
        GotFocus += (_, _) => UpdateVisualState();
        LostFocus += (_, _) => UpdateVisualState();

        var outer = new Grid { ClipToBounds = false };
        outer.Children.Add(_tile);
        outer.Children.Add(_focusRing);

        UpdateCaption();
        UpdateCornerRadii();
        UpdateVisualState();
        return outer;
    }

    private void UpdateCornerRadii()
    {
        var radius = TileCornerRadius;
        if (_tile is not null)
            _tile.CornerRadius = radius;
        if (_stateLayer is not null)
            _stateLayer.CornerRadius = radius;
        if (_focusRing is not null)
            _focusRing.CornerRadius = OffsetCornerRadius(radius, 5);
        if (_selectionRing is not null)
            _selectionRing.CornerRadius = radius;
        UpdateClip();
    }

    private void UpdateClip()
    {
        if (_tile is null) return;
        var size = _tile.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        _tile.Clip = CreateRoundedClip(size, TileCornerRadius);
    }

    private static Geometry CreateRoundedClip(Size size, CornerRadius radius)
    {
        var max = Math.Min(size.Width, size.Height) / 2d;
        if (radius.IsUniform)
        {
            var r = Math.Clamp(radius.TopLeft, 0d, max);
            return new RectangleGeometry(new Rect(size), r, r);
        }

        var tl = Math.Clamp(radius.TopLeft, 0d, max);
        var tr = Math.Clamp(radius.TopRight, 0d, max);
        var br = Math.Clamp(radius.BottomRight, 0d, max);
        var bl = Math.Clamp(radius.BottomLeft, 0d, max);
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(tl, 0), isFilled: true);
        ctx.LineTo(new Point(size.Width - tr, 0));
        if (tr > 0)
            ctx.ArcTo(new Point(size.Width, tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(size.Width, size.Height - br));
        if (br > 0)
            ctx.ArcTo(new Point(size.Width - br, size.Height), new Size(br, br), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(bl, size.Height));
        if (bl > 0)
            ctx.ArcTo(new Point(0, size.Height - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(0, tl));
        if (tl > 0)
            ctx.ArcTo(new Point(tl, 0), new Size(tl, tl), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: true);
        return geometry;
    }

    private static CornerRadius OffsetCornerRadius(CornerRadius radius, double delta) =>
        new(
            Math.Max(0d, radius.TopLeft + delta),
            Math.Max(0d, radius.TopRight + delta),
            Math.Max(0d, radius.BottomRight + delta),
            Math.Max(0d, radius.BottomLeft + delta));

    private void UpdateCaption()
    {
        if (_caption is Border { Child: StackPanel text })
        {
            text.Children[0].IsVisible = !string.IsNullOrEmpty(Label);
            if (text.Children[0] is TextBlock label) label.Text = Label;
            text.Children[1].IsVisible = ShowSupportingText && !string.IsNullOrEmpty(SupportingText);
            if (text.Children[1] is TextBlock supporting) supporting.Text = SupportingText;
            _caption.IsVisible = ShowCarouselText && (!string.IsNullOrEmpty(Label) || !string.IsNullOrEmpty(SupportingText));
        }

        UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        var fallback = this.TryFindResource("MaterialGeneralItemText", ActualThemeVariant, out var resource)
                       && resource is string text && !string.IsNullOrEmpty(text)
            ? text
            : "Item";
        var name = !string.IsNullOrEmpty(Label) ? Label
            : !string.IsNullOrEmpty(SupportingText) ? SupportingText
            : fallback;
        _autoAutomationName?.Dispose();
        _autoAutomationName = SetValue(AutomationProperties.NameProperty, name, BindingPriority.Style);
    }

    private void UpdateVisualState()
    {
        var enabled = IsEffectivelyEnabled;
        SetValue(CursorProperty, enabled ? s_hand : null, BindingPriority.StyleTrigger);
        if (_tile is not null)
            _tile.Opacity = enabled ? 1d : 0.38d;
        if (_focusRing is not null)
            _focusRing.IsVisible = enabled && IsFocused && Classes.Contains(":focus-visible");
        if (_selectionRing is not null)
            _selectionRing.IsVisible = enabled && IsSelected;
    }

    private sealed class MaterialGeneralItemAutomationPeer(MaterialGeneralItem owner)
        : ContentControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(MaterialGeneralItem);

        public void Invoke()
        {
            if (!IsEnabled())
                throw new ElementNotEnabledException();
            owner.Invoke();
        }
    }
}
