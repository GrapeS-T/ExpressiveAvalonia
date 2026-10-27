using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

// Frame callbacks follow each indicator's TopLevel and stop when hidden, detached or reduced.
internal sealed class ProgressAnimationClock
{
    private readonly Control _owner;
    private readonly Func<bool> _shouldAnimate;
    private FrameSource? _frameSource;
    private readonly List<Visual> _visibilityAncestors = new();
    private bool _attached;
    private bool _running;
    private long _started;
    public double ElapsedMilliseconds { get; private set; }

    public ProgressAnimationClock(Control owner, Func<bool> shouldAnimate)
    {
        _owner = owner;
        _shouldAnimate = shouldAnimate;
        owner.AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            MaterialMotion.ReducedMotionChanged += ReducedMotionChanged;
            foreach (var ancestor in owner.GetVisualAncestors())
            {
                _visibilityAncestors.Add(ancestor);
                ancestor.PropertyChanged += AncestorPropertyChanged;
            }
            Update();
        };
        owner.DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            _frameSource = null;
            MaterialMotion.ReducedMotionChanged -= ReducedMotionChanged;
            foreach (var ancestor in _visibilityAncestors)
                ancestor.PropertyChanged -= AncestorPropertyChanged;
            _visibilityAncestors.Clear();
            Update();
        };
        owner.PropertyChanged += (_, change) =>
        {
            if (change.Property == CircularProgressIndicator.IsIndeterminateProperty ||
                change.Property == LinearProgressPresenter.IsIndeterminateProperty ||
                change.Property == MaterialMotion.IsReducedProperty || change.Property == Visual.IsVisibleProperty)
                Update();
        };
    }

    private void ReducedMotionChanged(object? sender, EventArgs e) => Update();
    public void Refresh() => Update();

    private void AncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty) Update();
    }

    private void Update()
    {
        var animate = _attached && _owner.IsEffectivelyVisible && _shouldAnimate() && !MaterialMotion.GetIsReduced(_owner);
        if (animate) Start(); else Stop();
        _owner.InvalidateVisual();
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        _started = Stopwatch.GetTimestamp();
        RequestFrame();
    }

    private void Stop()
    {
        ElapsedMilliseconds = 0;
        _running = false;
    }

    private void RequestFrame()
    {
        if (TopLevel.GetTopLevel(_owner) is not { } root) { Stop(); return; }
        if (_frameSource is not { } source || !ReferenceEquals(source.Root, root))
            _frameSource = source = new FrameSource(this, root);
        source.Request();
    }

    private void OnFrame(FrameSource source)
    {
        if (!ReferenceEquals(source, _frameSource) || !_running) return;
        if (!_owner.IsEffectivelyVisible || MaterialMotion.GetIsReduced(_owner)) { Update(); return; }
        ElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
        _owner.InvalidateVisual();
        if (_running) RequestFrame();
    }

    private sealed class FrameSource
    {
        private readonly ProgressAnimationClock _clock;
        private readonly Action<TimeSpan> _callback;
        private bool _pending;

        public FrameSource(ProgressAnimationClock clock, TopLevel root)
        {
            _clock = clock;
            Root = root;
            _callback = OnFrame;
        }

        public TopLevel Root { get; }

        public void Request()
        {
            if (_pending) return;
            _pending = true;
            Root.RequestAnimationFrame(_callback);
        }

        private void OnFrame(TimeSpan timestamp)
        {
            _pending = false;
            _clock.OnFrame(this);
        }
    }
}
