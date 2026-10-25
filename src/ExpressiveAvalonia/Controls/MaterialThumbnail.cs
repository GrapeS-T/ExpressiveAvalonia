using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The two thumbnail building blocks of the design source.</summary>
/// <remarks>
/// <c>.Building Blocks/image-Thumbnail</c> (local-000296) is a 56 x 56 square;
/// <c>.Building Blocks/video-Thumbnail</c> (local-000751) is a 114 x 64 rectangle (16:9). Both
/// paint the exported placeholder image over the surface container color.
/// </remarks>
public enum MaterialThumbnailKind
{
    /// <summary>Square 56 x 56 media thumbnail.</summary>
    Image,

    /// <summary>16:9 114 x 64 video thumbnail.</summary>
    Video,
}

/// <summary>A media thumbnail: an image cover-cropped into the source 56 x 56 or 114 x 64 frame.</summary>
/// <remarks>
/// <para>
/// The frame follows the source building blocks: <see cref="MaterialThumbnailKind.Image"/> measures
/// 56 x 56, <see cref="MaterialThumbnailKind.Video"/> measures 114 x 64, and the image is stretched
/// with <see cref="Stretch.UniformToFill"/> so a photograph covers the frame the way the source's
/// image fill does. When <see cref="Source"/> is unset the source's own placeholder image
/// (<see cref="MaterialAvatarAssets.ThumbnailPlaceholder"/>) is shown over
/// <see cref="TemplatedControl.Background"/>; supply <see cref="Placeholder"/> to substitute an
/// application placeholder.
/// </para>
/// <para>
/// The source rectangles have square corners, so <see cref="TemplatedControl.CornerRadius"/>
/// defaults to 0. Assign a shape token (for example <c>MaterialShapeMediumCornerRadius</c>) when a
/// rounded thumbnail is wanted.
/// </para>
/// <para>
/// Thumbnails are inert by default. With <see cref="MaterialInteractionSurface.IsInteractive"/> set
/// they activate on pointer release, Space and Enter and raise <c>Click</c>, which is how a
/// thumbnail opens its media in an application.
/// </para>
/// </remarks>
[PseudoClasses(":image", ":video")]
public sealed class MaterialThumbnail : MaterialInteractionSurface
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<MaterialThumbnailKind> KindProperty =
        AvaloniaProperty.Register<MaterialThumbnail, MaterialThumbnailKind>(nameof(Kind));

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<MaterialThumbnail, IImage?>(nameof(Source));

    /// <summary>Defines the <see cref="Placeholder"/> property.</summary>
    public static readonly StyledProperty<IImage?> PlaceholderProperty =
        AvaloniaProperty.Register<MaterialThumbnail, IImage?>(nameof(Placeholder));

    /// <summary>Defines the <see cref="Stretch"/> property.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<MaterialThumbnail, Stretch>(nameof(Stretch), Stretch.UniformToFill);

    /// <summary>Defines the <see cref="AspectRatio"/> property.</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<MaterialThumbnail, double>(nameof(AspectRatio), double.NaN,
            validate: value => double.IsNaN(value) || (double.IsFinite(value) && value > 0));

    /// <summary>Defines the <see cref="StateLayerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> StateLayerBrushProperty =
        AvaloniaProperty.Register<MaterialThumbnail, IBrush?>(nameof(StateLayerBrush));

    /// <summary>Defines the <see cref="FocusRingBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<MaterialThumbnail, IBrush?>(nameof(FocusRingBrush));

    /// <summary>Defines the <see cref="DisplayedSource"/> property.</summary>
    public static readonly DirectProperty<MaterialThumbnail, IImage?> DisplayedSourceProperty =
        AvaloniaProperty.RegisterDirect<MaterialThumbnail, IImage?>(
            nameof(DisplayedSource), thumbnail => thumbnail.DisplayedSource);

    /// <summary>Defines the <see cref="FocusRingCornerRadius"/> property.</summary>
    public static readonly DirectProperty<MaterialThumbnail, CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<MaterialThumbnail, CornerRadius>(
            nameof(FocusRingCornerRadius), thumbnail => thumbnail.FocusRingCornerRadius);

    private IImage? _displayedSource;
    private CornerRadius _focusRingCornerRadius;

    static MaterialThumbnail()
    {
        AffectsMeasure<MaterialThumbnail>(KindProperty, AspectRatioProperty);
        KindProperty.Changed.AddClassHandler<MaterialThumbnail>(
            static (thumbnail, _) => thumbnail.UpdateKindPseudoClasses());
        SourceProperty.Changed.AddClassHandler<MaterialThumbnail>(
            static (thumbnail, _) => thumbnail.UpdateDisplayedSource());
        PlaceholderProperty.Changed.AddClassHandler<MaterialThumbnail>(
            static (thumbnail, _) => thumbnail.UpdateDisplayedSource());
        CornerRadiusProperty.Changed.AddClassHandler<MaterialThumbnail>(
            static (thumbnail, _) => thumbnail.UpdateFocusRing());
    }

    public MaterialThumbnail()
    {
        UpdateKindPseudoClasses();
        UpdateDisplayedSource();
        UpdateFocusRing();
    }

    /// <summary>Which source frame is used.</summary>
    public MaterialThumbnailKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The media image.</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Image shown when <see cref="Source"/> is unset. Null uses the exported source placeholder
    /// from <see cref="MaterialAvatarAssets.ThumbnailPlaceholder"/>.
    /// </summary>
    public IImage? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>How the image fills the frame. The source image fills cover the frame.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Width-to-height ratio used when only the width is constrained. NaN (the default) uses the
    /// source ratio: 1 for an image thumbnail and 114/64 for a video thumbnail.
    /// </summary>
    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <summary>Hover, focus and press overlay color for interactive thumbnails.</summary>
    public IBrush? StateLayerBrush
    {
        get => GetValue(StateLayerBrushProperty);
        set => SetValue(StateLayerBrushProperty, value);
    }

    /// <summary>Keyboard focus ring color for interactive thumbnails.</summary>
    public IBrush? FocusRingBrush
    {
        get => GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    /// <summary>The image the template renders: <see cref="Source"/>, then <see cref="Placeholder"/>, then the source asset.</summary>
    public IImage? DisplayedSource
    {
        get => _displayedSource;
        private set => SetAndRaise(DisplayedSourceProperty, ref _displayedSource, value);
    }

    /// <summary>The focus ring radius: <see cref="TemplatedControl.CornerRadius"/> plus the 2 DIP ring offset.</summary>
    public CornerRadius FocusRingCornerRadius
    {
        get => _focusRingCornerRadius;
        private set => SetAndRaise(FocusRingCornerRadiusProperty, ref _focusRingCornerRadius, value);
    }

    private double EffectiveAspectRatio => double.IsNaN(AspectRatio)
        ? Kind == MaterialThumbnailKind.Video ? 114d / 64d : 1d
        : AspectRatio;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = Kind == MaterialThumbnailKind.Video ? new Size(114, 64) : new Size(56, 56);
        var ratio = EffectiveAspectRatio;
        // Only an explicit Width/Height overrides the source frame; the ratio then preserves the
        // shape when just one dimension is fixed.
        Size frame;
        if (!double.IsNaN(Width) && !double.IsNaN(Height))
            frame = new Size(Width, Height);
        else if (!double.IsNaN(Width))
            frame = new Size(Width, Width / ratio);
        else if (!double.IsNaN(Height))
            frame = new Size(Height * ratio, Height);
        else
            frame = natural;
        base.MeasureOverride(frame);
        return frame;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ThumbnailPeer(this);

    private void UpdateKindPseudoClasses()
    {
        PseudoClasses.Set(":image", Kind == MaterialThumbnailKind.Image);
        PseudoClasses.Set(":video", Kind == MaterialThumbnailKind.Video);
    }

    private void UpdateDisplayedSource() =>
        DisplayedSource = Source ?? Placeholder ?? MaterialAvatarAssets.ThumbnailPlaceholder;

    private void UpdateFocusRing() =>
        FocusRingCornerRadius = new CornerRadius(
            CornerRadius.TopLeft + 2, CornerRadius.TopRight + 2,
            CornerRadius.BottomRight + 2, CornerRadius.BottomLeft + 2);

    private sealed class ThumbnailPeer(MaterialThumbnail owner)
        : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.IsInteractive ? AutomationControlType.Button : AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(MaterialThumbnail);

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(Avalonia.Automation.Provider.IInvokeProvider) && !owner.IsInteractive
                ? null
                : base.GetProviderCore(providerType);

        public void Invoke()
        {
            if (!owner.IsInteractive || !owner.IsEffectivelyEnabled)
                throw new ElementNotEnabledException();
            owner.Invoke();
        }
    }
}
