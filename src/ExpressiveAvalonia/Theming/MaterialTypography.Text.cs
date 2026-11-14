using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

public sealed partial class MaterialTypography
{
    public static readonly AttachedProperty<MaterialTextAutoSize?> AutoSizeProperty =
        AvaloniaProperty.RegisterAttached<MaterialTypography, TextBlock, MaterialTextAutoSize?>("AutoSize");
    private static readonly AttachedProperty<AutoSizeTracker?> AutoSizeTrackerProperty =
        AvaloniaProperty.RegisterAttached<MaterialTypography, TextBlock, AutoSizeTracker?>("AutoSizeTracker");

    public static MaterialTextAutoSize? GetAutoSize(TextBlock text) => text.GetValue(AutoSizeProperty);
    public static void SetAutoSize(TextBlock text, MaterialTextAutoSize? value) => text.SetValue(AutoSizeProperty, value);

    /// <summary>Native inline-hosted link. Caller local foreground/decorations override Material defaults.</summary>
    public static InlineUIContainer CreateLink(string text, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var caption = new TextBlock { Text = text };
        caption.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline, BindingPriority.Style);
        var link = new HyperlinkButton { Content = caption, Padding = new Thickness(0), MinHeight = 0, MinWidth = 0 };
        link.Bind(HyperlinkButton.ForegroundProperty, link.GetResourceObservable("MaterialPrimaryBrush"), BindingPriority.Style);
        link.Click += (_, _) => action();
        return new InlineUIContainer(link);
    }

    private static void UpdateAutoSize(TextBlock text)
    {
        text.GetValue(AutoSizeTrackerProperty)?.Dispose();
        text.SetValue(AutoSizeTrackerProperty, GetAutoSize(text) is null ? null : new AutoSizeTracker(text));
    }

    private sealed class AutoSizeTracker : IDisposable
    {
        private readonly TextBlock _text;
        private IDisposable? _font;
        private bool _updating, _queued, _disposed, _dirty = true;
        private Size _lastSize;
        private double _size = double.NaN;
        private InlineCollection? _inlines;
        private readonly List<Inline> _observedInlines = [];

        public AutoSizeTracker(TextBlock text)
        {
            _text = text;
            text.PropertyChanged += Changed;
            text.LayoutUpdated += LayoutUpdated;
            text.AttachedToVisualTree += Attached;
            TrackInlines();
            Queue();
        }

        public void Dispose()
        {
            _disposed = true;
            _text.PropertyChanged -= Changed;
            _text.LayoutUpdated -= LayoutUpdated;
            _text.AttachedToVisualTree -= Attached;
            if (_inlines is not null) _inlines.Invalidated -= InlinesChanged;
            foreach (var inline in _observedInlines) inline.PropertyChanged -= InlineChanged;
            _font?.Dispose();
        }

        private void Attached(object? sender, VisualTreeAttachmentEventArgs e) { _dirty = true; Queue(); }
        private void InlinesChanged(object? sender, EventArgs e)
        {
            if (_updating) return;
            TrackInlines();
            _dirty = true;
            Queue();
        }

        private void InlineChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updating) return;
            _dirty = true;
            Queue();
        }

        private void TrackInlines()
        {
            if (_inlines is not null) _inlines.Invalidated -= InlinesChanged;
            foreach (var inline in _observedInlines) inline.PropertyChanged -= InlineChanged;
            _observedInlines.Clear();
            _inlines = _text.Inlines;
            if (_inlines is not null) _inlines.Invalidated += InlinesChanged;
            if (_inlines is not null) Observe(_inlines);

            void Observe(InlineCollection inlines)
            {
                foreach (var inline in inlines)
                {
                    _observedInlines.Add(inline);
                    inline.PropertyChanged += InlineChanged;
                    if (inline is Span span) Observe(span.Inlines);
                }
            }
        }

        private void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updating || e.Property == AutoSizeTrackerProperty || e.Property == AutoSizeProperty) return;
            if (e.Property == TextBlock.InlinesProperty) TrackInlines();
            if (e.Property == TextBlock.TextProperty || e.Property == TextBlock.InlinesProperty ||
                e.Property == TextBlock.FontFamilyProperty || e.Property == TextBlock.FontWeightProperty ||
                e.Property == TextBlock.FontStyleProperty || e.Property == TextBlock.FontStretchProperty ||
                e.Property == TextBlock.FontSizeProperty || e.Property == TextBlock.LetterSpacingProperty ||
                e.Property == TextBlock.LineHeightProperty || e.Property == TextBlock.LineSpacingProperty ||
                e.Property == TextBlock.FontFeaturesProperty || e.Property == TextBlock.MaxLinesProperty ||
                e.Property == TextBlock.TextWrappingProperty || e.Property == TextBlock.TextTrimmingProperty ||
                e.Property == TextBlock.PaddingProperty || e.Property == TextBlock.FlowDirectionProperty)
            { _dirty = true; Queue(); }
        }

        private void LayoutUpdated(object? sender, EventArgs e)
        {
            if (_lastSize != _text.Bounds.Size) { _dirty = true; Queue(); }
        }

        private void Queue()
        {
            if (_disposed || _queued || !_text.IsAttachedToVisualTree()) return;
            _queued = true;
            Dispatcher.UIThread.Post(Update, DispatcherPriority.Loaded);
        }

        private void Update()
        {
            _queued = false;
            if (_disposed || !_dirty || !_text.IsAttachedToVisualTree() || GetAutoSize(_text) is not { } config) return;
            var size = _text.Bounds.Size.Deflate(_text.Padding);
            if (size.Width <= 0 || size.Height <= 0) return;
            _lastSize = _text.Bounds.Size;
            _dirty = false;
            _updating = true;
            try
            {
                var candidate = config.FindSize(_text, size);
                if (candidate != _size)
                {
                    _font?.Dispose();
                    _size = candidate;
                    _font = _text.SetValue(TextBlock.FontSizeProperty, candidate, BindingPriority.StyleTrigger);
                }
                _text.InvalidateMeasure();
            }
            finally { _updating = false; }
        }
    }
}
