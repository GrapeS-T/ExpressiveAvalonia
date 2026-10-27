using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The two badge sizes from the Material badge specification.</summary>
public enum MaterialBadgeSize
{
    /// <summary>A six unit dot with no label.</summary>
    Small,

    /// <summary>A sixteen unit pill carrying <see cref="MaterialBadge.Text"/>.</summary>
    Large
}

/// <summary>
/// A Material count badge: a small dot or a large labeled pill.
/// </summary>
/// <remarks>
/// <para>
/// Shape and size are selected from the presence of content:
/// no content is a 6 dp dot in the error role; content is a
/// 16 dp full-corner pill with 4 dp leading/trailing padding and a label-small label in the
/// on-error role. <see cref="Size"/> is a library extension for previewing both treatments; the
/// default <see cref="MaterialBadgeSize.Large"/> keeps the upstream content-driven behavior, and a
/// large badge without <see cref="Text"/> still draws the dot.
/// </para>
/// <para>
/// The badge is decorative. It is not focusable, ignores hit testing, and is exposed to
/// automation as raw content so screen readers announce the owning control (for example a
/// navigation destination) rather than a stray "3". Colors come from
/// <see cref="TemplatedControl.Background"/> and <see cref="TemplatedControl.Foreground"/>; the
/// default theme uses the error roles. Count formatting and overflow policy are application
/// concerns upstream, so no formatting is invented here.
/// </para>
/// </remarks>
[PseudoClasses(":small", ":large", ":dot", ":labeled")]
public class MaterialBadge : TemplatedControl
{
    /// <summary>Defines the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<MaterialBadgeSize> SizeProperty =
        AvaloniaProperty.Register<MaterialBadge, MaterialBadgeSize>(nameof(Size), MaterialBadgeSize.Large);

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialBadge, string?>(nameof(Text));

    static MaterialBadge()
    {
        SizeProperty.Changed.AddClassHandler<MaterialBadge>(static (badge, _) => badge.UpdateState());
        TextProperty.Changed.AddClassHandler<MaterialBadge>(static (badge, _) => badge.UpdateState());
    }

    public MaterialBadge()
    {
        Focusable = false;
        IsTabStop = false;
        IsHitTestVisible = false;
        UpdateState();
    }

    /// <summary>Dot or labeled pill. A large badge without text is still drawn as a dot.</summary>
    public MaterialBadgeSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>The count or short status shown by a large badge.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether the badge is currently painted as a labeled pill.</summary>
    public bool IsLabeled => Size == MaterialBadgeSize.Large && !string.IsNullOrWhiteSpace(Text);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        UpdateState();
    }

    private void UpdateState()
    {
        var labeled = IsLabeled;
        PseudoClasses.Set(":small", Size == MaterialBadgeSize.Small);
        PseudoClasses.Set(":large", Size == MaterialBadgeSize.Large);
        PseudoClasses.Set(":dot", !labeled);
        PseudoClasses.Set(":labeled", labeled);
    }
}

/// <summary>
/// Places a badge relative to anchor content.
/// </summary>
/// <remarks>
/// The box reports the anchor's size and overlays the badge inside those bounds. A dot keeps the
/// 6 dp end/top offsets; a badge with content keeps the 12 dp end and 14 dp top offsets, so a
/// content badge overhangs the anchor's end and top edges exactly like the source
/// <c>placeRelative</c> placement. The horizontal position is mirrored for right-to-left flow.
/// <para>
/// Badge placement is a pure adapter: the panel lays out the two template children directly and
/// does not alter their own layout, alignment or content.
/// </para>
/// </remarks>
public sealed class MaterialBadgeAnchorPanel : Panel
{
    private const double DotSize = 6;
    private const double DotOffset = 6;
    private const double ContentHorizontalOffset = 12;
    private const double ContentVerticalOffset = 14;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
            return default;

        var anchor = Children[0];
        anchor.Measure(availableSize);
        var anchorSize = anchor.DesiredSize;

        if (Children.Count > 1)
            Children[1].Measure(new Size(availableSize.Width, double.PositiveInfinity));

        // Compose lays the box out at the anchor's size; the badge may not enlarge it.
        return anchorSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
            return finalSize;

        var anchor = Children[0];
        var anchorSize = anchor.DesiredSize;
        anchor.Arrange(new Rect(anchorSize));

        if (Children.Count > 1)
        {
            var badge = Children[1];
            var badgeSize = badge.DesiredSize;
            if (badgeSize.Width > 0 || badgeSize.Height > 0)
            {
                // Source: hasContent = badge width > BadgeTokens.Size (the 6 DIP dot).
                var hasContent = badgeSize.Width > DotSize;
                var horizontalOffset = hasContent ? ContentHorizontalOffset : DotOffset;
                var verticalOffset = hasContent ? ContentVerticalOffset : DotOffset;

                // Source places the badge end horizontalOffset from the anchor's top-end corner
                // and overlaps the anchor by verticalOffset; the badge therefore overhangs the
                // anchor's end/top edges instead of being clamped inside it.
                var x = anchorSize.Width - horizontalOffset;
                var y = verticalOffset - badgeSize.Height;

                // Compose uses placeRelative, which mirrors the horizontal position in RTL.
                if (FlowDirection == FlowDirection.RightToLeft)
                    x = anchorSize.Width - x - badgeSize.Width;

                badge.Arrange(new Rect(new Point(x, y), badgeSize));
            }
            else
            {
                badge.Arrange(default);
            }
        }

        return anchorSize;
    }
}

/// <summary>Anchors a <see cref="MaterialBadge"/> to content using the upstream BadgedBox rules.</summary>
public sealed class MaterialBadgedBox : ContentControl
{
    /// <summary>Defines the <see cref="Badge"/> property.</summary>
    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<MaterialBadgedBox, object?>(nameof(Badge));

    /// <summary>Defines the <see cref="BadgeTemplate"/> property.</summary>
    public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> BadgeTemplateProperty =
        AvaloniaProperty.Register<MaterialBadgedBox, Avalonia.Controls.Templates.IDataTemplate?>(nameof(BadgeTemplate));

    /// <summary>Gets or sets the badge content shown over the anchor content.</summary>
    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>Gets or sets the template used to present <see cref="Badge"/>.</summary>
    public Avalonia.Controls.Templates.IDataTemplate? BadgeTemplate
    {
        get => GetValue(BadgeTemplateProperty);
        set => SetValue(BadgeTemplateProperty, value);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The upstream BadgedBox adds no semantics of its own: assistive technology sees the anchor
    /// content unchanged. Returning no peer keeps the anchor's automation node intact instead of
    /// inserting a group node around it.
    /// </remarks>
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}