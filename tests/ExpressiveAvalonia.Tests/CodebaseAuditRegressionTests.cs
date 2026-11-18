using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Shapes;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;
using Xunit;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Tests;

public class CodebaseAuditRegressionTests
{
    private static Window ShowWindow(Control content, double width = 640, double height = 480)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 6; i++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void PressKey(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    [AvaloniaFact]
    public void IdleSpringAndTweenDoNotOverwriteSubsequentValueOnDetach()
    {
        var host = new Grid();
        var control = new Border();
        host.Children.Add(control);
        var window = ShowWindow(host);
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var springValue = 0d;
            using var spring = MaterialMotion.Animate(control, 0, 10, v => springValue = v);
            Assert.Equal(10d, springValue);

            var tweenValue = 0d;
            using var tween = MaterialMotion.Tween(control, 0, 10, 0, 1, v => tweenValue = v);
            Assert.Equal(10d, tweenValue);

            springValue = 42d;
            tweenValue = 99d;

            host.Children.Remove(control);
            Settle(window);

            Assert.Equal(42d, springValue);
            Assert.Equal(99d, tweenValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NarrowWavyProgressBarRendersWithoutClampException()
    {
        var panel = new StackPanel();
        foreach (var width in new[] { 2d, 6d, 10d, 14d, 20d })
        {
            var determinate = new ProgressBar { Width = width, Minimum = 0, Maximum = 100, Value = 50 };
            MaterialProgress.SetIndicatorType(determinate, ProgressIndicatorType.Wave);
            var indeterminate = new ProgressBar { Width = width, IsIndeterminate = true };
            MaterialProgress.SetIndicatorType(indeterminate, ProgressIndicatorType.Wave);
            panel.Children.Add(determinate);
            panel.Children.Add(indeterminate);
        }

        var window = ShowWindow(panel);
        try
        {
            Settle(window);
            var presenters = panel.GetVisualDescendants().OfType<LinearProgressPresenter>().ToList();
            Assert.NotEmpty(presenters);
            foreach (var presenter in presenters)
            {
                var drawing = new DrawingGroup();
                using var ctx = drawing.Open();
                presenter.Render(ctx);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NarrowPrimaryTabControlMeasuresIndicatorWithoutClampException()
    {
        var tabs = new TabControl { Width = 20, Height = 80 };
        MaterialTabs.SetVariant(tabs, MaterialTabVariant.Primary);
        tabs.Items.Add(new TabItem { Header = "One", Content = "A" });
        tabs.Items.Add(new TabItem { Header = "Two", Content = "B" });

        var window = ShowWindow(new StackPanel { Children = { tabs } });
        try
        {
            tabs.SelectedIndex = 1;
            Settle(window);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NarrowCarouselAndInvertedSmallWidthBoundsDoNotThrow()
    {
        var carousel = new MaterialCarousel
        {
            Width = 8,
            Height = 120,
            ItemHeight = 100,
            Padding = new Thickness(8, 0),
            MinSmallItemWidth = 120,
            MaxSmallItemWidth = 40,
            ItemsSource = new[] { "One", "Two", "Three" }
        };

        var window = ShowWindow(new StackPanel { Children = { carousel } });
        try
        {
            Settle(window);
            carousel.Width = 320;
            Settle(window);
            Assert.Equal(3, carousel.ItemCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CarouselOwnContainerRemovalAndReparentingDoNotThrow()
    {
        var first = new MaterialCarouselItem { Content = "First" };
        var second = new MaterialCarouselItem { Content = "Second" };
        var carousel = new MaterialCarousel { Width = 400, ItemHeight = 140 };
        carousel.Items.Add(first);
        carousel.Items.Add(second);

        var otherHost = new Border();
        var window = ShowWindow(new StackPanel { Children = { carousel, otherHost } });
        try
        {
            carousel.Items.Remove(first);
            Settle(window);

            otherHost.Child = first;
            Settle(window);
            Assert.Same(first, otherHost.Child);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ZeroExtentRoundedPolygonNormalizeDoesNotProduceNaN()
    {
        var zeroPoly = RoundedPolygon.FromNumVertices(4, radius: 0).Normalized();
        Assert.True(double.IsFinite(zeroPoly.CenterX));
        Assert.True(double.IsFinite(zeroPoly.CenterY));
    }

    [AvaloniaFact]
    public void OpticalCenterDecoratorClampsFullCornerSentinelAndIgnoresNaN()
    {
        var symmetricFull = MaterialOpticalCenter.ComputeOffset(
            new Size(120, 40),
            new CornerRadius(9999, 9999, 9999, 9999),
            FlowDirection.LeftToRight,
            maxStartOffset: 16,
            maxEndOffset: 16);
        Assert.Equal(0d, symmetricFull, 5);

        var asymmetricFull = MaterialOpticalCenter.ComputeOffset(
            new Size(48, 40),
            new CornerRadius(4, 9999, 9999, 4),
            FlowDirection.LeftToRight,
            maxStartOffset: 13,
            maxEndOffset: 13);
        Assert.InRange(asymmetricFull, -3d, -1d);

        var nanResult = MaterialOpticalCenter.ComputeOffset(
            new Size(double.NaN, 40),
            new CornerRadius(double.NaN, 12, 12, double.PositiveInfinity),
            FlowDirection.LeftToRight,
            maxStartOffset: 10,
            maxEndOffset: 10);
        Assert.True(double.IsFinite(nanResult));
    }

    [AvaloniaFact]
    public void TypographyStyleBodyLargeAppliesEvenAsDefaultEnumValue()
    {
        var text = new TextBlock { Text = "Sample" };
        var host = new Border { Child = text };
        Avalonia.Controls.Documents.TextElement.SetFontSize(host, 30);
        var window = ShowWindow(host);
        try
        {
            Assert.Equal(30d, text.FontSize, 3);
            MaterialTypography.SetStyle(text, MaterialTypeStyle.BodyLarge);
            Settle(window);
            Assert.Equal(16d, text.FontSize, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SnackbarRetainsHitTestingAcrossShowCyclesAndFreshWindowOverlayHostWorks()
    {
        var window = ShowWindow(new Border());
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var snackbar = new MaterialSnackbar
            {
                Text = "Saved",
                ActionContent = "Undo",
                IsCloseButtonVisible = true
            };

            var first = MaterialSnackbarHost.Show(window, snackbar);
            Settle(window);
            Assert.True(snackbar.IsOpen);
            Assert.True(snackbar.IsHitTestVisible);

            snackbar.Dismiss();
            Settle(window);
            Assert.True(first.IsCompleted);

            var second = MaterialSnackbarHost.Show(window, snackbar);
            Settle(window);
            Assert.True(snackbar.IsOpen);
            Assert.True(snackbar.IsHitTestVisible);

            snackbar.Dismiss();
            Settle(window);
            Assert.True(second.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task SheetCanceledOpenAsyncDoesNotThrowAndMidCloseDetachRecovers()
    {
        var sheet = new MaterialBottomSheet
        {
            IsModal = true,
            Content = new Border { Height = 200 }
        };
        var host = new Grid { Children = { sheet } };
        var window = ShowWindow(host);
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            EventHandler<SheetOpeningEventArgs> cancelOpen = (_, e) => e.Cancel = true;
            sheet.Opening += cancelOpen;
            await sheet.OpenAsync();
            Assert.False(sheet.IsOpen);
            sheet.Opening -= cancelOpen;

            MaterialMotion.SetIsReduced(window, false);
            _ = sheet.OpenAsync();
            MaterialMotion.SetIsReduced(window, true);
            Settle(window);
            Assert.True(sheet.IsOpen);

            MaterialMotion.SetIsReduced(window, false);
            sheet.IsOpen = false;
            host.Children.Remove(sheet);
            Settle(window);

            MaterialMotion.SetIsReduced(window, true);
            host.Children.Add(sheet);
            Settle(window);

            Assert.Equal(MaterialSheetValue.Hidden, sheet.CurrentValue);
            Assert.False(sheet.IsVisible);

            await sheet.OpenAsync();
            Settle(window);
            Assert.True(sheet.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BottomSheetScaffoldTemplateReapplicationDoesNotThrow()
    {
        var scaffold = new MaterialBottomSheetScaffold
        {
            Content = new TextBlock { Text = "Body" },
            SheetContent = new TextBlock { Text = "Sheet" }
        };
        var window = ShowWindow(scaffold);
        try
        {
            var originalTemplate = scaffold.Template;
            scaffold.Template = null;
            Settle(window);
            scaffold.Template = originalTemplate;
            Settle(window);
            Assert.True(scaffold.Sheet.IsAttachedToVisualTree());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DialogRespectsCanExecuteFalseOnEnterAndSupportsControlIconUpdates()
    {
        var canExecute = false;
        var executed = 0;
        var command = new RelayTestCommand(() => executed++, () => canExecute);
        var dialog = new MaterialDialog
        {
            Title = "Confirm",
            Content = new TextBox { Text = "Draft" }
        };
        dialog.AddAction(new MaterialDialogAction("Save")
        {
            IsDefault = true,
            Icon = MaterialDialog.CreateIcon(),
            Command = command,
            Result = "saved"
        });

        var window = ShowWindow(new Border());
        try
        {
            MaterialMotion.SetIsReduced(window, true);
            var task = dialog.ShowAsync(window);
            Settle(window);

            dialog.AddAction(new MaterialDialogAction("Cancel") { IsCancel = true, Result = "cancel" });
            Settle(window);

            PressKey(window, Key.Enter);
            Settle(window);
            Assert.Equal(0, executed);
            Assert.True(dialog.IsOpen);
            Assert.False(task.IsCompleted);

            canExecute = true;
            command.RaiseCanExecuteChanged();
            Settle(window);

            PressKey(window, Key.Enter);
            Settle(window);
            Assert.Equal(1, executed);
            Assert.False(dialog.IsOpen);
            Assert.True(task.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DialogListArrowNavigationAcrossDisabledRowPreservesCheckedItems()
    {
        var list = new MaterialDialogList();
        var item0 = new MaterialDialogListItem { Content = "Zero", IsSelected = true };
        var item1 = new MaterialDialogListItem { Content = "One", IsEnabled = false };
        var item2 = new MaterialDialogListItem { Content = "Two", IsSelected = true };
        list.Items.Add(item0);
        list.Items.Add(item1);
        list.Items.Add(item2);

        var window = ShowWindow(list);
        try
        {
            Assert.True(item2.Focus());
            PressKey(window, Key.Up);
            Settle(window);

            Assert.True(item0.IsFocused);
            Assert.True(item0.IsSelected);
            Assert.True(item2.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuTextFieldFiltersItemsWithControlIconsWithoutReparentException()
    {
        var field = new MaterialMenuTextField { Width = 280 };
        field.Items.Add(new MaterialMenuTextFieldItem
        {
            Label = "Alpha",
            Icon = new ShapePath { Data = StreamGeometry.Parse("M0,0 L10,0 L10,10 Z"), Fill = Brushes.Red }
        });
        field.Items.Add(new MaterialMenuTextFieldItem
        {
            Label = "Beta",
            Icon = new ShapePath { Data = StreamGeometry.Parse("M0,0 L10,5 L0,10 Z"), Fill = Brushes.Blue }
        });

        var window = ShowWindow(field);
        try
        {
            field.Text = "b";
            Settle(window);
            field.Text = string.Empty;
            Settle(window);
            field.Text = "a";
            Settle(window);
            Assert.Equal("a", field.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuButtonDoesNotToggleCheckedWhenFlyoutIsNullOrClosingIsVetoed()
    {
        var noFlyout = new MaterialMenuButton { Content = "Options" };
        var vetoFlyout = new MenuFlyout { Items = { new MenuItem { Header = "Item" } } };
        vetoFlyout.Closing += (_, e) => e.Cancel = true;
        var vetoButton = new MaterialMenuButton { Content = "Veto", Flyout = vetoFlyout };

        var window = ShowWindow(new StackPanel { Children = { noFlyout, vetoButton } });
        try
        {
            var p1 = noFlyout.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(p1, MouseButton.Left);
            window.MouseUp(p1, MouseButton.Left);
            Settle(window);
            Assert.NotEqual(true, noFlyout.IsChecked);

            vetoFlyout.ShowAt(vetoButton);
            Settle(window);
            Assert.Equal(true, vetoButton.IsChecked);

            // Clicking while Closing is vetoed keeps Flyout open and IsChecked == true:
            var p2 = vetoButton.TranslatePoint(new Point(20, 20), window)!.Value;
            window.MouseDown(p2, MouseButton.Left);
            window.MouseUp(p2, MouseButton.Left);
            Settle(window);
            Assert.Equal(vetoFlyout.IsOpen, vetoButton.IsChecked == true);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DatePickerFieldSurvivesReattachAndClosedEscapeKeepsSelectedDate()
    {
        var expected = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero);
        var field = new MaterialDatePickerField { SelectedDate = expected, Width = 280 };
        var host = new Grid { Children = { field } };
        var window = ShowWindow(host);
        try
        {
            var input = field.GetVisualDescendants().OfType<TextBox>().First();
            input.Focus();
            PressKey(window, Key.Escape);
            Settle(window);
            Assert.Equal(expected.Date, field.SelectedDate?.Date);

            host.Children.Remove(field);
            Settle(window);
            host.Children.Add(field);
            Settle(window);

            field.IsOpen = true;
            Settle(window);
            Assert.True(field.IsOpen);
            field.IsOpen = false;
            Settle(window);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DatePickerHandlesInvertedYearBoundsWithoutCrash()
    {
        var picker = new MaterialDatePicker
        {
            MinimumYear = 2030,
            MaximumYear = 2020,
            DisplayDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var window = ShowWindow(picker);
        try
        {
            picker.Focus();
            PressKey(window, Key.Right);
            Settle(window);
            Assert.NotNull(picker.DisplayDate);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimePickerRapidModeSwitchKeepsHourFieldVisible()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 30, 0),
            InputMode = MaterialTimeInputMode.Dial
        };
        var window = ShowWindow(picker);
        try
        {
            MaterialMotion.SetIsReduced(window, false);
            picker.InputMode = MaterialTimeInputMode.Keyboard;
            picker.UpdateLayout();
            picker.InputMode = MaterialTimeInputMode.Dial;
            picker.UpdateLayout();
            MaterialMotion.SetIsReduced(window, true);
            picker.InputMode = MaterialTimeInputMode.Keyboard;
            Settle(window);

            var hour = picker.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_Hour");
            Assert.Equal(1d, hour.Opacity, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AppBarSupportsControlSubtitleSequentialHeightsAndRepeatedOverflowOpen()
    {
        var appBar = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.MediumFlexible,
            Title = "Title",
            Subtitle = new TextBlock { Text = "Control subtitle" },
            OverflowContent = new TextBlock { Text = "Settings" }
        };
        appBar.CollapsedHeight = 96;
        appBar.ExpandedHeight = 140;

        var window = ShowWindow(appBar);
        try
        {
            Assert.Equal(96d, appBar.CollapsedHeight);
            Assert.Equal(140d, appBar.ExpandedHeight);

            var overflowBtn = appBar.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Name == "PART_OverflowButton");
            if (overflowBtn is not null)
            {
                overflowBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, overflowBtn));
                Settle(window);
                appBar.MenuState.Dismiss();
                Settle(window);

                overflowBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, overflowBtn));
                Settle(window);
                appBar.MenuState.Dismiss();
                Settle(window);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavigationDrawerPreservesSelectionAcrossDetachAndClosedDrawerHostDoesNotStealSliderDrag()
    {
        var drawer = new MaterialNavigationDrawer();
        var item0 = new MaterialNavigationItem { Content = "Inbox" };
        var item1 = new MaterialNavigationItem { Content = "Starred", IsSelected = true };
        drawer.Items.Add(item0);
        drawer.Items.Add(item1);

        var slider = new Slider { Width = 300, Minimum = 0, Maximum = 100, Value = 10 };
        var drawerHost = new MaterialNavigationDrawerHost
        {
            Presentation = MaterialNavigationDrawerPresentation.Modal,
            IsDrawerOpen = false,
            Drawer = drawer,
            Content = new StackPanel { Margin = new Thickness(16), Children = { slider } }
        };
        var root = new Grid { Children = { drawerHost } };
        var window = ShowWindow(root);
        try
        {
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var start = thumb.TranslatePoint(new Point(24, 24), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(100, 0));
            window.MouseUp(start + new Vector(100, 0), MouseButton.Left);
            Settle(window);
            Assert.False(drawerHost.IsDrawerOpen);
            Assert.True(slider.Value > 20);

            drawerHost.IsDrawerOpen = true;
            Settle(window);
            Assert.Equal(1, drawer.SelectedIndex);
            Assert.Same(item1, drawer.SelectedItem);

            root.Children.Remove(drawerHost);
            Settle(window);
            Assert.Equal(1, drawer.SelectedIndex);
            Assert.Same(item1, drawer.SelectedItem);

            root.Children.Add(drawerHost);
            Settle(window);
            Assert.Equal(1, drawer.SelectedIndex);
            Assert.Same(item1, drawer.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BadgedBoxExposesNonNullAutomationPeer()
    {
        var button = new Button { Content = "Notifications" };
        var box = new MaterialBadgedBox
        {
            Content = button,
            Badge = new MaterialBadge { Text = "3" }
        };
        var window = ShowWindow(box);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(box);
            Assert.NotNull(peer);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InteractiveCardReflectsCommandCanExecuteState()
    {
        var canExecute = false;
        var command = new RelayTestCommand(() => { }, () => canExecute);
        var card = new MaterialCard
        {
            IsInteractive = true,
            Command = command,
            Content = "Action card"
        };

        var window = ShowWindow(card);
        try
        {
            Assert.False(card.IsEffectivelyEnabled);

            canExecute = true;
            command.RaiseCanExecuteChanged();
            Settle(window);
            Assert.True(card.IsEffectivelyEnabled);

            canExecute = false;
            command.RaiseCanExecuteChanged();
            Settle(window);
            Assert.False(card.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SwipeListItemDetachDuringDismissSettleDoesNotFireDismissed()
    {
        var dismissed = 0;
        var item = new MaterialSwipeListItem
        {
            Width = 320,
            Height = 64,
            EnableDismissFromStartToEnd = true,
            Content = new TextBlock { Text = "Row" }
        };
        item.Dismissed += (_, _) => dismissed++;

        var host = new StackPanel { Children = { item } };
        var window = ShowWindow(host);
        try
        {
            MaterialMotion.SetIsReduced(window, false);
            var start = item.TranslatePoint(new Point(20, 32), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(240, 0));
            window.MouseUp(start + new Vector(240, 0), MouseButton.Left);

            host.Children.Remove(item);
            Settle(window);

            Assert.Equal(0, dismissed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AccordionDoesNotStealNavigationKeysFromBodyTextBox()
    {
        var textBox = new TextBox { Text = "Hello world" };
        var first = new MaterialAccordionItem
        {
            Headline = "Section 1",
            IsExpanded = true,
            Body = textBox
        };
        var second = new MaterialAccordionItem
        {
            Headline = "Section 2",
            Body = new TextBlock { Text = "Body 2" }
        };
        var accordion = new MaterialAccordion();
        accordion.Items.Add(first);
        accordion.Items.Add(second);

        var window = ShowWindow(accordion);
        try
        {
            Assert.True(textBox.Focus());
            textBox.CaretIndex = 5;
            PressKey(window, Key.End);
            Settle(window);

            Assert.True(textBox.IsFocused);
            Assert.Equal(textBox.Text!.Length, textBox.CaretIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBarShowClockAndSlotHostSafetyZoneUpdateCorrectly()
    {
        var statusBar = new MaterialStatusBar { ShowClock = true, BarHeight = 40 };
        var slotHost = new MaterialSlotHost { Width = 200, Height = 100 };

        var window = ShowWindow(new StackPanel { Children = { statusBar, slotHost } });
        try
        {
            var clockLabel = statusBar.GetVisualDescendants().OfType<TextBlock>().First();
            Assert.True(clockLabel.IsVisible);

            statusBar.ShowClock = false;
            Settle(window);
            Assert.False(clockLabel.IsVisible);

            var safetyRect = slotHost.GetVisualDescendants().OfType<Rectangle>().First();
            Assert.True(safetyRect.IsVisible);

            slotHost.Content = new Border { Background = Brushes.CornflowerBlue };
            Settle(window);
            Assert.False(safetyRect.IsVisible);
        }
        finally { window.Close(); }
    }

    private sealed class RelayTestCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) => execute();
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
