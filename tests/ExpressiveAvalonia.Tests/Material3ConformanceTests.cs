using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Cross-family conformance regressions for Material Design 3 and Material 3 Expressive
/// specifications, focus geometry, RTL single-plane mirroring, accessibility, and localization.
/// </summary>
public class Material3ConformanceTests
{
    private static Window Show(Control control, double width = 800, double height = 500)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = control,
            UseLayoutRounding = false
        };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static Point Center(Control control, Visual root) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)!.Value;

    private static Color ColorOf(IBrush? brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color Role(Control control, string name) =>
        ColorOf((IBrush)control.FindResource(control.ActualThemeVariant, name)!);

    [AvaloniaFact]
    public void FabDefaultsToBaseline56DipAndDrawsUnclippedTwoDipFocusGap()
    {
        var fab = new MaterialFab
        {
            Content = new MaterialIcon { IconName = "add" },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(fab);
        try
        {
            Assert.Equal(MaterialFabSize.Default, fab.Size);
            Assert.Equal(56, fab.Bounds.Width, 3);
            Assert.Equal(56, fab.Bounds.Height, 3);

            fab.Focus(NavigationMethod.Tab);
            window.UpdateLayout();
            var ring = Part<Border>(fab, "FocusRing");
            var surface = Part<Border>(fab, "Surface");
            Assert.True(ring.IsVisible);
            Assert.False(fab.ClipToBounds);
            Assert.Equal(2, surface.TranslatePoint(default, fab)!.Value.X
                - ring.TranslatePoint(default, fab)!.Value.X - ring.BorderThickness.Left, 3);
            Assert.Equal(surface.CornerRadius.TopLeft + 5, ring.CornerRadius.TopLeft, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void IconlessExpressiveExtendedFabHasSymmetricPaddingAndDoesNotCollapseBlank()
    {
        var fab = new MaterialExtendedFab
        {
            Label = "Compose",
            Mode = MaterialExtendedFabMode.Expressive,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(fab);
        try
        {
            var label = Part<TextBlock>(fab, "PART_Label");
            var leftPad = label.TranslatePoint(default, fab)!.Value.X;
            var rightPad = fab.Bounds.Width - label.TranslatePoint(new Point(label.Bounds.Width, 0), fab)!.Value.X;
            Assert.Equal(leftPad, rightPad, 3);

            var expandedWidth = fab.Bounds.Width;
            fab.IsExpanded = false;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(expandedWidth, fab.Bounds.Width, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FabMenuExposesExpandCollapseAndPlacesTriggerBeforeItemsInTabOrder()
    {
        var first = new MaterialFabMenuSegment { Label = "First" };
        var second = new MaterialFabMenuSegment { Label = "Second" };
        var menu = new MaterialFabMenu { Items = { first, second } };
        MaterialMotion.SetIsReduced(menu, true);
        var window = Show(menu);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(menu);
            var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);
            Assert.Equal(ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);

            expandCollapse.Expand();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(menu.IsExpanded);
            Assert.Equal(ExpandCollapseState.Expanded, expandCollapse.ExpandCollapseState);

            var trigger = menu.GetVisualDescendants().OfType<MaterialFab>().Single();
            Assert.True(trigger.TabIndex < menu.GetVisualDescendants().OfType<Control>()
                .First(c => c.Name == "PART_MenuHost").TabIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SplitButtonAndRemovableChipAndSwitchMirrorOnceInRtl()
    {
        var split = new MaterialSplitButton { Content = "Send", HorizontalAlignment = HorizontalAlignment.Center };
        var chip = new MaterialChip
        {
            Kind = MaterialChipKind.Input,
            Content = "Tag",
            IsRemovable = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var toggle = new ToggleSwitch { Content = "Wi-Fi", HorizontalAlignment = HorizontalAlignment.Center };
        var stack = new StackPanel
        {
            Spacing = 16,
            FlowDirection = FlowDirection.RightToLeft,
            Children = { split, chip, toggle }
        };
        var window = Show(stack);
        try
        {
            var primary = Part<Button>(split, "PART_PrimaryButton");
            var trailing = Part<Button>(split, "PART_TrailingButton");
            Assert.True(Center(primary, window).X > Center(trailing, window).X,
                "Primary split-button half must sit on the physical right in RTL.");

            var remove = Part<Button>(chip, "PART_RemoveButton");
            Assert.True(Center(remove, window).X < Center(chip, window).X,
                "Input chip remove target must sit on the physical left (trailing edge) in RTL.");

            var track = Part<Panel>(toggle, "SwitchTrack");
            var label = Part<Control>(toggle, "PART_ContentPresenter");
            Assert.True(Center(track, window).X > Center(label, window).X,
                "Switch track must sit on the physical right of its label in RTL.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ActionChipsExposePureButtonAutomationAndElevatedChipsKeepUnclippedShadows()
    {
        var assist = new MaterialChip
        {
            Kind = MaterialChipKind.Assist,
            Variant = MaterialChipVariant.Elevated,
            Content = "Assist"
        };
        var filter = new MaterialChip { Kind = MaterialChipKind.Filter, Content = "Filter" };
        var window = Show(new StackPanel { Children = { assist, filter } });
        try
        {
            var assistPeer = ControlAutomationPeer.CreatePeerForElement(assist);
            Assert.Equal(AutomationControlType.Button, assistPeer.GetAutomationControlType());
            Assert.Null(assistPeer.GetProvider<IToggleProvider>());
            Assert.Null(assistPeer.GetProvider<ISelectionItemProvider>());

            var filterPeer = ControlAutomationPeer.CreatePeerForElement(filter);
            Assert.NotNull(filterPeer.GetProvider<IToggleProvider>());

            var surface = Part<Border>(assist, "Surface");
            Assert.False(surface.ClipToBounds);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SegmentedButtonsPreserve48DipHitTargets()
    {
        var first = new MaterialButtonGroupItem { Content = "Day" };
        var second = new MaterialButtonGroupItem { Content = "Week" };
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Segmented,
            Items = { first, second },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(group);
        try
        {
            Assert.True(first.Bounds.Height >= 48);
            Assert.True(second.Bounds.Height >= 48);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OutlinedInsideLabelTextFieldRetainsTopClearanceAndFilledDisabledUsesTokenContainer()
    {
        var outlined = new TextBox { Text = "Value" };
        MaterialTextField.SetVariant(outlined, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(outlined, "Label");
        MaterialTextField.SetLabelPosition(outlined, MaterialTextFieldLabelPosition.Inside);

        var filledDisabled = new TextBox { Text = "Read", IsEnabled = false };
        MaterialTextField.SetVariant(filledDisabled, MaterialTextFieldVariant.Filled);

        var window = Show(new StackPanel { Spacing = 16, Children = { outlined, filledDisabled } });
        try
        {
            Assert.Equal(24, outlined.Padding.Top, 3);
            Assert.Equal(8, outlined.Padding.Bottom, 3);

            var expected = (IBrush)filledDisabled.FindResource(
                filledDisabled.ActualThemeVariant, "MaterialTextFieldDisabledContainerBrush")!;
            Assert.Equal(ColorOf(expected), ColorOf(filledDisabled.Background));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SearchBarClickRequestsExpansionAndPreservesCallerAutomationName()
    {
        var bar = new MaterialSearchBar { PlaceholderText = "Search mail", Width = 400 };
        AutomationProperties.SetName(bar, "Custom mail search");
        var expanded = 0;
        bar.ExpansionRequested += (_, _) => expanded++;
        var window = Show(bar);
        try
        {
            var input = Part<TextBox>(bar, "PART_TextBox");
            Assert.Equal("Custom mail search", AutomationProperties.GetName(input));

            var pt = Center(input, window);
            window.MouseDown(pt, MouseButton.Left);
            window.MouseUp(pt, MouseButton.Left);
            Assert.Equal(1, expanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToolbarToggleActionsApplyStandardAndVibrantSelectionColorsAndFloatingShadow()
    {
        var toggleAction = new MaterialBarAction
        {
            Text = "Bold",
            Icon = Geometry.Parse("M 4,4 L 20,4 L 20,20 L 4,20 Z"),
            Kind = MaterialBarActionKind.Toggleable,
            IsChecked = true
        };
        var toolbar = new MaterialToolbar
        {
            Configuration = MaterialToolbarConfiguration.Floating,
            Color = MaterialToolbarColor.Standard
        };
        toolbar.Actions.Add(toggleAction);
        MaterialMotion.SetIsReduced(toolbar, true);
        var window = Show(toolbar);
        try
        {
            var background = Part<Border>(toolbar, "PART_Background");
            Assert.False(background.ClipToBounds);
            Assert.Equal(toolbar.FindResource(toolbar.ActualThemeVariant, "MaterialElevationLevel3Shadow"),
                background.BoxShadow);

            var toggleBtn = toolbar.GetVisualDescendants().OfType<ToggleButton>().First();
            Assert.Equal(Role(toolbar, "MaterialOnSecondaryContainerBrush"), ColorOf(toggleBtn.Foreground));

            toolbar.Color = MaterialToolbarColor.Vibrant;
            window.UpdateLayout();
            Assert.Equal(Role(toolbar, "MaterialOnSurfaceBrush"), ColorOf(toggleBtn.Foreground));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClassicAndShortNavigationBarsUseDistinctIndicatorWidthsAndMirrorOnceInRtl()
    {
        var first = new MaterialNavigationBarItem { Content = "Home", Icon = Geometry.Parse("M4,4L20,4L20,20Z") };
        var second = new MaterialNavigationBarItem { Content = "Saved", Icon = Geometry.Parse("M4,4L20,4L20,20Z") };
        var bar = new MaterialNavigationBar
        {
            Kind = MaterialNavigationBarKind.Classic,
            Width = 400,
            Items = { first, second }
        };
        bar.SelectedIndex = 0;
        MaterialMotion.SetIsReduced(bar, true);
        var window = Show(bar);
        try
        {
            var classicIndicator = Part<Border>(first, "PART_Indicator");
            Assert.Equal(64, classicIndicator.Bounds.Width, 3);

            bar.Kind = MaterialNavigationBarKind.Short;
            window.UpdateLayout();
            Assert.Equal(56, classicIndicator.Bounds.Width, 3);

            bar.FlowDirection = FlowDirection.RightToLeft;
            window.UpdateLayout();
            Assert.True(Center(first, window).X > Center(second, window).X,
                "First navigation bar destination must sit on the physical right in RTL.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavigationDrawerDistinguishesStandardAndModalSurfacesAndTabsUseRovingTabStop()
    {
        var drawer = new MaterialNavigationDrawer { Variant = MaterialNavigationDrawerVariant.Standard };
        var tab1 = new TabItem { Header = "One", Content = "Page 1" };
        var tab2 = new TabItem { Header = "Two", Content = "Page 2" };
        var tabs = new TabControl { Items = { tab1, tab2 }, SelectedIndex = 0 };
        var window = Show(new StackPanel { Children = { drawer, tabs } }, width: 900, height: 600);
        try
        {
            Assert.Equal(Role(drawer, "MaterialSurfaceBrush"), ColorOf(drawer.Background));
            Assert.Equal(new CornerRadius(0), drawer.CornerRadius);

            drawer.Variant = MaterialNavigationDrawerVariant.Modal;
            window.UpdateLayout();
            Assert.Equal(Role(drawer, "MaterialSurfaceContainerLowBrush"), ColorOf(drawer.Background));
            Assert.Equal(new CornerRadius(0, 16, 16, 0), drawer.CornerRadius);

            Assert.True(KeyboardNavigation.GetIsTabStop(tab1));
            Assert.False(KeyboardNavigation.GetIsTabStop(tab2));

            var indicator = Part<Border>(tabs, "PART_Indicator");
            Assert.Equal(new CornerRadius(3, 3, 0, 0), indicator.CornerRadius);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CarouselClipsMaskedItemsWithRoundedShapeAndSegmentedListSuppressesDividers()
    {
        var carousel = new MaterialCarousel
        {
            Layout = MaterialCarouselLayout.Hero,
            Width = 400,
            Height = 200,
            PreferredItemWidth = 180
        };
        MaterialMotion.SetIsReduced(carousel, false);
        for (var i = 0; i < 6; i++)
            carousel.Items.Add($"Slide {i}");

        var row1 = new ListBoxItem();
        MaterialList.SetHeadline(row1, "First");
        var row2 = new ListBoxItem();
        MaterialList.SetHeadline(row2, "Second");
        var list = new ListBox { Width = 320, Items = { row1, row2 } };
        MaterialList.SetSegmented(list, true);
        MaterialList.SetShowDividers(list, true);

        var window = Show(new StackPanel { Spacing = 16, Children = { carousel, list } }, width: 800, height: 600);
        try
        {
            var clippedItem = carousel.GetVisualDescendants().OfType<MaterialCarouselItem>()
                .First(item => item.MaskRect.Width > 0 && item.MaskRect.Width < item.Bounds.Width - 1);
            var clip = Assert.IsType<RectangleGeometry>(clippedItem.Clip);
            Assert.True(clip.RadiusX > 0, "Masked carousel item clip must preserve rounded corners.");

            Assert.Equal(0, row2.Margin.Bottom);
            var divider = Part<Border>(row1, "Divider");
            Assert.False(divider.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TextOnlyMenuItemCollapsesEmptyIconGutterAndStandardSheetDoesNotHijackModalOverlay()
    {
        var menuItem = new MenuItem { Header = "Copy" };
        var dialog = new MaterialDialog { Title = "Confirm", SupportingText = "Proceed?" };
        var scaffold = new MaterialBottomSheetScaffold
        {
            Width = 500,
            Height = 400,
            SheetContent = new TextBlock { Text = "Persistent sheet" },
            Content = menuItem
        };
        var window = Show(scaffold);
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var leading = Part<Panel>(menuItem, "LeadingHost");
            Assert.False(leading.IsVisible);

            var dialogTask = dialog.ShowAsync(window);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(dialogTask.IsCompleted);

            // Re-attaching or restoring the persistent non-modal sheet must not hijack Escape from the modal dialog.
            window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(dialogTask.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SingleLineAutoSnackbarStays48DipAndProgressPeersResolveLocalizedStatus()
    {
        var snackbar = new MaterialSnackbar
        {
            Width = 344,
            Text = "Saved",
            ActionContent = "Undo"
        };
        var loading = new LoadingIndicator { IsIndeterminate = true };
        var circular = new CircularProgressIndicator { IsIndeterminate = true };
        var window = Show(new StackPanel { Children = { snackbar, loading, circular } });
        window.Resources["MaterialLoadingIndicatorText"] = "Загрузка";
        window.Resources["MaterialProgressIndeterminateText"] = "Выполняется";
        try
        {
            snackbar.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var container = Part<Border>(snackbar, "PART_Container");
            Assert.Equal(48, container.Bounds.Height, 3);

            var loadingPeer = ControlAutomationPeer.CreatePeerForElement(loading);
            Assert.Equal("Загрузка", loadingPeer.GetName());
            Assert.Equal("Выполняется", loadingPeer.GetItemStatus());

            var circularPeer = ControlAutomationPeer.CreatePeerForElement(circular);
            Assert.Equal("Выполняется", circularPeer.GetItemStatus());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DatePickerFieldUsesOutlinedInputAndFormatsOutOfRangeValidationError()
    {
        var field = new MaterialDatePickerField
        {
            MinimumDate = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero),
            MaximumDate = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero),
            DateFormat = "yyyy-MM-dd"
        };
        var window = Show(field);
        try
        {
            var input = Part<TextBox>(field, "PART_Input");
            Assert.Equal(MaterialTextFieldVariant.Outlined, MaterialTextField.GetVariant(input));

            input.Focus();
            input.Text = "2026-05-01";
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            window.UpdateLayout();

            Assert.NotNull(field.ValidationMessage);
            Assert.DoesNotContain("{0}", field.ValidationMessage);
            Assert.DoesNotContain("{1}", field.ValidationMessage);
            Assert.True(DataValidationErrors.GetHasErrors(input));
        }
        finally { window.Close(); }
    }
}
