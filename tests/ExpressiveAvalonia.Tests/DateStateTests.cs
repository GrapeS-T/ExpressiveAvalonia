using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// State contracts for <see cref="MaterialDatePicker"/> and its modal dialog: cancelling a
/// disjoint range draft restores the committed pair atomically, a rejected start replacement
/// clears the pair while keeping the editor error, and mode changes through the public property
/// share the built-in toggle's displayed-month rule.
/// </summary>
public class DateStateTests
{
    private static readonly TimeSpan Utc = TimeSpan.Zero;

    private static DateTimeOffset Day(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, Utc);

    private static Window Show(Control control)
    {
        var window = new Window { Width = 640, Height = 640, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Press(TopLevel window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    [AvaloniaFact]
    public void CancelRestoresDisjointCommittedRangeAtomically()
    {
        var picker = new MaterialDatePicker
        {
            SelectionMode = MaterialDateSelectionMode.Range,
            SelectedDate = Day(2026, 6, 20),
            SelectedEndDate = Day(2026, 6, 25),
            DisplayDate = Day(2026, 8, 1),
            View = MaterialDatePickerView.Month,
            IsInputMode = false,
        };
        var dialog = new MaterialDatePickerDialog(picker);
        dialog.Show();
        try
        {
            // A draft entirely before the committed range used to restore start=20 while the
            // draft end was still 5, which normalized the pair to no selection.
            picker.Select(new DateTime(2026, 6, 1));
            picker.Select(new DateTime(2026, 6, 5));
            Assert.Equal(1, picker.SelectedDate?.Day);
            Assert.Equal(5, picker.SelectedEndDate?.Day);
            picker.DisplayDate = Day(2026, 9, 1);
            picker.View = MaterialDatePickerView.Year;
            picker.IsInputMode = true;

            picker.Cancel();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(20, picker.SelectedDate?.Day);
            Assert.Equal(25, picker.SelectedEndDate?.Day);
            Assert.Equal(new DateTime(2026, 8, 1), picker.DisplayDate?.Date);
            Assert.Equal(MaterialDatePickerView.Month, picker.View);
            Assert.False(picker.IsInputMode);
        }
        finally
        {
            if (dialog.IsVisible)
                dialog.Close();
        }
    }

    [AvaloniaFact]
    public void RejectedStartAfterEndClearsBothEndpointsAndKeepsEditorError()
    {
        var picker = new MaterialDatePicker
        {
            SelectionMode = MaterialDateSelectionMode.Range,
            Culture = CultureInfo.InvariantCulture,
            SelectedDate = Day(2026, 6, 10),
            SelectedEndDate = Day(2026, 6, 20),
            IsInputMode = true,
        };
        var window = Show(picker);
        try
        {
            var start = picker.GetVisualDescendants().OfType<TextBox>()
                .Single(box => box.Name == "PART_StartInput");
            Assert.True(start.Focus());
            // DateInput reports the rejected start as null, and range setSelection then clears
            // the existing end as well.
            start.Text = "06252026";
            // Avalonia raises TextBox.TextChanged asynchronously (posted at Normal priority), so the
            // mask and the draft commit only run once the dispatcher pumps.
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.Null(picker.SelectedDate);
            Assert.Null(picker.SelectedEndDate);
            Assert.False(string.IsNullOrEmpty(picker.ValidationMessage));
            Assert.Equal("06/25/2026", start.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputModePropertyRetargetsDisplayMonthLikeTheBuiltInToggle()
    {
        var picker = new MaterialDatePicker
        {
            SelectedDate = Day(2026, 6, 15),
            DisplayDate = Day(2026, 9, 1),
        };
        var window = Show(picker);
        try
        {
            picker.IsInputMode = true;
            Assert.Equal((DateTime?)new DateTime(2026, 6, 1), picker.DisplayDate?.Date);

            picker.DisplayDate = Day(2026, 11, 1);
            picker.IsInputMode = false;
            Assert.Equal((DateTime?)new DateTime(2026, 6, 1), picker.DisplayDate?.Date);

            // The built-in edit button now relies on the same property transition.
            picker.DisplayDate = Day(2026, 10, 1);
            var edit = picker.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "PART_EditButton");
            edit.Focus();
            Press(window, Key.Space);
            Assert.True(picker.IsInputMode);
            Assert.Equal((DateTime?)new DateTime(2026, 6, 1), picker.DisplayDate?.Date);
        }
        finally
        {
            window.Close();
        }
    }
    [AvaloniaFact]
    public void ExtremeMonthsRenderAndNavigateWithoutOverflow()
    {
        foreach (var mode in new[] { MaterialDatePickerMode.Modal, MaterialDatePickerMode.FullScreen })
        foreach (var (year, month, direction) in new[] { (1, 1, -1), (9999, 12, 1) })
        {
            var picker = new MaterialDatePicker
            {
                Mode = mode,
                MinimumYear = year,
                MaximumYear = year,
                DisplayDate = Day(year, month, 1),
                SelectedDate = Day(year, month, 1),
            };
            var window = Show(picker);
            try
            {
                picker.NavigateMonth(direction);
                picker.NavigateYear(direction);
                window.UpdateLayout();
                Assert.Equal(new DateTime(year, month, 1), picker.DisplayDate?.Date);
                picker.View = MaterialDatePickerView.Month;
                window.UpdateLayout();
                picker.View = MaterialDatePickerView.Day;
                window.UpdateLayout();
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public void LocalizedDecimalDigitsAreAcceptedAndNormalizedInInputAndParsing()
    {
        var picker = new MaterialDatePicker
        {
            Culture = CultureInfo.InvariantCulture,
            MinimumYear = 1,
            MaximumYear = 9999,
            IsInputMode = true,
        };
        var window = Show(picker);
        try
        {
            var input = picker.GetVisualDescendants().OfType<TextBox>()
                .Single(box => box.Name == "PART_StartInput");
            Assert.True(input.Focus());
            input.Text = "٠٦٢٥٢٠٢٦";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("06/25/2026", input.Text);
            Assert.Equal(new DateTime(2026, 6, 25), picker.SelectedDate?.Date);
            Assert.True(picker.TryParseDate("०६२५२०२६", out var parsed));
            Assert.Equal(new DateTime(2026, 6, 25), parsed);
        }
        finally { window.Close(); }
    }

}