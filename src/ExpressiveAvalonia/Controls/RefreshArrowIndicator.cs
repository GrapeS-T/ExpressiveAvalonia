using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

internal sealed class RefreshArrowIndicator : TemplatedControl
{
    private double _progress, _alpha = .3;
    private MaterialMotion.SpringAnimation? _alphaAnimation;
    private readonly Pen _pen = new() { Thickness = 2.5, LineCap = PenLineCap.Flat };
    private StreamGeometry? _arc, _arrow;
    private double _geometryProgress = double.NaN;

    public RefreshArrowIndicator()
    {
        Width = Height = 16;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        IsHitTestVisible = false;
        DetachedFromVisualTree += (_, _) => { _alphaAnimation?.Dispose(); _alphaAnimation = null; };
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (_progress == value) return;
            _progress = value;
            var target = value >= 1 ? 1 : .3;
            _alphaAnimation ??= MaterialMotion.Animate(this, _alpha, target, ApplyAlpha, MaterialSpring.EffectsDefault);
            _alphaAnimation.Retarget(target);
            InvalidateVisual();
        }
    }
    private void ApplyAlpha(double value) { _alpha = Math.Clamp(value, 0, 1); InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var adjusted = Math.Max(Math.Min(1, _progress) - .4, 0) * 5 / 3;
        if (adjusted <= 0 || Foreground is null) return;
        var tension = Math.Clamp(Math.Abs(_progress) - 1, 0, 2);
        tension -= tension * tension / 4;
        var rotation = (-.25 + .4 * adjusted + tension) * .5;
        var startAngle = rotation * 360;
        var endAngle = (rotation + adjusted * .8) * 360;
        const double radius = 6.75;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (_geometryProgress != _progress)
        {
            _geometryProgress = _progress;
            _arc = new StreamGeometry();
            using (var path = _arc.Open())
            {
                var start = startAngle * Math.PI / 180;
                var end = endAngle * Math.PI / 180;
                path.BeginFigure(center + new Vector(radius * Math.Cos(start), radius * Math.Sin(start)), false);
                path.ArcTo(center + new Vector(radius * Math.Cos(end), radius * Math.Sin(end)),
                    new Size(radius, radius), 0, endAngle - startAngle > 180, SweepDirection.Clockwise);
                path.EndFigure(false);
            }
            var scale = Math.Min(1, adjusted);
            _arrow = new StreamGeometry();
            using (var path = _arrow.Open())
            {
                path.SetFillRule(FillRule.EvenOdd);
                var origin = new Point(radius + center.X - 5 * scale, center.Y - 2.5);
                path.BeginFigure(origin, false);
                path.LineTo(origin + new Vector(5 * scale, 5 * scale));
                path.LineTo(origin + new Vector(10 * scale, 0));
                path.EndFigure(false);
            }
        }
        _pen.Brush = Foreground;
        using (context.PushOpacity(_alpha))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
            Matrix.CreateRotation(rotation * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _pen, _arc!);
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                Matrix.CreateRotation((endAngle - 2.5) * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
                context.DrawGeometry(null, _pen, _arrow!);
        }
    }
}
