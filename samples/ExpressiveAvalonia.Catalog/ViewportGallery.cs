using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Defers expensive example construction until its reserved area intersects the viewport.</summary>
internal sealed class ViewportGallery : Decorator
{
    internal static readonly AttachedProperty<MaterialControlSize?> SizeOverrideProperty =
        AvaloniaProperty.RegisterAttached<ViewportGallery, Control, MaterialControlSize?>("SizeOverride", inherits: true);
    private readonly Func<Control> _create;
    private readonly double _estimatedHeight;
    private Size _lastSize;

    internal ViewportGallery(Func<Control> create, double estimatedHeight)
    {
        _create = create;
        _estimatedHeight = estimatedHeight;
        if (Program.Preview) Child = _create();
        EffectiveViewportChanged += (_, e) =>
        {
            if (Program.Preview) return;
            if (e.EffectiveViewport.Intersects(new Rect(Bounds.Size)))
            {
                Child ??= _create();
                if (GetValue(SizeOverrideProperty) is { } size)
                    foreach (var button in Child.GetLogicalDescendants().OfType<Button>().Prepend(Child).OfType<Button>())
                        MaterialAssist.SetControlSize(button, size);
            }
            else if (Child is not null && !Child.IsKeyboardFocusWithin)
            {
                _lastSize = Child.DesiredSize;
                Child = null;
            }
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not null) return base.MeasureOverride(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : _lastSize.Width,
            _lastSize.Height > 0 ? _lastSize.Height : _estimatedHeight);
    }
}
