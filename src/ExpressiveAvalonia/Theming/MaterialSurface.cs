using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// The pinned Compose M3 tonal-elevation contract for a native Avalonia surface.
/// </summary>
/// <remarks>
/// <para>
/// A surface is any control that renders its container through a <see cref="Border"/> template
/// part connected with <see cref="Connect"/>. Its tonal elevation is the attached
/// <see cref="TonalElevationProperty"/>; the value a surface actually renders at is
/// <see cref="GetAbsoluteTonalElevation"/>: every ancestor surface's tonal elevation plus its own,
/// exactly like <c>LocalAbsoluteTonalElevation</c>. The two axes stay independent: tonal elevation
/// only ever changes the container color, and a shadow (the control's own <c>BoxShadow</c>, for
/// example the tweened card elevation) never contributes to the overlay.
/// </para>
/// <para>
/// <see cref="MaterialAssist.ApplyTonalElevation"/> follows the pinned guard: the overlay is applied only when the
/// resolved container color is exactly <c>MaterialSurfaceColor</c> and
/// <see cref="TonalElevationEnabledProperty"/> is true, so dedicated container roles
/// (surfaceContainer, primary, …) and consumer-supplied colors keep their exact value. The tint is
/// computed once from the absolute elevation that is current at that moment; nested surfaces do not
/// stack overlays.
/// </para>
/// <para>
/// The contract is attached-state only and never rewrites the consumer's
/// <see cref="TemplatedControl.Background"/>. The connected part is updated with
/// <see cref="AvaloniaObject.SetCurrentValue{T}"/>, which keeps the template binding and any
/// consumer binding intact while the tint (or, at zero elevation, the unchanged base brush) is
/// current.
/// </para>
/// </remarks>
public sealed class MaterialSurface : AvaloniaObject
{
    private MaterialSurface() { }
    internal const string SurfaceColorKey = "MaterialSurfaceColor";
    internal const string TintColorKey = "MaterialSurfaceTintColor";

    /// <summary>
    /// The local tonal elevation in DIP, mirroring the <c>tonalElevation: Dp</c> parameter of
    /// <c>Surface.kt</c>. It participates in the tonal overlay only; it never draws a shadow.
    /// </summary>
    public static readonly AttachedProperty<double> TonalElevationProperty =
        AvaloniaProperty.RegisterAttached<MaterialSurface, Control, double>(
            "TonalElevation", 0, validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>
    /// Whether <see cref="ApplyTonalElevation"/> is applied in this subtree, mirroring
    /// <c>LocalTonalElevationEnabled</c>. Inherited; setting it to false opts a surface and all of
    /// its descendants out. Defaults to true.
    /// </summary>
    public static readonly AttachedProperty<bool> TonalElevationEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialSurface, Control, bool>(
            "TonalElevationEnabled", true, inherits: true);

    // Marks a control that renders a connected surface container, so the ancestor walk counts only
    // surfaces (as Surface.kt provides the local only from a Surface) and not every parent that
    // happens to carry a tonal value.
    private static readonly AttachedProperty<bool> IsSurfaceProperty =
        AvaloniaProperty.RegisterAttached<MaterialSurface, Control, bool>("IsSurface");

    // Trackers are keyed by their own host: an unloaded surface is collected with its control even
    // if nobody called Disconnect. Active is only a broadcast list for ancestor tone changes.
    private static readonly ConditionalWeakTable<TemplatedControl, SurfaceTracker> Trackers = new();
    private static readonly List<WeakReference<SurfaceTracker>> Active = [];

    /// <summary>Gets the local tonal elevation of <paramref name="control"/>.</summary>
    public static double GetTonalElevation(Control control) =>
        control.GetValue(TonalElevationProperty);

    /// <summary>Sets the local tonal elevation of <paramref name="control"/>.</summary>
    public static void SetTonalElevation(Control control, double value) =>
        control.SetValue(TonalElevationProperty, value);

    /// <summary>Gets whether tonal elevation is applied in the subtree of <paramref name="control"/>.</summary>
    public static bool GetTonalElevationEnabled(Control control) =>
        control.GetValue(TonalElevationEnabledProperty);

    /// <summary>Opts the subtree of <paramref name="control"/> in or out of the tonal overlay.</summary>
    public static void SetTonalElevationEnabled(Control control, bool value) =>
        control.SetValue(TonalElevationEnabledProperty, value);

    /// <summary>
    /// The absolute tonal elevation a surface renders at: the sum of every ancestor surface's
    /// <see cref="TonalElevationProperty"/> plus its own.
    /// </summary>
    /// <remarks>
    /// Like <c>LocalAbsoluteTonalElevation</c>, the value is provided whether or not tonal
    /// elevation is enabled; the opt-out flag only gates the color overlay.
    /// </remarks>
    public static double GetAbsoluteTonalElevation(Control surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var total = GetTonalElevation(surface);
        for (var parent = Parent(surface); parent is not null; parent = Parent(parent))
        {
            if (parent.GetValue(IsSurfaceProperty))
                total += GetTonalElevation(parent);
        }

        return total;
    }


    /// <summary>
    /// Connects a surface control and its container part to the tonal-elevation contract.
    /// </summary>
    /// <param name="host">The control that owns the surface.</param>
    /// <param name="part">
    /// The container <see cref="Border"/>, or null when the current template has no surface part.
    /// The host still participates in the absolute-elevation chain.
    /// </param>
    /// <remarks>
    /// The part's <see cref="Border.BackgroundProperty"/> is updated with
    /// <see cref="AvaloniaObject.SetCurrentValue{T}"/> whenever the host's background, tonal
    /// elevation, opt-out flag, theme variant or color-role resources change, so live seed and
    /// theme updates repaint an already loaded surface. Calling this again with a new part (a
    /// re-templated control) re-targets the existing tracker; <see cref="Disconnect"/> releases it.
    /// </remarks>
    internal static void Connect(TemplatedControl host, Border? part)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.SetValue(IsSurfaceProperty, true);
        if (Trackers.TryGetValue(host, out var tracker))
        {
            tracker.Attach(part);
            return;
        }

        tracker = new SurfaceTracker(host);
        Trackers.Add(host, tracker);
        Active.Add(new WeakReference<SurfaceTracker>(tracker));
        tracker.Attach(part);
    }

    /// <summary>Releases the tonal-elevation tracking of <paramref name="host"/>.</summary>
    internal static void Disconnect(TemplatedControl host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Trackers.TryGetValue(host, out var tracker))
        {
            Trackers.Remove(host);
            tracker.Dispose();
        }

        host.ClearValue(IsSurfaceProperty);
    }

    private static Control? Parent(Control control) => control is TopLevel ? null :
        control.GetVisualParent() as Control ?? (control as ILogical)?.LogicalParent as Control;

    private static Color? FindColor(Control host, string key) =>
        host.TryFindResource(key, host.ActualThemeVariant, out var value) && value is Color color
            ? color
            : null;


    private sealed class SurfaceTracker : IDisposable
    {
        private readonly TemplatedControl _host;
        private readonly SolidColorBrush _tintBrush = new(Colors.Transparent);
        private Border? _part;
        private bool _refreshing;
        private bool _disposed;

        public SurfaceTracker(TemplatedControl host)
        {
            _host = host;
            host.PropertyChanged += OnHostPropertyChanged;
            host.ResourcesChanged += OnResourcesChanged;
            host.ActualThemeVariantChanged += OnThemeVariantChanged;
            host.AttachedToVisualTree += OnAttachedToVisualTree;
        }

        public void Attach(Border? part)
        {
            _part = part;
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _host.PropertyChanged -= OnHostPropertyChanged;
            _host.ResourcesChanged -= OnResourcesChanged;
            _host.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _host.AttachedToVisualTree -= OnAttachedToVisualTree;
            _part = null;
        }

        private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == TonalElevationProperty)
            {
                // The host's own tone changes the absolute elevation of every surface below it, not
                // only its own overlay. Refreshing each live tracker recomputes the ancestor walk,
                // so the result does not depend on the refresh order.
                RefreshAll();
            }
            else if (change.Property == TonalElevationEnabledProperty ||
                     change.Property == TemplatedControl.BackgroundProperty)
            {
                Refresh();
            }
        }

        private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => Refresh();

        private void OnThemeVariantChanged(object? sender, EventArgs e) => Refresh();

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => Refresh();

        private void Refresh()
        {
            if (_disposed || _refreshing)
                return;

            _refreshing = true;
            try
            {
                Apply();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void Apply()
        {
            if (_part is not { } part)
                return;

            var background = _host.Background;
            if (TryResolveTint(background) is { } tinted)
            {
                _tintBrush.Color = tinted;
                part.SetCurrentValue(Border.BackgroundProperty, _tintBrush);
                return;
            }

            // Zero elevation, a dedicated container role or a consumer color: the part keeps the
            // exact base brush. Re-applying it releases a previous tint override, and because it is
            // the same value the template/consumer binding already produces, no binding changes.
            part.SetCurrentValue(Border.BackgroundProperty, background);
        }

        private Color? TryResolveTint(IBrush? background)
        {
            if (background is not ISolidColorBrush { Opacity: 1 } solid || !GetTonalElevationEnabled(_host))
                return null;

            var surface = FindColor(_host, SurfaceColorKey);
            var tint = FindColor(_host, TintColorKey);
            if (surface is null || tint is null || solid.Color != surface.Value)
                return null;

            var absolute = GetAbsoluteTonalElevation(_host);
            return absolute > 0 ? MaterialAssist.SurfaceColorAtElevation(surface.Value, tint.Value, absolute) : null;
        }

        private static void RefreshAll()
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                if (!Active[i].TryGetTarget(out var tracker))
                {
                    Active.RemoveAt(i);
                    continue;
                }

                tracker.Refresh();
            }
        }
    }
}
