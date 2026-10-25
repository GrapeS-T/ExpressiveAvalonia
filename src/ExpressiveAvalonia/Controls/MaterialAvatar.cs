using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The three source styles of the <c>Generic avatar</c> component, plus an image avatar.</summary>
/// <remarks>
/// <see cref="Placeholder"/>, <see cref="Check"/> and <see cref="Monogram"/> are the source
/// variants <c>Style=Avatar</c> (local-001643), <c>Style=Check</c> (local-001644) and
/// <c>Style=Monogram</c> (local-001645). <see cref="Image"/> is the same 40 dp container filled
/// with a photograph, which is how the thirty "3D Avatars" packages are used.
/// </remarks>
public enum MaterialAvatarKind
{
    /// <summary>A photograph clipped to the avatar silhouette. Requires <see cref="MaterialAvatar.Source"/>.</summary>
    Image,

    /// <summary>The source person glyph on the primary-container fill (source default).</summary>
    Placeholder,

    /// <summary>A check mark on the primary-container fill.</summary>
    Check,

    /// <summary>One or two characters on the primary-container fill.</summary>
    Monogram,
}

/// <summary>A Material avatar: image, person placeholder, check or monogram inside a circle.</summary>
/// <remarks>
/// <para>
/// The control reproduces the source 40 x 40 component and scales that artwork uniformly, so
/// <see cref="Size"/> changes the whole composition (container, glyph and 16 dp medium letter)
/// instead of only the box. Colors come from <see cref="TemplatedControl.Background"/> and
/// <see cref="TemplatedControl.Foreground"/>; the theme maps them to the primary-container and
/// on-primary-container roles the source uses. <see cref="TemplatedControl.CornerRadius"/>
/// defaults to a full circle.
/// </para>
/// <para>
/// <see cref="IsChecked"/> drives the check glyph. The source draws the tick statically; the
/// reveal is animated with the shared Material springs so a toggling avatar reads as a state
/// change. Reduced motion (<see cref="MaterialMotion.IsReducedProperty"/>) and an off-tree control
/// apply the final scale immediately.
/// </para>
/// <para>
/// Avatars are decorative by default: no tab stop, no pointer cursor and an Image automation
/// peer. Set <see cref="MaterialInteractionSurface.IsInteractive"/> to turn the avatar into a
/// button-like surface with pointer activation, Space/Enter, <c>Click</c>, <c>Command</c> and a
/// Button automation peer.
/// </para>
/// </remarks>
[TemplatePart("PART_Check", typeof(Avalonia.Controls.Shapes.Path))]
[PseudoClasses(":image", ":placeholder", ":check", ":monogram", ":checked")]
public sealed class MaterialAvatar : MaterialInteractionSurface
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<MaterialAvatarKind> KindProperty =
        AvaloniaProperty.Register<MaterialAvatar, MaterialAvatarKind>(
            nameof(Kind), MaterialAvatarKind.Placeholder);

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<MaterialAvatar, IImage?>(nameof(Source));

    /// <summary>Defines the <see cref="Letter"/> property.</summary>
    public static readonly StyledProperty<string?> LetterProperty =
        AvaloniaProperty.Register<MaterialAvatar, string?>(nameof(Letter), "A");

    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<MaterialAvatar, double>(nameof(Size), 40,
            validate: value => double.IsFinite(value) && value > 0);

    /// <summary>Defines the <see cref="IsChecked"/> property.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<MaterialAvatar, bool>(nameof(IsChecked), true);

    /// <summary>Defines the <see cref="StateLayerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> StateLayerBrushProperty =
        AvaloniaProperty.Register<MaterialAvatar, IBrush?>(nameof(StateLayerBrush));

    /// <summary>Defines the <see cref="FocusRingBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<MaterialAvatar, IBrush?>(nameof(FocusRingBrush));

    /// <summary>Defines the <see cref="FocusRingCornerRadius"/> property.</summary>
    public static readonly DirectProperty<MaterialAvatar, CornerRadius> FocusRingCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<MaterialAvatar, CornerRadius>(
            nameof(FocusRingCornerRadius), avatar => avatar.FocusRingCornerRadius);

    private CornerRadius _focusRingCornerRadius;
    private Avalonia.Controls.Shapes.Path? _check;
    private ScaleTransform? _checkScale;
    private MaterialMotion.SpringAnimation? _checkSpring;
    private double _checkProgress = 1;

    static MaterialAvatar()
    {
        AffectsMeasure<MaterialAvatar>(SizeProperty);
        KindProperty.Changed.AddClassHandler<MaterialAvatar>(
            static (avatar, _) => avatar.UpdateKindPseudoClasses());
        IsCheckedProperty.Changed.AddClassHandler<MaterialAvatar>(
            static (avatar, change) => avatar.OnIsCheckedChanged(change.GetNewValue<bool>()));
        CornerRadiusProperty.Changed.AddClassHandler<MaterialAvatar>(
            static (avatar, _) => avatar.UpdateFocusRing());
    }

    public MaterialAvatar()
    {
        UpdateKindPseudoClasses();
        PseudoClasses.Set(":checked", IsChecked);
        UpdateFocusRing();
    }

    /// <summary>Which source style is drawn.</summary>
    public MaterialAvatarKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The photograph for <see cref="MaterialAvatarKind.Image"/>.</summary>
    /// <remarks>
    /// <see cref="MaterialAvatars"/> exposes every exported avatar together with its decoded
    /// image, for example <c>MaterialAvatars.GetByIndex(1).Image</c>.
    /// </remarks>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The monogram text. The source property is <c>Letter</c> with the default "A".</summary>
    public string? Letter
    {
        get => GetValue(LetterProperty);
        set => SetValue(LetterProperty, value);
    }

    /// <summary>Edge length in device-independent units. The source component is 40.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Whether the check glyph is shown for <see cref="MaterialAvatarKind.Check"/>.</summary>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Hover, focus and press overlay color for interactive avatars.</summary>
    public IBrush? StateLayerBrush
    {
        get => GetValue(StateLayerBrushProperty);
        set => SetValue(StateLayerBrushProperty, value);
    }

    /// <summary>Keyboard focus ring color for interactive avatars.</summary>
    public IBrush? FocusRingBrush
    {
        get => GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    /// <summary>The focus ring radius: <see cref="TemplatedControl.CornerRadius"/> plus the 2 DIP ring offset.</summary>
    public CornerRadius FocusRingCornerRadius
    {
        get => _focusRingCornerRadius;
        private set => SetAndRaise(FocusRingCornerRadiusProperty, ref _focusRingCornerRadius, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // The template root is measured against the square frame, so the Viewbox receives a
        // definite 40 x 40 box instead of the parent's available size.
        var frame = new Size(Size, Size);
        base.MeasureOverride(frame);
        return frame;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _checkSpring?.Dispose();
        _checkSpring = null;
        _check = e.NameScope.Find<Avalonia.Controls.Shapes.Path>("PART_Check");
        if (_check is null)
            return;

        _checkScale = new ScaleTransform();
        _check.RenderTransform = _checkScale;
        _check.RenderTransformOrigin = RelativePoint.Center;
        ApplyCheckScale(IsChecked ? 1 : 0);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _checkSpring?.Dispose();
        _checkSpring = null;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarPeer(this);

    private void UpdateKindPseudoClasses()
    {
        PseudoClasses.Set(":image", Kind == MaterialAvatarKind.Image);
        PseudoClasses.Set(":placeholder", Kind == MaterialAvatarKind.Placeholder);
        PseudoClasses.Set(":check", Kind == MaterialAvatarKind.Check);
        PseudoClasses.Set(":monogram", Kind == MaterialAvatarKind.Monogram);
    }

    private void UpdateFocusRing() =>
        FocusRingCornerRadius = new CornerRadius(
            CornerRadius.TopLeft + 2, CornerRadius.TopRight + 2,
            CornerRadius.BottomRight + 2, CornerRadius.BottomLeft + 2);

    private void OnIsCheckedChanged(bool value)
    {
        PseudoClasses.Set(":checked", value);
        var target = value ? 1d : 0d;
        if (_checkScale is null)
        {
            _checkProgress = target;
            return;
        }

        if (MaterialMotion.GetIsReduced(this) || !this.IsAttachedToVisualTree())
        {
            _checkSpring?.Dispose();
            _checkSpring = null;
            ApplyCheckScale(target);
            return;
        }

        if (_checkSpring is null)
            _checkSpring = MaterialMotion.Animate(this, _checkProgress, target, ApplyCheckScale);
        else
            _checkSpring.Retarget(target);
    }

    private void ApplyCheckScale(double progress)
    {
        _checkProgress = progress;
        if (_check is null || _checkScale is null)
            return;
        var scale = Math.Clamp(progress, 0, 1);
        _checkScale.ScaleX = scale;
        _checkScale.ScaleY = scale;
    }

    private sealed class AvatarPeer(MaterialAvatar owner)
        : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner.IsInteractive ? AutomationControlType.Button : AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(MaterialAvatar);

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            if (!string.IsNullOrEmpty(name))
                return name;
            return owner.Kind == MaterialAvatarKind.Monogram && !string.IsNullOrEmpty(owner.Letter)
                ? owner.Letter
                : null;
        }

        // An inert avatar is decoration: it exposes no Invoke pattern even though the peer
        // implements the provider for the interactive case.
        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IInvokeProvider) && !owner.IsInteractive
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
