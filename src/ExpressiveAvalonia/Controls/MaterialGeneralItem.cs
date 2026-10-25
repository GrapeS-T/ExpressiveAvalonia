using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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

    private Border? _tile;
    private Border? _stateLayer;
    private Border? _focusRing;
    private Border? _selectionRing;
    private Control? _caption;

    static MaterialGeneralItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateVisualState());
        ShowCarouselTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        ShowSupportingTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        LabelProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
        SupportingTextProperty.Changed.AddClassHandler<MaterialGeneralItem>(static (item, _) => item.UpdateCaption());
    }

    public MaterialGeneralItem()
    {
        Focusable = true;
        IsTabStop = true;
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
    public void Invoke() => Invoked?.Invoke(this, EventArgs.Empty);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key is Key.Enter or Key.Space)
        {
            Invoke();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_stateLayer is not null) _stateLayer.Opacity = 0.12;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_stateLayer is not null) _stateLayer.Opacity = _pointerOver ? 0.08 : 0;
        if (e.InitialPressMouseButton == MouseButton.Left) Invoke();
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

        _stateLayer = new Border
        {
            Opacity = 0,
            IsHitTestVisible = false,
            CornerRadius = TileCornerRadius
        };
        _stateLayer.Bind(Border.BackgroundProperty, _stateLayer.GetResourceObservable("MaterialOnSurfaceBrush"));

        _focusRing = new Border
        {
            Margin = new Thickness(-3),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(TileCornerRadius.TopLeft + 3),
            IsVisible = false,
            IsHitTestVisible = false
        };
        _focusRing.Bind(Border.BorderBrushProperty, _focusRing.GetResourceObservable("MaterialSecondaryBrush"));

        _selectionRing = new Border
        {
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(TileCornerRadius.TopLeft + 1),
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

        _tile.PointerEntered += (_, _) => UpdateVisualState(pointerOver: true);
        _tile.PointerExited += (_, _) => UpdateVisualState(pointerOver: false);
        _tile.SizeChanged += (_, _) => UpdateClip();
        GotFocus += (_, _) => UpdateVisualState();
        LostFocus += (_, _) => UpdateVisualState();

        var outer = new Grid();
        outer.Children.Add(_tile);
        outer.Children.Add(_focusRing);

        UpdateCaption();
        UpdateVisualState();
        return outer;
    }

    private void UpdateClip()
    {
        if (_tile is null) return;
        var size = _tile.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var radius = TileCornerRadius.TopLeft;
        _tile.Clip = new RectangleGeometry(new Rect(size), radius, radius);
    }

    private void UpdateCaption()
    {
        if (_caption is not Border { Child: StackPanel text }) return;
        text.Children[0].IsVisible = !string.IsNullOrEmpty(Label);
        if (text.Children[0] is TextBlock label) label.Text = Label;
        text.Children[1].IsVisible = ShowSupportingText && !string.IsNullOrEmpty(SupportingText);
        if (text.Children[1] is TextBlock supporting) supporting.Text = SupportingText;
        _caption.IsVisible = ShowCarouselText && (!string.IsNullOrEmpty(Label) || !string.IsNullOrEmpty(SupportingText));
        AutomationProperties.SetName(this, Label ?? SupportingText ?? "Item");
    }

    private bool _pointerOver;

    private void UpdateVisualState(bool? pointerOver = null)
    {
        if (pointerOver is { } value) _pointerOver = value;
        if (_stateLayer is not null) _stateLayer.Opacity = _pointerOver ? 0.08 : 0;
        if (_focusRing is not null) _focusRing.IsVisible = IsFocused;
        if (_selectionRing is not null) _selectionRing.IsVisible = IsSelected;
    }
}
