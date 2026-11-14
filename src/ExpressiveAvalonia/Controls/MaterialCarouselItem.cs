using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A carousel card: media, an optional bottom scrim, and an optional label/supporting-text overlay.
/// </summary>
/// <remarks>
/// The container is a <see cref="ListBoxItem"/>, so pointer selection, disabled handling, focus,
/// and SelectionItemPattern come from Avalonia; the carousel only supplies the size of the card.
/// <see cref="Content"/> is the card media (any control, for example a custom image or play
/// button); <see cref="ImageSource"/> is the built-in convenience background. The overlay is the
/// source "Carousel text" block and stays hidden until <see cref="ShowContent"/> is set, matching
/// the source building block, whose "Show carousel text" property defaults to false.
/// A tap or <see cref="Invoke"/> raises <see cref="Invoked"/> and executes <see cref="Command"/>.
/// </remarks>
public class MaterialCarouselItem : ListBoxItem, ICustomHitTest
{
    /// <summary>Background media. Stretched with <see cref="ImageStretch"/> and clipped to the card.</summary>
    public static readonly StyledProperty<IImage?> ImageSourceProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, IImage?>(nameof(ImageSource));

    /// <summary>How <see cref="ImageSource"/> fills the card. The source cards use a filled crop.</summary>
    public static readonly StyledProperty<Stretch> ImageStretchProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, Stretch>(nameof(ImageStretch), Stretch.UniformToFill);

    /// <summary>The headline of the "Carousel text" overlay (source "Label text").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, string?>(nameof(Label));

    /// <summary>The secondary line of the overlay (source "Supporting text").</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, string?>(nameof(SupportingText));

    /// <summary>Shows the overlay block (source "Show carousel text"; the source default is false).</summary>
    public static readonly StyledProperty<bool> ShowContentProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, bool>(nameof(ShowContent));

    /// <summary>Shows <see cref="SupportingText"/> inside the overlay (source "Show supporting text").</summary>
    public static readonly StyledProperty<bool> ShowSupportingTextProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, bool>(nameof(ShowSupportingText));

    /// <summary>
    /// Width divided by height for <see cref="MaterialCarouselLayout.MultiAspectRatio"/>.
    /// The source building blocks use 16:9, 4:3, 1:1, 3:4 and 9:16 at a 204 unit height.
    /// </summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, double>(nameof(AspectRatio), 16d / 9d,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Executed when the card is activated by tap, Enter, or Space.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MaterialCarouselItem, object?>(nameof(CommandParameter));

    /// <summary>Template plumbing: whether the scrim and overlay text are shown.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly DirectProperty<MaterialCarouselItem, bool> IsOverlayVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, bool>(nameof(IsOverlayVisible),
            item => item.IsOverlayVisible);

    /// <summary>Template plumbing: whether the supporting line has text and is enabled.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly DirectProperty<MaterialCarouselItem, bool> IsSupportingTextVisibleProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, bool>(nameof(IsSupportingTextVisible),
            item => item.IsSupportingTextVisible);

    /// <summary>The keyline size assigned to this card (the pinned <c>CarouselItemDrawInfo.size</c>).</summary>
    public static readonly DirectProperty<MaterialCarouselItem, double> ItemSizeProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, double>(nameof(ItemSize),
            item => item.ItemSize);

    /// <summary>The smallest size any arrangement step assigns to a card.</summary>
    public static readonly DirectProperty<MaterialCarouselItem, double> MinItemSizeProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, double>(nameof(MinItemSize),
            item => item.MinItemSize);

    /// <summary>The largest size any arrangement step assigns to a card.</summary>
    public static readonly DirectProperty<MaterialCarouselItem, double> MaxItemSizeProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, double>(nameof(MaxItemSize),
            item => item.MaxItemSize);

    /// <summary>
    /// The visible part of the card in its own coordinates (the pinned mask rect). Hit testing is
    /// limited to this rectangle, matching the clip applied by the carousel.
    /// </summary>
    public static readonly DirectProperty<MaterialCarouselItem, Rect> MaskRectProperty =
        AvaloniaProperty.RegisterDirect<MaterialCarouselItem, Rect>(nameof(MaskRect),
            item => item.MaskRect);

    private double _itemSize;
    private double _minItemSize;
    private double _maxItemSize;
    private Rect _maskRect;
    private bool _isOverlayVisible;
    private bool _isSupportingTextVisible;
    private bool _pressed;
    private Point _pressedAt;
    private string? _assignedAutomationName;
    private readonly TranslateTransform _parallaxTransform = new();
    private Control? _parallaxContent;

    internal void SetParallax(double translation) => _parallaxTransform.X = translation;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_parallaxContent is not null)
            _parallaxContent.RenderTransform = null;
        base.OnApplyTemplate(e);
        _parallaxContent = e.NameScope.Find<Control>("PART_Card");
        if (_parallaxContent is not null)
            _parallaxContent.RenderTransform = _parallaxTransform;
    }

    static MaterialCarouselItem()
    {
        LabelProperty.Changed.AddClassHandler<MaterialCarouselItem>((item, _) => item.UpdateOverlayState());
        SupportingTextProperty.Changed.AddClassHandler<MaterialCarouselItem>((item, _) => item.UpdateOverlayState());
        ShowContentProperty.Changed.AddClassHandler<MaterialCarouselItem>((item, _) => item.UpdateOverlayState());
        ShowSupportingTextProperty.Changed.AddClassHandler<MaterialCarouselItem>((item, _) => item.UpdateOverlayState());
    }

    /// <summary>Raised when the card is activated; the owning carousel forwards it as ItemInvoked.</summary>
    public event EventHandler? Invoked;

    public IImage? ImageSource { get => GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    public Stretch ImageStretch { get => GetValue(ImageStretchProperty); set => SetValue(ImageStretchProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
    public bool ShowContent { get => GetValue(ShowContentProperty); set => SetValue(ShowContentProperty, value); }
    public bool ShowSupportingText { get => GetValue(ShowSupportingTextProperty); set => SetValue(ShowSupportingTextProperty, value); }
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public bool IsOverlayVisible => _isOverlayVisible;
    public bool IsSupportingTextVisible => _isSupportingTextVisible;

    /// <summary>The keyline size currently assigned to this card.</summary>
    public double ItemSize => _itemSize;

    /// <summary>The smallest size the strategy assigns to a card.</summary>
    public double MinItemSize => _minItemSize;

    /// <summary>The largest size the strategy assigns to a card.</summary>
    public double MaxItemSize => _maxItemSize;

    /// <summary>The card's visible rectangle in its own coordinates.</summary>
    public Rect MaskRect => _maskRect;

    internal void UpdateItemInfo(double itemSize, double minItemSize, double maxItemSize, Rect maskRect)
    {
        SetAndRaise(ItemSizeProperty, ref _itemSize, itemSize);
        SetAndRaise(MinItemSizeProperty, ref _minItemSize, minItemSize);
        SetAndRaise(MaxItemSizeProperty, ref _maxItemSize, maxItemSize);
        SetAndRaise(MaskRectProperty, ref _maskRect, maskRect);
    }

    /// <summary>
    /// Hit testing follows the visible mask instead of the full (clipped) card bounds, so a
    /// partially masked card cannot be clicked in the clipped-away region.
    /// </summary>
    bool ICustomHitTest.HitTest(Point point)
    {
        if (!new Rect(Bounds.Size).Contains(point))
            return false;

        return _maskRect.Width <= 0 || _maskRect.Contains(point);
    }

    /// <summary>Executes <see cref="Command"/> and raises <see cref="Invoked"/>; disabled cards do nothing.</summary>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
            return;

        var command = Command;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled)
        {
            _pressed = true;
            _pressedAt = e.GetPosition(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;

        _pressed = false;
        var position = e.GetPosition(this);
        // A drag that scrolls the strip must not activate the card it started on.
        var drag = Vector.Distance(new Vector(position.X, position.Y),
                                   new Vector(_pressedAt.X, _pressedAt.Y));
        if (IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(position) && drag <= 8)
            Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        base.OnPointerCaptureLost(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
            UpdateAutomationName();
    }

    private void UpdateOverlayState()
    {
        var hasSupporting = ShowSupportingText && !string.IsNullOrEmpty(SupportingText);
        SetAndRaise(IsSupportingTextVisibleProperty, ref _isSupportingTextVisible, hasSupporting);

        var hasLabel = !string.IsNullOrEmpty(Label);
        SetAndRaise(IsOverlayVisibleProperty, ref _isOverlayVisible, ShowContent && (hasLabel || hasSupporting));
        UpdateAutomationName();
    }

    // SelectionItemPattern needs a name even when the card has no Content, so the overlay label
    // becomes the accessible name unless the consumer supplied one explicitly.
    private void UpdateAutomationName()
    {
        var current = AutomationProperties.GetName(this);
        if (!string.IsNullOrEmpty(current) && current != _assignedAutomationName)
            return;

        var name = !string.IsNullOrEmpty(Label) ? Label : Content as string;
        if (string.IsNullOrEmpty(name))
        {
            if (current is not null)
                AutomationProperties.SetName(this, null);
            _assignedAutomationName = null;
            return;
        }

        if (current != name)
            AutomationProperties.SetName(this, name);
        _assignedAutomationName = name;
    }
}
