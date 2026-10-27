using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(ExpressiveAvalonia.Tests.TestApp))]
namespace ExpressiveAvalonia.Tests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    public override void Initialize() { Styles.Add(new MaterialTheme { IsReducedMotion = true }); }
}

public class ControlBehaviorTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Press(TopLevel window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static Avalonia.Media.Color Role(Control control, string key) =>
        control.FindResource(control.ActualThemeVariant, key) switch
        {
            Avalonia.Media.Color color => color,
            Avalonia.Media.ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException($"Resource '{key}' is not a color")
        };

    private static Avalonia.Media.Color BrushColor(Avalonia.Media.IBrush? brush) =>
        brush switch
        {
            Avalonia.Media.ISolidColorBrush solid => solid.Color,
            _ => throw new InvalidOperationException("Expected a solid brush")
        };

    [AvaloniaFact]
    public void TransitionDestinationAcceptsPointerInitiallyAndAfterReattach()
    {
        var clicks = 0;
        var destination = new Button { Content = "Destination" };
        destination.Click += (_, _) => clicks++;
        var host = new MaterialTransitionHost { Content = destination };
        var root = new Grid { Children = { host } };
        var window = Show(root);
        try
        {
            ClickDestination();
            Assert.Equal(1, clicks);
            root.Children.Remove(host);
            root.Children.Add(host);
            window.UpdateLayout();
            ClickDestination();
            Assert.Equal(2, clicks);
        }
        finally { window.Close(); }

        void ClickDestination()
        {
            var point = destination.TranslatePoint(
                new Point(destination.Bounds.Width / 2, destination.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
    }

    [AvaloniaFact]
    public void TopRailItemsFollowHeaderWithinRailBounds()
    {
        var rail = new MaterialNavigationRail { ShowMenuButton = true, ShowFab = true };
        var item = new MaterialNavigationRailItem { Content = "Destination" };
        rail.Items.Add(item);
        var window = Show(rail);
        try
        {
            window.UpdateLayout();
            var header = rail.GetVisualDescendants().OfType<StackPanel>()
                .Single(panel => panel.Name == "PART_HeaderHost");
            var position = item.TranslatePoint(new Point(), rail)!.Value;
            Assert.True(header.Bounds.Height < rail.Bounds.Height);
            Assert.True(position.Y >= header.Bounds.Bottom);
            Assert.True(position.Y + item.Bounds.Height <= rail.Bounds.Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WideRailLabelRemainsVisibleCollapsedAndKeepsSourceExpandedGap()
    {
        var rail = new MaterialNavigationRail
        {
            Mode = MaterialNavigationRailMode.Wide,
            ShowFab = false,
            ShowMenuButton = false,
            IsExpanded = false
        };
        var item = new MaterialNavigationRailItem { Content = "Destination" };
        rail.Items.Add(item);
        var window = Show(rail);
        try
        {
            var layout = item.GetVisualDescendants().OfType<MaterialNavigationItemLayout>().Single();
            Assert.Equal(1, layout.Children[3].Opacity);
            rail.IsExpanded = true;
            window.UpdateLayout();
            Assert.Equal(8, layout.Children[3].Bounds.X - layout.Children[2].Bounds.Right, 5);
            rail.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
            window.UpdateLayout();
            Assert.Equal(8, layout.Children[2].Bounds.X - layout.Children[3].Bounds.Right, 5);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WideRailUpdatesLabelAndAccessibleNameWithoutReselecting()
    {
        var item = new MaterialNavigationRailItem { Label = "Inbox" };
        var rail = new MaterialNavigationRail { Mode = MaterialNavigationRailMode.Wide, IsExpanded = true, ShowFab = false, ShowMenuButton = false };
        rail.Items.Add(item);
        var window = Show(rail);
        try
        {
            var layout = item.GetVisualDescendants().OfType<MaterialNavigationItemLayout>().Single();
            Assert.Equal("Inbox", Avalonia.Automation.AutomationProperties.GetName(item));
            Assert.True(layout.HasLabel);
            item.ShowLabel = false;
            Assert.False(layout.HasLabel);
            item.Label = "Archive";
            item.ShowLabel = true;
            Assert.True(layout.HasLabel);
            Assert.Equal("Archive", Avalonia.Automation.AutomationProperties.GetName(item));
            Avalonia.Automation.AutomationProperties.SetName(item, "Custom name");
            item.Label = "Recent";
            Assert.Equal("Custom name", Avalonia.Automation.AutomationProperties.GetName(item));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WideRailNotifiesWidthAndProgressWhenExpanded()
    {
        var rail = new MaterialNavigationRail { Mode = MaterialNavigationRailMode.Wide, IsExpanded = false, ShowFab = false, ShowMenuButton = false };
        rail.Items.Add(new MaterialNavigationRailItem { Content = "Home" });
        var window = Show(rail);
        try
        {
            var widthChanged = false;
            var progressChanged = false;
            rail.PropertyChanged += (_, e) =>
            {
                widthChanged |= e.Property == MaterialNavigationRail.ExpansionWidthProperty;
                progressChanged |= e.Property == MaterialNavigationRail.ExpansionProgressProperty;
            };
            rail.IsExpanded = true;
            window.UpdateLayout();
            Assert.True(widthChanged);
            Assert.True(progressChanged);
            Assert.True(rail.ExpansionWidth >= 220);
            Assert.Equal(1, rail.ExpansionProgress);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WideRailWrapsLongLabelsWithinTheAvailableWidth()
    {
        var item = new MaterialNavigationRailItem { Label = "A long navigation destination with multiple words", IsSelected = true };
        var rail = new MaterialNavigationRail { Mode = MaterialNavigationRailMode.Wide, IsExpanded = true, ShowFab = false, ShowMenuButton = false };
        rail.Items.Add(item);
        var window = Show(rail);
        try
        {
            window.UpdateLayout();
            var layout = item.GetVisualDescendants().OfType<MaterialNavigationItemLayout>().Single();
            var label = layout.Children[3];
            var text = label.GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.True(label.Bounds.X >= 0 && label.Bounds.Right <= layout.Bounds.Width);
            Assert.True(text.Bounds.Height > text.LineHeight);
            Assert.True(item.Bounds.Height > 56);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void IconUsesContextualTintAndOnlyOwningComponentDimsDisabledContent()
    {
        var icon = MaterialIcon.For(MaterialIcons.All[0].SourceId);
        var button = new Button { Content = icon };
        var window = Show(button);
        try
        {
            var glyph = icon.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
            Assert.Equal(button.Foreground, glyph.Fill);
            button.IsEnabled = false;
            window.UpdateLayout();
            Assert.Equal(button.Foreground, glyph.Fill);
            Assert.Equal(1, glyph.Opacity);
            icon.Foreground = Avalonia.Media.Brushes.Red;
            Assert.Equal(Avalonia.Media.Brushes.Red, glyph.Fill);
            icon.Foreground = null;
            Assert.Equal(Avalonia.Media.Color.Parse("#1D1B20"),
                Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(glyph.Fill).Color);
        }
        finally { window.Close(); }
    }

    private sealed class Skip2027 : MaterialSelectableDates
    {
        public override bool IsSelectableYear(int year) => year != 2027;
    }

    [AvaloniaFact]
    public void DateNavigationCanBrowseUnselectableYearAndRefreshesWeekdayTheme()
    {
        var picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            MinimumYear = 2026,
            MaximumYear = 2028,
            DisplayDate = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            SelectableDates = new Skip2027()
        };
        var window = Show(picker);
        try
        {
            var next = picker.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_NextButton");
            Assert.True(next.IsEnabled);
            next.Focus();
            Press(window, Key.Space);
            Assert.Equal(2027, picker.DisplayDate?.Year);
            var dayGrid = picker.GetVisualDescendants().OfType<UniformGrid>().Single(x => x.Name == "PART_DayGrid");
            Assert.All(dayGrid.Children.OfType<MaterialDateCell>(), day => Assert.False(day.IsEnabled));
            var weekdays = picker.GetVisualDescendants().OfType<UniformGrid>().Single(x => x.Name == "PART_WeekdayRow");
            var weekday = Assert.IsType<TextBlock>(weekdays.Children[0]);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(picker.FindResource(ThemeVariant.Dark, "MaterialOnSurfaceBrush"), weekday.Foreground);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CarouselAspectSlotsFollowDefaultsLiveRatioAndSameCountReplacement()
    {
        var carousel = new MaterialCarousel
        {
            Width = 500, ItemHeight = 150, Layout = MaterialCarouselLayout.MultiAspectRatio,
            ItemsSource = new System.Collections.ObjectModel.ObservableCollection<object> { "first", "second", "third" }
        };
        var window = Show(carousel);
        try
        {
            var first = (MaterialCarouselItem)carousel.ContainerFromIndex(0)!;
            var second = (MaterialCarouselItem)carousel.ContainerFromIndex(1)!;
            Assert.Equal(16d / 9, first.Bounds.Width / first.Bounds.Height, 2);
            Assert.Equal(4d / 3, second.Bounds.Width / second.Bounds.Height, 2);
            first.AspectRatio = 2;
            window.UpdateLayout();
            Assert.Equal(2, first.Bounds.Width / first.Bounds.Height, 2);
            var replacement = new MaterialCarouselItem { Content = "replacement", AspectRatio = .5 };
            ((System.Collections.ObjectModel.ObservableCollection<object>)carousel.ItemsSource!)[0] = replacement;
            window.UpdateLayout();
            Assert.Equal(.5, replacement.Bounds.Width / replacement.Bounds.Height, 2);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimePickerProgrammaticSynchronizationDoesNotAdvanceHour()
    {
        var picker = new MaterialTimePicker { Time = new TimeSpan(1, 59, 0) };
        var window = Show(picker);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var dial = picker.GetVisualDescendants().OfType<MaterialClockDial>().Single();
            Assert.Equal(MaterialClockDialSelection.Hour, dial.Selection);
            picker.Time = new TimeSpan(12, 34, 0);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(MaterialClockDialSelection.Hour, dial.Selection);
            Assert.Equal(12, dial.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NativeAutoSizeRespondsToRichRunMutationAndRespectsExplicitFontSize()
    {
        var run = new Avalonia.Controls.Documents.Run("Short");
        var text = new TextBlock { Width = 240, Height = 80 };
        text.Inlines!.Add(run);
        MaterialTypography.SetAutoSize(text, MaterialTextAutoSize.StepBased(12, 40, 1));
        var window = Show(text);
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(40, text.FontSize);
            run.Text = "A native rich-text run now requires a smaller font size";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.InRange(text.FontSize, 12, 39);
            text.FontSize = 17;
            text.Width = 400;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(17, text.FontSize);
            MaterialTypography.SetAutoSize(text, null);
            Assert.Equal(17, text.FontSize);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollFieldReattachStartsKeyboardFromDetachedStateUpdate()
    {
        var state = new MaterialScrollFieldState(10, 2);
        var field = new MaterialScrollField { State = state, Width = 240, Height = 200 };
        var root = new StackPanel { Children = { field } };
        var window = Show(root);
        try
        {
            root.Children.Remove(field);
            state.ScrollToOption(7);
            root.Children.Add(field);
            window.UpdateLayout();
            field.Focus();
            Press(window, Key.Down);
            Assert.Equal(8, state.SelectedOption);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NestedTonalSurfacesUseAbsoluteElevationAndKeepCallerBackground()
    {
        var inner = new MaterialCard { Layout = MaterialCardLayout.Slot, Content = "Nested" };
        var outer = new MaterialCard { Layout = MaterialCardLayout.Slot, Content = inner };
        MaterialSurface.SetTonalElevation(outer, 2);
        MaterialSurface.SetTonalElevation(inner, 3);
        var window = Show(outer);
        try
        {
            Assert.Equal(5, MaterialSurface.GetAbsoluteTonalElevation(inner));
            var part = inner.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
                .First(border => border.Name == "PART_Card");
            var surface = (Avalonia.Media.Color)inner.FindResource(inner.ActualThemeVariant, "MaterialSurfaceColor")!;
            var tint = (Avalonia.Media.Color)inner.FindResource(inner.ActualThemeVariant, "MaterialSurfaceTintColor")!;
            Assert.Equal(MaterialAssist.SurfaceColorAtElevation(surface, tint, 5),
                ((Avalonia.Media.ISolidColorBrush)part.Background!).Color);
            MaterialSurface.SetTonalElevationEnabled(outer, false);
            Assert.Equal(surface, ((Avalonia.Media.ISolidColorBrush)part.Background!).Color);
            inner.Background = Avalonia.Media.Brushes.Coral;
            MaterialSurface.SetTonalElevationEnabled(outer, true);
            Assert.Same(Avalonia.Media.Brushes.Coral, part.Background);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DiscreteSliderDragUsesStationaryOwnerAfterThumbRelayout()
    {
        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = 20,
            Width = 400, TickFrequency = 10, IsSnapToTickEnabled = true
        };
        var window = Show(slider);
        try
        {
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(24, 24), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(40, 0));
            window.UpdateLayout();
            Assert.Equal(30, slider.Value);
            window.MouseMove(point + new Vector(80, 0));
            window.UpdateLayout();
            Assert.Equal(40, slider.Value);
            window.MouseUp(point + new Vector(80, 0), MouseButton.Left);
            window.MouseMove(point + new Vector(120, 0));
            Assert.Equal(40, slider.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DiscreteSliderThumbTargetUsesCornerInsetButEndpointsStayAtGutter()
    {
        var slider = new Slider
        {
            Minimum = 0, Maximum = 100, Value = 20,
            Width = 400, TickFrequency = 10, IsSnapToTickEnabled = true
        };
        var window = Show(slider);
        try
        {
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            Assert.Equal(86, Center(), 1);
            slider.Value = 0;
            window.UpdateLayout();
            Assert.Equal(2, Center(), 1);
            slider.Value = 100;
            window.UpdateLayout();
            Assert.Equal(398, Center(), 1);
            slider.Value = 20;
            slider.IsSnapToTickEnabled = false;
            window.UpdateLayout();
            Assert.InRange(Center(), 80.7, 81.7);

            double Center() => thumb.TranslatePoint(
                new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), slider)!.Value.X;
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TextEditingAndSelectionSurviveThemeChange()
    {
        var text = new TextBox();
        MaterialTextField.SetLabel(text, "Name");
        var window = Show(text);
        try
        {
            text.Focus();
            window.KeyTextInput("material");
            Assert.Equal("material", text.Text);
            text.SelectionStart = 1;
            text.SelectionEnd = 4;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal("material", text.Text);
            Assert.Equal("ate", text.SelectedText);
            window.KeyTextInput("D");
            Assert.Equal("mDrial", text.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToggleKeyboardAndDisabledInput()
    {
        var toggle = new ToggleButton { Content = "Favorite" };
        var window = Show(toggle);
        try
        {
            toggle.Focus();
            Press(window, Key.Space);
            Assert.True(toggle.IsChecked);
            toggle.IsEnabled = false;
            Press(window, Key.Space);
            Assert.True(toggle.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RangeValuesClampOnCrossingAndBoundsChange()
    {
        var range = new MaterialRangeSlider { Minimum = 0, Maximum = 100, LowerValue = 20, UpperValue = 80 };
        range.LowerValue = 90;
        Assert.Equal(80, range.LowerValue);
        range.Maximum = 50;
        Assert.InRange(range.LowerValue, range.Minimum, 50);
        Assert.InRange(range.UpperValue, range.LowerValue, 50);
        range.UpperValue = -10;
        Assert.Equal(range.LowerValue, range.UpperValue);
    }

    [AvaloniaFact]
    public void RadioGroupingAndTriStateKeyboard()
    {
        var first = new RadioButton { GroupName = "test", IsChecked = true };
        var second = new RadioButton { GroupName = "test" };
        var check = new CheckBox { IsThreeState = true, IsChecked = false };
        var panel = new StackPanel(); panel.Children.Add(first); panel.Children.Add(second); panel.Children.Add(check);
        var window = Show(panel);
        try
        {
            second.Focus(); Press(window, Key.Space);
            Assert.True(second.IsChecked); Assert.False(first.IsChecked);
            check.Focus(); Press(window, Key.Space); Assert.True(check.IsChecked);
            Press(window, Key.Space); Assert.Null(check.IsChecked);
            Press(window, Key.Space); Assert.False(check.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TabsKeyboardSelectsNextContent()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "One", Content = "First" });
        tabs.Items.Add(new TabItem { Header = "Two", Content = "Second" });
        var window = Show(tabs);
        try
        {
            ((TabItem)tabs.Items[0]!).Focus();
            Press(window, Key.Right);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void RangeThumbKeyboardSnapsAndCannotCross()
    {
        var range = new MaterialRangeSlider { LowerValue = 20, UpperValue = 40, TickFrequency = 10, IsSnapToTickEnabled = true, SmallChange = 10 };
        var window = Show(range);
        try
        {
            var lower = range.GetVisualDescendants().OfType<Thumb>().Single(t => t.Name == "PART_LowerThumb");
            lower.Focus();
            Press(window, Key.Right); Assert.Equal(30, range.LowerValue);
            Press(window, Key.End); Assert.Equal(40, range.LowerValue);
            Press(window, Key.Right); Assert.Equal(40, range.LowerValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RichTooltipEscapeDismissesAndRestoresOwnerFocus()
    {
        var action = new Button { Content = "Action" };
        var rich = new RichTooltip { Title = "Help", Content = "Details", Actions = action };
        var owner = new Button { Content = "Help", Flyout = rich };
        var window = Show(owner);
        try
        {
            owner.Focus();
            rich.ShowAt(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(rich.IsOpen);
            action.Focus();
            action.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Dispatcher.UIThread.RunJobs();
            Assert.False(rich.IsOpen);
            Assert.True(owner.IsFocused);
        }
        finally { rich.Hide(); window.Close(); }
    }

    [AvaloniaFact]
    public void RichTooltipPersistenceResolvesFromActionsUnlessSet()
    {
        // Tooltip.kt: isPersistent defaults to false in rememberTooltipState, and the component
        // recommends true "when there is actionable content". An unset property therefore keeps
        // actionable tooltips open and gives actionless ones the 1.5 s source duration.
        var actionless = new RichTooltip { Title = "Help", Content = "Details" };
        Assert.False(actionless.IsPersistent);
        Assert.False(actionless.EffectiveIsPersistent);

        var actionable = new RichTooltip { Title = "Help", Content = "Details", Actions = new Button() };
        Assert.True(actionable.EffectiveIsPersistent);

        actionless.IsPersistent = true;
        Assert.True(actionless.EffectiveIsPersistent);

        actionable.IsPersistent = false;
        Assert.False(actionable.EffectiveIsPersistent);
    }

    [AvaloniaFact]
    public void RichTooltipSchedulesDurationOnlyForNonPersistentContent()
    {
        var transientOwner = new Button { Content = "Help" };
        var window = Show(transientOwner);
        var transient = new RichTooltip { Title = "Help", Content = "Details" };
        try
        {
            transient.ShowAt(transientOwner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(transient.IsOpen);
            Assert.Equal(TimeSpan.FromMilliseconds(1500), transient.ScheduledDismissalInterval);

            transient.Hide();
            Dispatcher.UIThread.RunJobs();
            Assert.False(transient.IsOpen);
            Assert.Null(transient.ScheduledDismissalInterval);
        }
        finally { transient.Hide(); window.Close(); }

        var persistent = new RichTooltip
        {
            Title = "Help",
            Content = "Details",
            Actions = new Button { Content = "Action" },
        };
        var persistentOwner = new Button { Content = "Help" };
        var second = Show(persistentOwner);
        try
        {
            persistent.ShowAt(persistentOwner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(persistent.IsOpen);
            Assert.True(persistent.EffectiveIsPersistent);
            Assert.Null(persistent.ScheduledDismissalInterval);
        }
        finally { persistent.Hide(); second.Close(); }
    }

    [AvaloniaFact]
    public void PasswordRevealConcealsOnFocusLeavingField()
    {
        var password = new TextBox { Text = "secret", PasswordChar = '●' };
        MaterialTextField.SetShowPasswordReveal(password, true);
        var other = new Button { Content = "Other" };
        var panel = new StackPanel(); panel.Children.Add(password); panel.Children.Add(other);
        var window = Show(panel);
        try
        {
            password.Focus(); password.RevealPassword = true;
            other.Focus();
            Assert.False(password.RevealPassword);
            Assert.Equal("secret", password.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void GroupedMenuNavigationSkipsUnavailableRowsAndKeepsRowFocus()
    {
        var first = new MenuItem { Header = "First" };
        var hidden = new MenuItem { Header = "Hidden", IsVisible = false };
        var disabled = new MenuItem { Header = "Disabled", IsEnabled = false };
        var last = new MenuItem { Header = "Last" };
        var group = new MaterialMenuGroup { Items = { hidden, disabled, last } };
        var surface = new MenuFlyoutPresenter { Items = { first, group } };
        var owner = new Button { Content = "Open" };
        var popup = new Popup { PlacementTarget = owner, Child = surface, IsLightDismissEnabled = false };
        var window = Show(new StackPanel { Children = { owner, popup } });
        try
        {
            popup.IsOpen = true;
            window.UpdateLayout();
            Assert.True(first.Focus());
            first.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Assert.True(last.IsFocused);
            last.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Assert.True(first.IsFocused);
            first.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
            Assert.True(last.IsFocused);
            last.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
            Assert.True(first.IsFocused);
        }
        finally { popup.IsOpen = false; window.Close(); }
    }

    [AvaloniaFact]
    public void PlainTooltipFocusAndEscape()
    {
        var owner = new Button { Content = "Help" };
        ToolTip.SetTip(owner, "Details");
        var window = Show(owner);
        try
        {
            owner.Focus(NavigationMethod.Tab);
            Assert.True(ToolTip.GetIsOpen(owner));
            Press(window, Key.Escape);
            Assert.False(ToolTip.GetIsOpen(owner));
            Assert.True(owner.IsFocused);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void SearchFocusPreservesSharedSelectionRange()
    {
        var bar = new MaterialSearchBar { Text = "material", SelectionStart = 2, SelectionEnd = 5 };
        var other = new Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { bar, other } });
        try
        {
            bar.FocusInput();
            var field = bar.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.Equal("ter", field.SelectedText);
            other.Focus();
            bar.FocusInput();
            Assert.Equal("ter", field.SelectedText);
            Assert.Equal(2, bar.SelectionStart);
            Assert.Equal(5, bar.SelectionEnd);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CarouselPublishesInitialItemWithoutExplicitScroll()
    {
        var carousel = new MaterialCarousel { Width = 550, ItemHeight = 180 };
        carousel.Items.Add(new MaterialCarouselItem { Content = "First" });
        carousel.Items.Add(new MaterialCarouselItem { Content = "Second" });
        var window = Show(new StackPanel { Children = { carousel } });
        try
        {
            Assert.Equal(0, carousel.CurrentItem);
            Assert.Equal(0, carousel.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CarouselExplicitScrollWinsOverPendingDirectionChange()
    {
        var carousel = new MaterialCarousel { Width = 550, Height = 180 };
        for (var i = 0; i < 100; i++) carousel.Items.Add($"Card {i}");
        var window = Show(carousel);
        try
        {
            carousel.ScrollToItem(50);
            window.UpdateLayout();
            Assert.Equal(50, carousel.CurrentItem);
            carousel.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
            carousel.ScrollToItem(10);
            window.UpdateLayout();
            Assert.Equal(10, carousel.CurrentItem);
            Assert.Equal(10, carousel.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CustomButtonTextInheritsButtonForegroundAcrossThemeChanges()
    {
        var label = new TextBlock { Text = "Action" };
        var button = new Button { Content = label };
        var window = Show(button);
        try
        {
            Assert.Equal(button.Foreground, label.Foreground);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(button.Foreground, label.Foreground);
            button.IsEnabled = false;
            Assert.Equal(button.Foreground, label.Foreground);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModalRailOpeningPreservesSelectedDataDestination()
    {
        var rail = new MaterialNavigationRail
        {
            ItemsSource = new[] { "Home", "Library", "Settings" },
            Mode = MaterialNavigationRailMode.Wide,
            IsExpanded = true,
            ShowFab = false,
            ShowMenuButton = false,
            SelectedIndex = 2
        };
        var host = new MaterialNavigationRailHost { Rail = rail };
        var window = Show(host);
        try
        {
            host.OpenRail();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, rail.SelectedIndex);
            Assert.True(((Control)rail.ContainerFromIndex(2)!).IsFocused);
            host.CloseRail();
            host.OpenRail();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, rail.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ButtonGroupSwitchesKindsWithVisualIconAndContent()
    {
        var icon = new MaterialIcon { IconName = "star" };
        var label = new TextBlock { Text = "Visual label" };
        var item = new MaterialButtonGroupItem { Icon = icon, Content = label };
        var group = new MaterialButtonGroup { Kind = MaterialButtonGroupKind.Standard };
        group.Items.Add(item);
        var window = Show(group);
        try
        {
            foreach (var kind in new[] { MaterialButtonGroupKind.Segmented, MaterialButtonGroupKind.Connected,
                         MaterialButtonGroupKind.Standard, MaterialButtonGroupKind.Segmented })
            {
                group.Kind = kind;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.True(icon.IsAttachedToVisualTree());
                Assert.True(label.IsAttachedToVisualTree());
                Assert.Single(item.GetVisualDescendants(), v => ReferenceEquals(v, icon));
                Assert.Single(item.GetVisualDescendants(), v => ReferenceEquals(v, label));
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void GroupProgrammaticAndFocusSelectionRemainExclusive()
    {
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            SelectOnFocus = true
        };
        var first = new MaterialButtonGroupItem { Content = "First" };
        var second = new MaterialButtonGroupItem { Content = "Second" };
        group.Items.Add(first);
        group.Items.Add(second);
        var window = Show(group);
        try
        {
            group.Select(0, true);
            group.Select(1, true);
            Assert.False(first.IsChecked);
            Assert.True(second.IsChecked);
            Assert.Equal(1, group.SelectedIndex);
            second.Focus();
            Press(window, Key.Home);
            Assert.True(first.IsChecked);
            Assert.False(second.IsChecked);
            Assert.Equal(0, group.SelectedIndex);
            Assert.Same(first, group.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ConnectedGroupResolvesFullCornersAgainstPaintedBounds()
    {
        // F03/I03: CornerFull is CircleShape, i.e. half of the painted container. The item must
        // never rest at the 9999 DIP sentinel, and a changed container size re-resolves the same
        // logical shape instead of keeping the previous physical corner.
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            SelectionMode = MaterialButtonGroupSelectionMode.Single
        };
        var first = new MaterialButtonGroupItem { Content = "First" };
        var second = new MaterialButtonGroupItem { Content = "Second" };
        group.Items.Add(first);
        group.Items.Add(second);
        var window = Show(group);
        try
        {
            var compactInset = first.ContainerInset;
            var compactHeight = first.Bounds.Height - compactInset.Top - compactInset.Bottom;
            Assert.True(compactHeight > 0);
            Assert.Equal(compactHeight / 2, first.CornerRadius.TopLeft, 3);
            Assert.Equal(compactHeight / 2, first.CornerRadius.BottomLeft, 3);
            Assert.Equal(8, first.CornerRadius.TopRight, 3);
            Assert.Equal(8, second.CornerRadius.TopLeft, 3);
            Assert.Equal(compactHeight / 2, second.CornerRadius.TopRight, 3);

            group.ControlSize = MaterialControlSize.Large;
            window.UpdateLayout();
            var largeInset = first.ContainerInset;
            var largeHeight = first.Bounds.Height - largeInset.Top - largeInset.Bottom;
            Assert.True(largeHeight > compactHeight);
            Assert.Equal(largeHeight / 2, first.CornerRadius.TopLeft, 3);
            Assert.Equal(16, first.CornerRadius.TopRight, 3);
            Assert.Equal(largeHeight / 2, second.CornerRadius.BottomRight, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FabLatestFocusOverridesHoverAndDisableClearsElevation()
    {
        var fab = new MaterialFab { Content = "+" };
        var other = new Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { other, fab } });
        try
        {
            other.Focus();
            var point = fab.TranslatePoint(new Point(fab.Bounds.Width / 2, fab.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            var surface = fab.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Surface");
            Assert.Equal(fab.FindResource(fab.ActualThemeVariant, "MaterialElevationLevel4Shadow"), surface.BoxShadow);
            fab.Focus();
            Assert.Equal(fab.FindResource(fab.ActualThemeVariant, "MaterialElevationLevel3Shadow"), surface.BoxShadow);
            fab.IsEnabled = false;
            Assert.Equal(fab.FindResource(fab.ActualThemeVariant, "MaterialElevationLevel0Shadow"), surface.BoxShadow);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FullScreenMonthPreservesWeekdaySlotsInBothDirections()
    {
        var picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.FullScreen,
            SelectionMode = MaterialDateSelectionMode.Range,
            MinimumYear = 2026,
            MaximumYear = 2026,
            DisplayDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB")
        };
        var window = Show(picker);
        try
        {
            foreach (var direction in new[] { Avalonia.Media.FlowDirection.LeftToRight, Avalonia.Media.FlowDirection.RightToLeft })
            {
                picker.FlowDirection = direction;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var cells = picker.GetVisualDescendants().OfType<MaterialDateCell>()
                    .Where(c => c.IsEffectivelyVisible && c.Tag is DateTime date && date.Year == 2026 && date.Month == 1)
                    .ToArray();
                var first = cells.Single(c => ((DateTime)c.Tag!).Day == 1);
                var second = cells.Single(c => ((DateTime)c.Tag!).Day == 2);
                var eighth = cells.Single(c => ((DateTime)c.Tag!).Day == 8);
                Assert.Equal(first.Bounds.Y, second.Bounds.Y);
                Assert.Equal(first.Bounds.X, eighth.Bounds.X);
                Assert.Equal(48, eighth.Bounds.Y - first.Bounds.Y, 4);
                Assert.Equal(144, first.Bounds.X, 4);
                var firstPoint = first.TranslatePoint(default, window)!.Value;
                var secondPoint = second.TranslatePoint(default, window)!.Value;
                Assert.Equal(direction == Avalonia.Media.FlowDirection.LeftToRight ? 48 : -48,
                    secondPoint.X - firstPoint.X, 4);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EnterPressedShapeCancelsWhenFocusLeavesBeforeKeyUp()
    {
        var button = new Button { Content = "Action" };
        var other = new Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { button, other } });
        try
        {
            button.Focus();
            var rest = MaterialAssist.GetAnimatedCornerRadius(button);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.NotEqual(rest, MaterialAssist.GetAnimatedCornerRadius(button));
            other.Focus();
            Assert.Equal(rest, MaterialAssist.GetAnimatedCornerRadius(button));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledOutlinedCardStrokeBlendsOutlineOverSurfaceContainerLow()
    {
        // Card.kt outlinedCardBorder(enabled = false): outline@0.12 compositeOver
        // ElevatedCardTokens.ContainerColor (surface-container-low), drawn as one opaque stroke.
        var card = new MaterialCard
        {
            Variant = MaterialCardVariant.Outlined,
            IsEnabled = false,
            Content = "Card"
        };
        var window = Show(card);
        try
        {
            var stroke = card.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_DisabledOutline");
            Assert.True(stroke.IsVisible);
            Assert.Equal(1d, stroke.Opacity);

            var outline = Role(card, "MaterialOutlineColor");
            var container = Role(card, "MaterialSurfaceContainerLowColor");
            static byte Mix(byte foreground, byte background) =>
                (byte)Math.Round(foreground * 0.12 + background * 0.88);
            Assert.Equal(
                Avalonia.Media.Color.FromArgb(255,
                    Mix(outline.R, container.R), Mix(outline.G, container.G), Mix(outline.B, container.B)),
                BrushColor(stroke.BorderBrush));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledCheckedExpressiveMenuUsesDisabledRolesForEverySlot()
    {
        // Menu.kt SelectableMenuItemColors resolves every slot disabled-first, so a disabled
        // checked expressive row keeps the disabled roles: OnSurface for the standard variant and
        // OnTertiaryContainer for the vibrant variant, on the label, the leading icon, the
        // supporting text and the container.
        var standardIcon = new TextBlock { Text = "S" };
        var standard = new MenuItem
        {
            Header = "Standard",
            Icon = standardIcon,
            IsChecked = true,
            IsEnabled = false
        };
        MaterialMenu.SetSurfaceStyle(standard, MaterialMenuSurfaceStyle.Expressive);
        var vibrant = new MenuItem { Header = "Vibrant", IsChecked = true, IsEnabled = false };
        MaterialMenu.SetSurfaceStyle(vibrant, MaterialMenuSurfaceStyle.Expressive);
        MaterialMenu.SetVariant(vibrant, MaterialMenuVariant.Vibrant);
        MaterialMenu.SetSupportingText(vibrant, "Supporting");

        var window = Show(new StackPanel { Children = { standard, vibrant } });
        try
        {
            var onSurface = Role(standard, "MaterialOnSurfaceColor");
            Assert.Equal(onSurface, BrushColor(standard.Foreground));
            Assert.Equal(Role(standard, "MaterialSurfaceContainerLowColor"),
                BrushColor(standard.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Name == "Surface").Background));
            var iconHost = standard.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(presenter => presenter.Name == "PART_IconPresenter");
            Assert.Equal(onSurface,
                BrushColor((Avalonia.Media.IBrush?)iconHost.GetValue(
                    Avalonia.Controls.Documents.TextElement.ForegroundProperty)));

            var onTertiaryContainer = Role(vibrant, "MaterialOnTertiaryContainerColor");
            Assert.Equal(onTertiaryContainer, BrushColor(vibrant.Foreground));
            Assert.Equal(Role(vibrant, "MaterialTertiaryContainerColor"),
                BrushColor(vibrant.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Name == "Surface").Background));
            var supporting = vibrant.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Name == "SupportingText");
            Assert.Equal(onTertiaryContainer, BrushColor(supporting.Foreground));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WrappedSupportingTextSelectsThreeLineAnatomyAndExplicitCountWins()
    {
        // ListItem.kt derives the item type from the measured supporting text
        // (FirstBaseline != LastBaseline), so a wrapped paragraph becomes a three-line row while
        // an explicit MaterialList.LineCount stays authoritative.
        const string paragraph =
            "A supporting paragraph long enough to wrap onto a second measured line inside a narrow list row.";
        var measured = new ListBoxItem { Content = "Measured" };
        MaterialList.SetHeadline(measured, "Headline");
        MaterialList.SetSupportingText(measured, paragraph);
        var explicitCount = new ListBoxItem { Content = "Explicit" };
        MaterialList.SetHeadline(explicitCount, "Headline");
        MaterialList.SetSupportingText(explicitCount, paragraph);
        MaterialList.SetLineCount(explicitCount, 2);

        var list = new ListBox { Width = 220, Items = { measured, explicitCount } };
        var window = Show(list);
        try
        {
            // The class change invalidates measure, so the anatomy settles on the next pass.
            window.UpdateLayout();
            Assert.Contains("md-list-3line", measured.Classes);
            Assert.DoesNotContain("md-list-2line", measured.Classes);
            Assert.Equal(88d, measured.MinHeight);

            Assert.Contains("md-list-2line", explicitCount.Classes);
            Assert.DoesNotContain("md-list-3line", explicitCount.Classes);
            Assert.Equal(72d, explicitCount.MinHeight);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SnackbarCanBeRequeuedWhileClosingBehindAnotherItem()
    {
        var host = new MaterialSnackbarHost();
        var window = Show(host);
        try
        {
            var first = new MaterialSnackbar { Text = "A" };
            var second = new MaterialSnackbar { Text = "B" };
            var initial = host.Show(first);
            Dispatcher.UIThread.RunJobs();
            var next = host.Show(second);
            System.Threading.Tasks.Task<MaterialSnackbarCloseReason>? repeated = null;
            first.Closing += (_, _) => repeated ??= host.Show(first);
            first.Dismiss();
            Assert.True(initial.IsCompleted);
            Assert.Same(second, host.Current);
            Dispatcher.UIThread.RunJobs();
            second.Dismiss();
            Assert.True(next.IsCompleted);
            Assert.Same(first, host.Current);
            Dispatcher.UIThread.RunJobs();
            first.Dismiss();
            Assert.NotNull(repeated);
            Assert.True(repeated!.IsCompleted);
            Assert.Null(host.Current);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SnackbarHostTracksAvailableWidthWithoutOverridingExplicitWidth()
    {
        var host = new MaterialSnackbarHost { Width = 600 };
        var window = Show(host);
        try
        {
            var snackbar = new MaterialSnackbar { Text = "Message" };
            host.Show(snackbar);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var initial = snackbar.Bounds.Width;
            host.Width = 350;
            window.UpdateLayout();
            Assert.True(snackbar.Bounds.Width < initial);
            host.Width = 600;
            window.UpdateLayout();
            Assert.Equal(initial, snackbar.Bounds.Width, 5);
            host.DismissCurrent();
            var explicitWidth = new MaterialSnackbar { Text = "Narrow", Width = 150 };
            host.Show(explicitWidth);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(150, explicitWidth.Bounds.Width, 5);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SnackbarAutoTextWrapsAndGrowsWithoutEllipsis()
    {
        // Snackbar.kt measures the supporting text with no line cap and grows the container; the
        // explicit one/two-line caps are the library extension.
        const string message =
            "A snackbar message long enough to wrap onto a second and a third measured line " +
            "inside the 344 DIP container, so the source layout grows instead of trimming.";
        var snackbar = new MaterialSnackbar { Width = 344, Text = message };
        var window = Show(snackbar);
        try
        {
            snackbar.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var text = snackbar.GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.Name == "PART_Text");
            var container = snackbar.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "PART_Container");

            Assert.Equal(MaterialSnackbarLineCount.Auto, snackbar.LineCount);
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
            Assert.Equal(TextTrimming.None, text.TextTrimming);
            Assert.Equal(0, text.MaxLines);
            Assert.True(text.Bounds.Height > 30,
                $"The auto layout should wrap the long message (height {text.Bounds.Height}).");
            var autoHeight = container.Bounds.Height;

            // The explicit one-line cap keeps the old fixed anatomy: one ellipsized line and the
            // 48 DIP source height.
            snackbar.LineCount = MaterialSnackbarLineCount.OneLine;
            window.UpdateLayout();
            Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
            Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
            Assert.Equal(1, text.MaxLines);
            Assert.True(container.Bounds.Height < autoHeight,
                $"The one-line cap should be shorter than the measured layout ({container.Bounds.Height} vs {autoHeight}).");
        }
        finally { window.Close(); }
    }
}
