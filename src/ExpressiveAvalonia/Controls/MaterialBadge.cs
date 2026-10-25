using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

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
/// The badge is decorative. It is not focusable, ignores hit testing, and is exposed to
/// automation as raw content so screen readers announce the owning control (for example a
/// navigation destination) rather than a stray "3". A <see cref="MaterialBadgeSize.Large"/>
/// badge without text degrades to the dot, which is the specified treatment for an
/// uncounted notification. Colors come from <see cref="TemplatedControl.Background"/> and
/// <see cref="TemplatedControl.Foreground"/>; the default theme uses the error roles.
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
