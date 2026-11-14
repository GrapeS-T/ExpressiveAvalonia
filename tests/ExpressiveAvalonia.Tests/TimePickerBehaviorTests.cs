using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Behaviour regressions for the time input draft rules, AMPM guard, format-aware advance,
/// vibrant/uncontained variants and the TimeScroll wheel mapping.
/// </summary>
public class TimePickerBehaviorTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 720, Height = 560, Content = control };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MaterialTimeInputSegment Hour(MaterialTimePicker picker) =>
        picker.GetVisualDescendants().OfType<MaterialTimeInputSegment>()
            .Single(segment => segment.Name == "PART_Hour");

    private static MaterialTimeInputSegment Minute(MaterialTimePicker picker) =>
        picker.GetVisualDescendants().OfType<MaterialTimeInputSegment>()
            .Single(segment => segment.Name == "PART_Minute");

    private static MaterialPeriodSelector Period(MaterialTimePicker picker) =>
        picker.GetVisualDescendants().OfType<MaterialPeriodSelector>()
            .Single(selector => selector.Name == "PART_Period");

    private static MaterialScrollField Wheel(MaterialTimePicker picker, string name) =>
        picker.GetVisualDescendants().OfType<MaterialScrollField>()
            .Single(field => field.Name == name);

    private static TextBox Editor(MaterialTimeInputSegment segment) =>
        segment.GetVisualDescendants().OfType<TextBox>().Single();

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Select(TextBox editor, int start, int end)
    {
        editor.Focus();
        editor.CaretIndex = end;
        editor.SelectionStart = start;
        editor.SelectionEnd = end;
    }

    private static void Type(TextBox editor, string text)
    {
        editor.RaiseEvent(new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Text = text,
        });
        Flush();
    }

    private static void PressEnter(TextBox editor)
    {
        editor.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Enter,
        });
        Flush();
    }

    [AvaloniaFact]
    public void DialAutomationExposesInvokableNumbersForEachFace()
    {
        var dial = new MaterialClockDial { Width = 256, Height = 256, Culture = CultureInfo.InvariantCulture };
        var window = Show(dial);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(dial);
            var hours = peer.GetChildren();
            Assert.Equal(12, hours.Count);
            var seven = hours.Single(child => child.GetName() == "7 hours of 12");
            Assert.Equal(AutomationControlType.Button, seven.GetAutomationControlType());
            Assert.NotNull(seven.GetProvider<IInvokeProvider>());
            seven.GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal(7, dial.Value);

            dial.Is24Hour = true;
            var hours24 = peer.GetChildren();
            Assert.Equal(24, hours24.Count);
            hours24.Single(child => child.GetName() == "20 hours of 24").GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal(20, dial.Value);

            dial.Selection = MaterialClockDialSelection.Minute;
            var minutes = peer.GetChildren();
            Assert.Equal(12, minutes.Count);
            minutes.Single(child => child.GetName() == "25 minutes of 60").GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal(25, dial.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InvalidHourDraftSurvivesBlurAndOrientationChange()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 45, 0),
            Format = MaterialTimeFormat.Hour24,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);

            hour.Text = "27";
            Assert.True(hour.IsInvalid);
            Assert.False(picker.IsValid);
            Assert.NotNull(picker.ValidationMessage);
            Assert.Equal(new TimeSpan(9, 45, 0), picker.Time);

            minute.FocusEditor();
            Dispatcher.UIThread.RunJobs();
            Assert.True(hour.IsInvalid);
            Assert.Equal("27", hour.Text);

            picker.Orientation = Avalonia.Layout.Orientation.Horizontal;
            Dispatcher.UIThread.RunJobs();
            Assert.True(hour.IsInvalid);
            Assert.Equal("27", hour.Text);
            Assert.False(picker.IsValid);

            hour.Text = "14";
            Dispatcher.UIThread.RunJobs();
            Assert.False(hour.IsInvalid);
            Assert.True(picker.IsValid);
            Assert.Equal(new TimeSpan(14, 45, 0), picker.Time);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EmptyFieldUsesSourcePeriodZeroTarget()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(13, 45, 0),
            Format = MaterialTimeFormat.Hour12,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);

            // PM empty hour resolves to the 12 o'clock dial position, i.e. 12:45 PM.
            hour.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.False(hour.IsInvalid);
            Assert.Equal(12, hour.Value);
            Assert.Equal(new TimeSpan(12, 45, 0), picker.Time);

            minute.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.False(minute.IsInvalid);
            Assert.Equal(0, minute.Value);
            Assert.Equal(new TimeSpan(12, 0, 0), picker.Time);

            picker.Time = new TimeSpan(9, 45, 0);
            Dispatcher.UIThread.RunJobs();
            hour.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            // AM empty hour resolves to 0 (displays as 12 on a 12-hour dial).
            Assert.Equal(12, hour.Value);
            Assert.Equal(new TimeSpan(0, 45, 0), picker.Time);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PeriodActionIsRejectedWhileHourDraftIsInvalid()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 0, 0),
            Format = MaterialTimeFormat.Hour12,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var period = Period(picker);

            hour.Text = "27";
            Dispatcher.UIThread.RunJobs();
            Assert.True(hour.IsInvalid);
            Assert.False(period.IsPm);

            period.IsPm = true;
            Dispatcher.UIThread.RunJobs();

            Assert.False(period.IsPm);
            Assert.Equal(new TimeSpan(9, 0, 0), picker.Time);
            Assert.True(hour.IsInvalid);
            Assert.Equal("27", hour.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SingleDigitAutoAdvanceIsFormatAware()
    {
        var picker = new MaterialTimePicker { Time = new TimeSpan(10, 0, 0), Format = MaterialTimeFormat.Hour24 };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);

            hour.Text = "4";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(4, 0, 0), picker.Time);
            Assert.True(hour.IsSelected);
            Assert.False(minute.IsSelected);

            picker.Format = MaterialTimeFormat.Hour12;
            Dispatcher.UIThread.RunJobs();
            hour.Text = "1";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(1, 0, 0), picker.Time);
            Assert.True(hour.IsSelected);

            hour.Text = "4";
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(4, 0, 0), picker.Time);
            Assert.True(minute.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VibrantAndUncontainedVariantsApplySourceShapesAndSizes()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 45, 0),
            InputMode = MaterialTimeInputMode.Keyboard,
            Variant = MaterialTimePickerVariant.Vibrant,
            TimeFieldShape = new CornerRadius(20),
            PeriodSelectorShape = new CornerRadius(999),
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);
            var period = Period(picker);

            Assert.Equal(MaterialTimeSegmentVariant.Vibrant, hour.Variant);
            Assert.Equal(MaterialTimeSegmentVariant.Vibrant, minute.Variant);
            Assert.Equal(100, hour.Width);
            Assert.Equal(120, hour.Height);
            Assert.Equal(20, hour.CornerRadius.TopLeft);
            Assert.True(period.IsVibrant);
            Assert.Equal(56, period.Width);
            Assert.Equal(120, period.Height);
            Assert.Equal(999, period.CornerRadius.TopLeft);

            picker.Variant = MaterialTimePickerVariant.Uncontained;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(MaterialTimeSegmentVariant.Uncontained, hour.Variant);
            Assert.Equal(136, hour.Height);
            Assert.Equal(20, hour.CornerRadius.TopLeft);
            Assert.False(period.IsVisible);
            Assert.True(picker.GetVisualDescendants().OfType<MaterialPeriodSelector>()
                .Single(selector => selector.Name == "PART_SidePeriod").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimeScrollMapsAmPmAndSynchronizesBothDirections()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 45, 0),
            Format = MaterialTimeFormat.Hour12,
            Variant = MaterialTimePickerVariant.Vibrant,
            InputMode = MaterialTimeInputMode.Scroll,
        };
        var window = Show(picker);
        try
        {
            var hourWheel = Wheel(picker, "PART_HourScroll");
            var minuteWheel = Wheel(picker, "PART_MinuteScroll");

            Assert.NotNull(hourWheel.State);
            Assert.NotNull(minuteWheel.State);
            var hourState = hourWheel.State!;
            var minuteState = minuteWheel.State!;
            Assert.Equal(8, hourState.SelectedOption); // 09:00 -> 12-hour option 8
            Assert.Equal(45, minuteState.SelectedOption);

            // Wheel -> state: option 0 displays 01 and commits 01:45.
            hourState.SelectedOption = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(1, 45, 0), picker.Time);

            // AMPM uses the 12-hour wheel position as its base.
            Period(picker).IsPm = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(13, 45, 0), picker.Time);
            Assert.Equal(0, hourState.SelectedOption);

            minuteState.SelectedOption = 5;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new TimeSpan(13, 5, 0), picker.Time);

            // State -> wheel: external time re-targets both wheels.
            picker.Time = new TimeSpan(22, 30, 0);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(9, hourState.SelectedOption); // 22:00 -> 10 PM -> option 9
            Assert.Equal(30, minuteState.SelectedOption);
            Assert.Equal("10", hourWheel.Formatter!(9));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NativeInsertionIntoTwoDigitsReplacesTheWholeField()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(12, 45, 0),
            Format = MaterialTimeFormat.Hour24,
            InputMode = MaterialTimeInputMode.Keyboard,
            AutoAdvance = false,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var editor = Editor(hour);
            Assert.Equal("12", hour.Text);

            // Third digit at the end: source deletes the two original digits.
            Select(editor, 2, 2);
            Type(editor, "3");
            Assert.Equal("3", hour.Text);
            Assert.Equal(3, hour.Value);
            Assert.Equal(new TimeSpan(3, 45, 0), picker.Time);

            // In the middle: source deletes indices 2..3 and 0..1, keeping the inserted digit.
            picker.Time = new TimeSpan(12, 45, 0);
            Flush();
            Select(editor, 1, 1);
            Type(editor, "9");
            Assert.Equal("9", hour.Text);
            Assert.Equal(new TimeSpan(9, 45, 0), picker.Time);

            // At the start: source deletes indices 1..3.
            picker.Time = new TimeSpan(12, 45, 0);
            Flush();
            Select(editor, 0, 0);
            Type(editor, "8");
            Assert.Equal("8", hour.Text);
            Assert.Equal(new TimeSpan(8, 45, 0), picker.Time);

            // A non-collapsed selection is ordinary replacement, not two-digit insertion.
            picker.Time = new TimeSpan(12, 45, 0);
            Flush();
            Select(editor, 0, 2);
            Type(editor, "7");
            Assert.Equal("7", hour.Text);
            Assert.Equal(new TimeSpan(7, 45, 0), picker.Time);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NativePasteRejectsNonDigitAndOverlongEditsWithoutChangingTheTime()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(12, 45, 0),
            Format = MaterialTimeFormat.Hour24,
            InputMode = MaterialTimeInputMode.Keyboard,
        };
        var window = Show(picker);
        try
        {
            var minute = Minute(picker);
            var editor = Editor(minute);
            Assert.Equal("45", minute.Text);

            // "4x" used to be sanitized to "4", silently changing 45 to 04.
            Select(editor, 0, 2);
            Type(editor, "4x");
            Assert.Equal("45", minute.Text);
            Assert.Equal(45, minute.Value);
            Assert.Equal(new TimeSpan(12, 45, 0), picker.Time);
            Assert.False(minute.IsInvalid);

            // An overlong paste used to be truncated to its first two digits. It is rejected.
            Select(editor, 0, 2);
            Type(editor, "123");
            Assert.Equal("45", minute.Text);
            Assert.Equal(45, minute.Value);
            Assert.Equal(new TimeSpan(12, 45, 0), picker.Time);
            Assert.False(minute.IsInvalid);

            // The same rule applies to a typed non-digit appended to a full field.
            Select(editor, 2, 2);
            Type(editor, "x");
            Assert.Equal("45", minute.Text);
            Assert.Equal(new TimeSpan(12, 45, 0), picker.Time);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardHourAdvanceHonorsAutoAdvanceButEnterAlwaysMoves()
    {
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(10, 0, 0),
            Format = MaterialTimeFormat.Hour12,
            InputMode = MaterialTimeInputMode.Keyboard,
            AutoAdvance = false,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);
            var editor = Editor(hour);

            Select(editor, 0, 2);
            Type(editor, "4");
            Assert.Equal("04", hour.Text);
            Assert.Equal(4, hour.Value);
            Assert.Equal(new TimeSpan(4, 0, 0), picker.Time);
            Assert.True(hour.IsSelected);
            Assert.False(minute.IsSelected);

            // Explicit Enter is the source ImeAction.Next action; AutoAdvance does not block it.
            PressEnter(editor);
            Assert.True(minute.IsSelected);
            Assert.False(hour.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CultureNumeralsApplyToCellsWheelsAndDialAndFollowLiveChanges()
    {
        var arabic = CultureInfo.GetCultureInfo("ar-EG");
        var picker = new MaterialTimePicker
        {
            Time = new TimeSpan(9, 45, 0),
            Format = MaterialTimeFormat.Hour24,
            InputMode = MaterialTimeInputMode.Scroll,
            Culture = arabic,
        };
        var window = Show(picker);
        try
        {
            var hour = Hour(picker);
            var minute = Minute(picker);
            var dial = picker.GetVisualDescendants().OfType<MaterialClockDial>().Single();
            var hourWheel = Wheel(picker, "PART_HourScroll");
            var minuteWheel = Wheel(picker, "PART_MinuteScroll");

            Assert.Equal("٠٩", hour.Text);
            Assert.Equal("٤٥", minute.Text);
            Assert.Same(arabic, dial.Culture);
            Assert.Equal("٠٩", hourWheel.Formatter!(9));
            Assert.Equal("٤٥", minuteWheel.Formatter!(45));

            picker.Culture = CultureInfo.GetCultureInfo("en-US");
            Flush();
            Assert.Equal("09", hour.Text);
            Assert.Equal("45", minute.Text);
            Assert.Equal("09", hourWheel.Formatter!(9));

            picker.Culture = arabic;
            Flush();
            // Scroll mode keeps the text cells hidden; switch to direct entry before editing.
            picker.InputMode = MaterialTimeInputMode.Keyboard;
            window.UpdateLayout();
            Flush();
            var editor = Editor(hour);
            Select(editor, 0, 2);
            Type(editor, "١٤");
            Assert.Equal("١٤", hour.Text);
            Assert.Equal(14, hour.Value);
            Assert.Equal(new TimeSpan(14, 45, 0), picker.Time);
            Assert.False(hour.IsInvalid);
        }
        finally { window.Close(); }
    }
}