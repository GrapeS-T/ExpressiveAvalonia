using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>Commit-phase transition family played by <see cref="MaterialTransitionHost"/>.</summary>
public enum MaterialTransitionKind
{
    /// <summary>Pinned horizontal shared axis (forward and backward).</summary>
    HorizontalSharedAxis,

    /// <summary>Pinned fade through.</summary>
    FadeThrough,
}

/// <summary>
/// Plays the pinned Material 3 transition specs between two contents under application control.
/// </summary>
/// <remarks>
/// <para>
/// This is a presentation helper, not a navigation router. The application decides which
/// destination is current and calls <see cref="Navigate"/>; <see cref="Transition"/> selects the
/// pinned <see cref="MaterialTransitions"/> family. The host keeps the outgoing content alive until
/// its exit springs and alpha tween finish, removes it afterwards, and raises
/// <see cref="TransitionCompleted"/>.
/// </para>
/// <para>
/// Navigating back to the content that is still leaving reverses in place: both layers keep their
/// current opacity, offset and scale and are retargeted, so a quick forward → backward gesture
/// never jumps. Navigating elsewhere reuses the leaving layer immediately for the new
/// destination. Outgoing content is removed from the accessibility tree
/// (<see cref="AccessibilityView.Raw"/>) and from hit testing while it leaves; incoming content is
/// the accessible content. Reduced motion, hidden and detached states settle both layers
/// immediately.
/// </para>
/// <para>
/// The gesture phase of predictive back is deliberately left to the application: use
/// <see cref="MaterialTransitions.HorizontalSharedAxis.PredictiveBack"/> or
/// <see cref="MaterialTransitions.FadeThrough.PredictiveBack"/> and apply the returned surfaces to
/// your own layers, then call <see cref="Navigate"/> for the commit phase.
/// </para>
/// </remarks>
public sealed class MaterialTransitionHost : Panel
{
    /// <summary>Defines the <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<MaterialTransitionKind> TransitionProperty =
        AvaloniaProperty.Register<MaterialTransitionHost, MaterialTransitionKind>(nameof(Transition));

    /// <summary>Defines the <see cref="Content"/> property.</summary>
    public static readonly DirectProperty<MaterialTransitionHost, object?> ContentProperty =
        AvaloniaProperty.RegisterDirect<MaterialTransitionHost, object?>(nameof(Content),
            host => host._content, (host, value) => host.Navigate(value));

    /// <summary>Defines the <see cref="IsTransitioning"/> property.</summary>
    public static readonly DirectProperty<MaterialTransitionHost, bool> IsTransitioningProperty =
        AvaloniaProperty.RegisterDirect<MaterialTransitionHost, bool>(nameof(IsTransitioning),
            host => host._isTransitioning);

    private static readonly Func<double, double> Linear = value => value;

    private readonly Layer _first;
    private readonly Layer _second;
    private Layer _incoming;
    private Layer _outgoing;
    private object? _content;
    private bool _isTransitioning;
    private bool _hasContent;
    private bool _pumpScheduled;
    private double _outgoingFadeLifetime;
    private readonly Stopwatch _outgoingClock = new();

    /// <summary>Creates an empty host.</summary>
    public MaterialTransitionHost()
    {
        ClipToBounds = true;
        _first = new Layer();
        _second = new Layer();
        _first.Frame.IsVisible = false;
        _second.Frame.IsVisible = false;
        Children.Add(_first.Frame);
        Children.Add(_second.Frame);
        _incoming = _first;
        _outgoing = _second;
    }

    /// <summary>Raised after the outgoing content has been removed.</summary>
    public event EventHandler? TransitionCompleted;

    /// <summary>The transition family used for the next <see cref="Navigate"/>.</summary>
    public MaterialTransitionKind Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// The current destination. Setting it is equivalent to <c>Navigate(value, forward: true)</c>.
    /// </summary>
    public object? Content
    {
        get => _content;
        set => Navigate(value);
    }

    /// <summary>True while the outgoing layer is still animating away.</summary>
    public bool IsTransitioning => _isTransitioning;

    /// <summary>
    /// Moves to <paramref name="content"/>. <paramref name="forward"/> selects the forward or
    /// backward variant of the current transition family. The initial destination and reduced
    /// motion appear immediately.
    /// </summary>
    public void Navigate(object? content, bool forward = true)
    {
        // Asking again for the destination that is already current or still arriving must not
        // restart its enter animation.
        if (Equals(content, _content) && _incoming.Presenter.Content is not null)
            return;

        var spec = CreateSpec(Transition, forward);
        var width = Math.Max(0, Bounds.Width);

        if (!_hasContent && _incoming.Presenter.Content is null)
        {
            // Initial destination: the pinned AnimatedContent shows it without an enter animation.
            _incoming.SetContent(content);
            _incoming.SnapTo(1, 0, 1);
            _incoming.Frame.IsVisible = content is not null;
            _incoming.Frame.IsHitTestVisible = content is not null;
            _incoming.Frame.ZIndex = 0;
            AutomationProperties.SetAccessibilityView(_incoming.Frame, AccessibilityView.Content);
            _hasContent = content is not null;
            SetContentProperty(content);
            return;
        }

        Layer incoming;
        Layer outgoing;
        var reversing = _outgoing.Presenter.Content is { } leaving &&
                        !ReferenceEquals(content, _content) && Equals(content, leaving);
        if (reversing)
        {
            // Reversing into the content that is still visible: swap roles and retarget from the
            // current visual state on both layers.
            incoming = _outgoing;
            outgoing = _incoming;
            incoming.ResumeAsIncoming();
        }
        else
        {
            outgoing = _incoming;
            incoming = _outgoing;
            incoming.Discard();
            incoming.PrepareEnter(content, spec, width);
        }

        _incoming = incoming;
        _outgoing = outgoing;

        if (ReferenceEquals(incoming, outgoing))
            return;

        incoming.Frame.IsVisible = true;
        incoming.Frame.IsHitTestVisible = true;
        incoming.Frame.ZIndex = (int)Math.Round(spec.TargetContentZIndex);
        AutomationProperties.SetAccessibilityView(incoming.Frame, AccessibilityView.Content);

        outgoing.Frame.IsHitTestVisible = false;
        outgoing.Frame.ZIndex = 0;
        AutomationProperties.SetAccessibilityView(outgoing.Frame, AccessibilityView.Raw);

        SetContentProperty(content);
        SetTransitioning(true);

        if (MaterialMotion.GetIsReduced(this) || !this.IsAttachedToVisualTree())
        {
            incoming.SnapEnter();
            outgoing.SnapExit(spec, width);
            FinalizeOutgoing();
            return;
        }

        outgoing.BeginExit(spec, width);
        incoming.BeginEnter(spec, width);
        _outgoingFadeLifetime = spec.ExitLifetimeSeconds;
        _outgoingClock.Restart();
        SchedulePump();
    }

    private void SetContentProperty(object? content)
    {
        if (!Equals(_content, content))
            SetAndRaise(ContentProperty, ref _content, content);
    }

    private void SetTransitioning(bool value)
    {
        if (_isTransitioning == value)
            return;
        SetAndRaise(IsTransitioningProperty, ref _isTransitioning, value);
    }

    private static MaterialTransitionSpec CreateSpec(MaterialTransitionKind kind, bool forward) =>
        kind == MaterialTransitionKind.FadeThrough
            ? MaterialTransitions.FadeThrough.Create()
            : forward
                ? MaterialTransitions.HorizontalSharedAxis.Forward()
                : MaterialTransitions.HorizontalSharedAxis.Backward();

    private void SchedulePump()
    {
        if (_pumpScheduled || !_outgoing.Exiting)
            return;

        if (TopLevel.GetTopLevel(this) is not { } root)
        {
            FinalizeOutgoing();
            return;
        }

        _pumpScheduled = true;
        root.RequestAnimationFrame(OnPumpFrame);
    }

    private void OnPumpFrame(TimeSpan timestamp)
    {
        _pumpScheduled = false;
        if (!_outgoing.Exiting)
            return;

        var fadeDone = _outgoingClock.Elapsed.TotalSeconds >= _outgoingFadeLifetime;
        if (MaterialMotion.GetIsReduced(this) || (fadeDone && !_outgoing.IsAnimating))
        {
            FinalizeOutgoing();
            return;
        }

        SchedulePump();
    }

    private void FinalizeOutgoing()
    {
        _outgoingClock.Stop();
        _outgoing.Discard();
        SetTransitioning(false);
        TransitionCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);

        var width = 0d;
        var height = 0d;
        foreach (var child in Children)
        {
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? height : availableSize.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _incoming.RetargetSlide(e.NewSize.Width);
        if (_outgoing.Exiting)
            _outgoing.RetargetSlide(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_content is null || _incoming.Presenter.Content is not null)
            return;

        _incoming.SetContent(_content);
        _incoming.Frame.IsVisible = true;
        _incoming.Frame.IsHitTestVisible = true;
        _incoming.Frame.ZIndex = 0;
        AutomationProperties.SetAccessibilityView(_incoming.Frame, AccessibilityView.Content);
        _hasContent = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _first.Discard();
        _second.Discard();
        _outgoingClock.Stop();
        _pumpScheduled = false;
        if (_isTransitioning)
            SetTransitioning(false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>One of the two content layers, with its own visual state and animations.</summary>
    private sealed class Layer
    {
        public Border Frame { get; } = new() { ClipToBounds = true };
        public ContentPresenter Presenter { get; } = new()
        {
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        private readonly TranslateTransform _translation = new();
        private readonly ScaleTransform _scale = new();
        private MaterialMotion.SpringAnimation? _slide;
        private MaterialMotion.SpringAnimation? _scaleAnimation;
        private MaterialMotion.TweenAnimation? _fade;
        private double _offset;
        private double _scaleValue = 1;
        private double _opacity = 1;

        public Layer()
        {
            Presenter.RenderTransform = _scale;
            Presenter.RenderTransformOrigin = RelativePoint.Center;
            Frame.RenderTransform = _translation;
            Frame.Child = Presenter;
            Frame.IsHitTestVisible = false;
        }

        public object? Content
        {
            get => Presenter.Content;
            set => Presenter.Content = value;
        }

        public bool Exiting { get; private set; }

        public bool IsAnimating =>
            _slide is { IsRunning: true } || _scaleAnimation is { IsRunning: true };

        public void SetContent(object? content)
        {
            DisposeAnimations();
            Content = content;
            Exiting = false;
            Apply(1, 0, 1);
        }

        /// <summary>Replaces the content and puts the layer at the spec's enter start.</summary>
        public void PrepareEnter(object? content, MaterialTransitionSpec spec, double width)
        {
            DisposeAnimations();
            Content = content;
            Exiting = false;
            Frame.IsVisible = true;
            Apply(spec.EnterFromAlpha, spec.EnterFromOffsetFraction * width, spec.EnterFromScale);
        }

        /// <summary>Keeps the current visual state but stops treating the layer as outgoing.</summary>
        public void ResumeAsIncoming()
        {
            _fade?.Dispose();
            _fade = null;
            Exiting = false;
            Frame.IsVisible = true;
        }

        public void BeginEnter(MaterialTransitionSpec spec, double width) =>
            StartAnimations(
                targetAlpha: 1,
                targetOffsetFraction: 0,
                targetScale: 1,
                spec.EnterFadeSeconds,
                spec.EnterFadeDelaySeconds,
                width);

        public void BeginExit(MaterialTransitionSpec spec, double width)
        {
            Exiting = true;
            StartAnimations(
                targetAlpha: spec.ExitToAlpha,
                targetOffsetFraction: spec.ExitToOffsetFraction,
                targetScale: spec.ExitToScale,
                spec.ExitFadeSeconds,
                spec.ExitFadeDelaySeconds,
                width);
        }

        public void SnapEnter() => Apply(1, 0, 1);

        public void SnapExit(MaterialTransitionSpec spec, double width) =>
            Apply(spec.ExitToAlpha, spec.ExitToOffsetFraction * width, spec.ExitToScale);

        private void StartAnimations(double targetAlpha, double targetOffsetFraction,
            double targetScale, double fadeSeconds, double fadeDelay, double width)
        {
            _fade?.Dispose();
            _fade = MaterialMotion.Tween(Frame, _opacity, targetAlpha, fadeSeconds, fadeDelay,
                value => { _opacity = value; Apply(); }, Linear);

            _slideFraction = targetOffsetFraction;
            if (_slide is not null)
                _slide.Retarget(targetOffsetFraction * width);
            else
                _slide = MaterialMotion.Animate(Frame, _offset, targetOffsetFraction * width,
                    value => { _offset = value; Apply(); },
                    stiffness: 400, dampingRatio: 1, visibilityThreshold: 0.5);

            if (_scaleAnimation is not null)
                _scaleAnimation.Retarget(targetScale);
            else
                _scaleAnimation = MaterialMotion.Animate(Frame, _scaleValue, targetScale,
                    value => { _scaleValue = value; Apply(); },
                    stiffness: 400, dampingRatio: 1, visibilityThreshold: 0.002);
        }

        private double _slideFraction;

        /// <summary>Re-targets a running slide to a new container width.</summary>
        public void RetargetSlide(double width)
        {
            if (_slide is { IsRunning: true })
                _slide.Retarget(_slideFraction * width);
        }

        public void SnapTo(double alpha, double offset, double scale) => Apply(alpha, offset, scale);

        public void Discard()
        {
            DisposeAnimations();
            Content = null;
            Exiting = false;
            Frame.IsVisible = false;
            Frame.IsHitTestVisible = false;
            Apply(1, 0, 1);
        }

        private void DisposeAnimations()
        {
            _fade?.Dispose();
            _fade = null;
            _slide?.Dispose();
            _slide = null;
            _scaleAnimation?.Dispose();
            _scaleAnimation = null;
        }

        private void Apply() => Apply(_opacity, _offset, _scaleValue);

        private void Apply(double opacity, double offset, double scale)
        {
            _opacity = opacity;
            _offset = offset;
            _scaleValue = scale;
            Frame.Opacity = opacity;
            _translation.X = offset;
            _translation.Y = 0;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
        }
    }
}