using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// An XR navigation bar: a rounded surface container of destinations with single selection.
/// </summary>
/// <remarks>
/// <para>
/// Desktop composition of the pinned <c>SpatialNavigationBar</c> and
/// <c>SpatialShortNavigationBar</c>. The standard variant keeps the source's 80 dp minimum height,
/// 8 dp horizontal padding and 50% container shape (40 radius on 80 height); the short variant
/// keeps the same 80 dp container but uses the short bar's 32 dp leading/trailing padding and
/// 32 dp gap between destinations. Both drop the spatial bottom-center Orbiter and its 24 dp
/// offset.
/// </para>
/// <para>
/// The control is a <see cref="ListBox"/> with an <see cref="XrNavigationBarItem"/> container,
/// so selection, SelectionItemPattern, disabled items, and pointer activation are native.
/// Left/Right, Home/End, and Enter/Space select and focus destinations. Items may be item
/// controls or strings (used as the label). The segment axis is expressed by the item count.
/// </para>
/// <para>
/// The local design source has no short-bar Figma package; the short geometry comes from the
/// pinned <c>ShortNavigationBar.kt</c> tokens only, and reuses the same destination container so
/// no second item anatomy is introduced.
/// </para>
/// </remarks>
[PseudoClasses(":short")]
public class XrNavigationBar : ListBox
{
    /// <summary>The horizontal padding and item gap of the short source bar, from <c>XrShortNavigationBarTokens.PaddingAroundItems</c>.</summary>
    public const double ShortPadding = 32;

    /// <summary>The leading/trailing and inter-item padding of the standard source bar, from <c>XrNavigationBarTokens.HorizontalPadding</c>.</summary>
    public const double StandardPadding = 8;

    /// <summary>Which pinned XR navigation bar family this control composes.</summary>
    public static readonly StyledProperty<XrNavigationBarVariant> VariantProperty =
        AvaloniaProperty.Register<XrNavigationBar, XrNavigationBarVariant>(nameof(Variant));

    static XrNavigationBar()
    {
        VariantProperty.Changed.AddClassHandler<XrNavigationBar>((bar, _) => bar.UpdateVariant());
    }

    public XrNavigationBar()
    {
        SelectionMode = SelectionMode.Single;
        UpdateVariant();
    }

    /// <summary>Standard is <c>SpatialNavigationBar</c>; Short is <c>SpatialShortNavigationBar</c>.</summary>
    public XrNavigationBarVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new XrNavigationBarItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is XrNavigationBarItem destination && destination.Label is null && item is string text)
            destination.Label = text;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!XrNavigationKeyboard.TryHandle(this, e, vertical: false))
            base.OnKeyDown(e);
    }

    private void UpdateVariant()
    {
        var shortBar = Variant == XrNavigationBarVariant.Short;
        PseudoClasses.Set(":short", shortBar);
        var spacing = shortBar ? ShortPadding : 0;
        // The source spaces the short bar's Row by 32 and leaves the standard Row with no
        // Arrangement spacing. The item panel is recreated because Spacing is structural.
        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing
        });
    }
}
