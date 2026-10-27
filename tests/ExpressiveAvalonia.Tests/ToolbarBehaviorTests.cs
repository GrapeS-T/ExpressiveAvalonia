using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class ToolbarBehaviorTests
{
    /// <summary>
    /// VerticalFloatingToolbarLayout is a Column: leading, content, trailing. The Avalonia
    /// template used to place the trailing slot first and the leading slot last, which only
    /// showed up once the slots had unequal intrinsic sizes.
    /// </summary>
    [AvaloniaFact]
    public void VerticalSlotsArrangeLeadingThenContentThenTrailing()
    {
        var layout = new MaterialFloatingToolbarLayout
        {
            Orientation = Orientation.Vertical,
            ExpandedProgress = 1,
        };
        var leading = new Border { Name = "PART_Leading", Height = 24 };
        var content = new Border { Name = "PART_Content", Height = 80 };
        var trailing = new Border { Name = "PART_Trailing", Height = 40 };
        layout.Children.Add(leading);
        layout.Children.Add(content);
        layout.Children.Add(trailing);

        layout.Measure(new Size(64, 144));
        layout.Arrange(new Rect(0, 0, 64, 144));

        Assert.Equal(new Rect(0, 0, 64, 24), leading.Bounds);
        Assert.Equal(leading.Bounds.Bottom, content.Bounds.Y);
        Assert.Equal(80d, content.Bounds.Height);
        Assert.Equal(content.Bounds.Bottom, trailing.Bounds.Y);
        Assert.Equal(40d, trailing.Bounds.Height);
        Assert.Equal(144d, trailing.Bounds.Bottom);
    }

    /// <summary>
    /// A collapsed vertical leading slot must leave the tab order and automation tree, and a
    /// focused action inside it must move to the still-visible toolbar content instead of
    /// remaining focused while invisible.
    /// </summary>
    [AvaloniaFact]
    public void CollapsedVerticalSlotLeavesTheTabOrderAndRestoresFocus()
    {
        var leadingButton = new Button { Content = "Leading" };
        var contentButton = new Button { Content = "Content" };
        var leading = new Border { Name = "PART_Leading", Child = leadingButton };
        var content = new Border { Name = "PART_Content", Child = contentButton };
        var trailing = new Border { Name = "PART_Trailing", Child = new Border { Height = 16 } };
        var layout = new MaterialFloatingToolbarLayout
        {
            Orientation = Orientation.Vertical,
            ExpandedProgress = 1,
        };
        layout.Children.Add(leading);
        layout.Children.Add(content);
        layout.Children.Add(trailing);

        var window = new Window { Width = 320, Height = 260, Content = layout };
        window.Show();
        try
        {
            window.UpdateLayout();
            var nativeTabNavigation = KeyboardNavigation.GetTabNavigation(leading);
            var nativeAccessibilityView = AutomationProperties.GetAccessibilityView(leading);
            Assert.True(leadingButton.Focus());
            Assert.True(leadingButton.IsFocused);

            layout.ExpandedProgress = 0;
            window.UpdateLayout();

            Assert.Equal(0d, leading.Opacity);
            Assert.False(leading.IsHitTestVisible);
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(leading));
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(leading));
            Assert.True(contentButton.IsFocused);

            // With the slot suppressed, Tab from the visible content cannot land on it.
            Assert.True(contentButton.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.NotSame(leadingButton, window.FocusManager?.GetFocusedElement());

            layout.ExpandedProgress = 1;
            window.UpdateLayout();

            Assert.Equal(1d, leading.Opacity);
            Assert.True(leading.IsHitTestVisible);
            Assert.Equal(nativeTabNavigation, KeyboardNavigation.GetTabNavigation(leading));
            Assert.Equal(nativeAccessibilityView, AutomationProperties.GetAccessibilityView(leading));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// FloatingToolbar.kt applies <c>focusProperties { canFocus = false }</c> whenever the
    /// scroll offset is non-zero. The desktop toolbar must therefore leave the tab order and the
    /// automation content view while displaced, and restore the native values at the origin.
    /// </summary>
    [AvaloniaFact]
    public void ScrolledFloatingToolbarLeavesTheTabOrderUntilItReturnsToOrigin()
    {
        var behavior = MaterialAppBarScrollBehavior.FloatingToolbar(
            exitDirection: MaterialFloatingToolbarExitDirection.Bottom);
        var toolbar = new MaterialToolbar { ScrollBehavior = behavior, Width = 120 };
        var window = new Window
        {
            Width = 320,
            Height = 240,
            Content = new Grid { Children = { toolbar } },
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var layout = toolbar.GetVisualDescendants().OfType<Control>()
                .Single(control => control.Name == "PART_Layout");
            var nativeTabNavigation = KeyboardNavigation.GetTabNavigation(layout);
            var nativeAccessibilityView = AutomationProperties.GetAccessibilityView(layout);

            behavior.State.HeightOffsetLimit = -240;
            behavior.State.HeightOffset = -20;
            window.UpdateLayout();

            Assert.False(layout.IsHitTestVisible);
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(layout));
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(layout));

            behavior.State.HeightOffset = 0;
            window.UpdateLayout();

            Assert.True(layout.IsHitTestVisible);
            Assert.Equal(nativeTabNavigation, KeyboardNavigation.GetTabNavigation(layout));
            Assert.Equal(nativeAccessibilityView, AutomationProperties.GetAccessibilityView(layout));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// FloatingToolbarDefaults.animationSpec runs inside an updateTransition, so reversing
    /// expand/collapse retargets the running spring. Replacing it with a fresh animation would
    /// drop the accumulated velocity and stall the reversal.
    /// </summary>
    [AvaloniaFact]
    public void ReversingExpansionRetargetsTheRunningSpring()
    {
        var toolbar = new MaterialToolbar { IsExpanded = true };
        MaterialMotion.SetIsReduced(toolbar, false);
        var window = new Window { Width = 320, Height = 240, Content = toolbar };
        window.Show();
        try
        {
            toolbar.IsExpanded = false;
            var collapse = Assert.IsType<MaterialMotion.SpringAnimation>(toolbar.ExpansionAnimation);

            Thread.Sleep(5);
            var (_, collapseVelocity) = collapse.SampleState();
            Assert.True(collapseVelocity < 0, "the collapse spring needs an outgoing velocity");

            toolbar.IsExpanded = true;

            Assert.Same(collapse, toolbar.ExpansionAnimation);
            var (_, reversedVelocity) = collapse.SampleState();
            Assert.True(reversedVelocity < 0,
                "retargeting must keep the collapse velocity before the spring reverses");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The template supplies orientation-aware anchors: the leading presenter sits on the bottom
    /// of its animated band and the trailing presenter on the top, matching
    /// verticalEnterTransition's Bottom/Top expansion.
    /// </summary>
    [AvaloniaFact]
    public void VerticalTemplateKeepsLeadingAboveContentAndTrailingBelowIt()
    {
        var toolbar = new MaterialToolbar
        {
            Orientation = Orientation.Vertical,
            LeadingContent = new Border { Height = 24 },
            TrailingContent = new Border { Height = 40 },
        };
        var window = new Window { Width = 320, Height = 260, Content = toolbar };
        window.Show();
        try
        {
            window.UpdateLayout();
            var leading = toolbar.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_Leading");
            var content = toolbar.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_Content");
            var trailing = toolbar.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_Trailing");

            Assert.Equal(0d, leading.Bounds.Y);
            Assert.Equal(leading.Bounds.Bottom, content.Bounds.Y);
            Assert.Equal(content.Bounds.Bottom, trailing.Bounds.Y);
            Assert.Equal(toolbar.Bounds.Height, trailing.Bounds.Bottom);
            Assert.True(leading.Bounds.Height >= 24);
            Assert.True(trailing.Bounds.Height >= 40);
        }
        finally
        {
            window.Close();
        }
    }
}