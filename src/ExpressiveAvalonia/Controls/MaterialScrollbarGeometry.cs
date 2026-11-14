using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Pure geometry for the passive Material scrollbar, ported from the drawing block of
/// <c>NonInteractiveScrollbarNode.ContentDrawScope.draw</c>.
/// </summary>
/// <remarks>
/// The source draws in pixels from a node size and an integer
/// <c>ScrollIndicatorState</c>. Avalonia exposes the same quantities as device-independent
/// units, so this adapter keeps the ratios and insets without rounding to integer pixels.
/// </remarks>
internal static class MaterialScrollbarGeometry
{
    /// <summary>
    /// Calculates the track and thumb rectangles. Returns false when the source draw method
    /// would return before drawing either shape.
    /// </summary>
    /// <param name="size">Size of the decorated scrollable node.</param>
    /// <param name="orientation">Scrollbar orientation.</param>
    /// <param name="flowDirection">Layout direction used for the end edge and RTL thumb.</param>
    /// <param name="scrollOffset">Absolute offset from the visual start of the content.</param>
    /// <param name="contentSize">Total content length on the scroll axis.</param>
    /// <param name="viewportSize">Visible content length on the scroll axis.</param>
    /// <param name="thickness">Scrollbar thickness.</param>
    /// <param name="thumbMinLength">Minimum thumb length.</param>
    /// <param name="thumbMaxLengthFraction">Maximum thumb length as a fraction of the track.</param>
    /// <param name="mainAxisTrackInset">Inset at both ends of the track.</param>
    /// <param name="crossAxisTrackInset">Inset from the end edge of the node.</param>
    /// <param name="track">Track rectangle.</param>
    /// <param name="thumb">Thumb rectangle.</param>
    internal static bool TryCalculate(
        Size size,
        Orientation orientation,
        FlowDirection flowDirection,
        double scrollOffset,
        double contentSize,
        double viewportSize,
        double thickness,
        double thumbMinLength,
        double thumbMaxLengthFraction,
        double mainAxisTrackInset,
        double crossAxisTrackInset,
        out Rect track,
        out Rect thumb)
    {
        track = default;
        thumb = default;

        // Source: contentSize <= viewportSize || viewportSize == 0f || contentSize == 0f
        if (contentSize <= viewportSize || viewportSize == 0 || contentSize == 0)
            return false;

        var vertical = orientation == Orientation.Vertical;
        var mainSize = vertical ? size.Height : size.Width;
        var crossSize = vertical ? size.Width : size.Height;
        var trackLength = mainSize - 2 * mainAxisTrackInset;

        // Source: trackLengthPx <= 0f || trackLengthPx < minThumbLengthPx
        if (trackLength <= 0 || trackLength < thumbMinLength)
            return false;

        var maxThumbLength = trackLength * thumbMaxLengthFraction;
        var thumbLengthFraction = viewportSize / contentSize;
        // Upstream coerceIn throws when a tiny track makes max < min. Keep valid layout
        // sizes drawable by lowering the minimum to the available maximum in that case.
        var thumbLength = Math.Clamp(
            trackLength * thumbLengthFraction, Math.Min(thumbMinLength, maxThumbLength), maxThumbLength);

        var maxOffset = contentSize - viewportSize;
        var thumbOffset = maxOffset > 0
            ? scrollOffset / maxOffset * (trackLength - thumbLength)
            : 0;

        if (vertical)
        {
            var trackX = flowDirection == FlowDirection.LeftToRight
                ? crossSize - crossAxisTrackInset - thickness
                : crossAxisTrackInset;
            track = new Rect(trackX, mainAxisTrackInset, thickness, trackLength);
            thumb = new Rect(
                trackX, mainAxisTrackInset + thumbOffset, thickness, thumbLength);
        }
        else
        {
            var trackY = crossSize - crossAxisTrackInset - thickness;
            track = new Rect(mainAxisTrackInset, trackY, trackLength, thickness);
            var thumbX = flowDirection == FlowDirection.LeftToRight
                ? mainAxisTrackInset + thumbOffset
                : mainSize - mainAxisTrackInset - thumbLength - thumbOffset;
            thumb = new Rect(thumbX, trackY, thumbLength, thickness);
        }

        return true;
    }
}