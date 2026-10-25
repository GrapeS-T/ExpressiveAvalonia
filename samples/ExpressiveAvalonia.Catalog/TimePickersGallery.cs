using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Gallery for the Material time picker family: both input modes, both formats, both
/// orientations, and the standalone building blocks (clock face, period selector, time cell).
/// </summary>
public static class TimePickersGallery
{
    public static Control Create()
    {
        var root = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        var status = new TextBlock { Text = "No time confirmed yet." };

        root.Children.Add(new TextBlock
        {
            Text = "Time picker",
            FontSize = 24,
            FontWeight = FontWeight.Medium,
        });
        root.Children.Add(new TextBlock
        {
            Text = "Dial and keyboard entry, 12 and 24 hour. The dial accepts pointer drags, the cells "
                   + "accept typing, arrows and Enter. Each picker below reports Confirmed and Cancelled.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 720,
        });

        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Card("Dial · 12 hour · vertical", Dial(TimeSpan.FromHours(7), MaterialTimeFormat.Hour12,
            MaterialTimeInputMode.Dial, Orientation.Vertical, status)));
        row.Children.Add(Card("Dial · 24 hour · vertical", Dial(TimeSpan.FromHours(20) + TimeSpan.FromMinutes(15),
            MaterialTimeFormat.Hour24, MaterialTimeInputMode.Dial, Orientation.Vertical, status)));
        row.Children.Add(Card("Keyboard · 12 hour", Dial(TimeSpan.FromHours(9) + TimeSpan.FromMinutes(45),
            MaterialTimeFormat.Hour12, MaterialTimeInputMode.Keyboard, Orientation.Vertical, status)));
        row.Children.Add(Card("Keyboard · 24 hour", Dial(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(5),
            MaterialTimeFormat.Hour24, MaterialTimeInputMode.Keyboard, Orientation.Vertical, status)));
        root.Children.Add(row);
        root.Children.Add(Card("Dial · 12 hour · horizontal",
            Dial(TimeSpan.FromHours(18) + TimeSpan.FromMinutes(30), MaterialTimeFormat.Hour12,
                MaterialTimeInputMode.Dial, Orientation.Horizontal, status)));

        var validated = Dial(TimeSpan.FromHours(10), MaterialTimeFormat.Hour12,
            MaterialTimeInputMode.Keyboard, Orientation.Vertical, status);
        validated.MinTime = TimeSpan.FromHours(9);
        validated.MaxTime = TimeSpan.FromHours(17);
        validated.Headline = "Office hours only";
        root.Children.Add(Card("Validation · 09:00-17:00", validated));

        root.Children.Add(new TextBlock
        {
            Text = "Building blocks",
            FontSize = 20,
            FontWeight = FontWeight.Medium,
            Margin = new Thickness(0, 8, 0, 0),
        });
        root.Children.Add(Blocks(status));

        var modal = new Button { Content = "Open modal dialog" };
        modal.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(modal) is not Window owner) return;
            var picker = new MaterialTimePicker
            {
                Time = TimeSpan.FromHours(16) + TimeSpan.FromMinutes(30),
                Format = MaterialTimeFormat.Hour12,
            };
            var result = await MaterialTimePickerDialog.ShowAsync(owner, picker);
            status.Text = result.IsConfirmed
                ? $"Dialog confirmed {Format(result.Time)}"
                : $"Dialog cancelled at {Format(result.Time)}";
        };
        root.Children.Add(modal);
        root.Children.Add(status);

        return new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control Card(string caption, Control content)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 24, 24) };
        panel.Children.Add(new TextBlock { Text = caption, FontWeight = FontWeight.Medium });
        panel.Children.Add(content);
        return panel;
    }

    private static MaterialTimePicker Dial(TimeSpan time, MaterialTimeFormat format,
        MaterialTimeInputMode mode, Orientation orientation, TextBlock status)
    {
        var picker = new MaterialTimePicker
        {
            Time = time,
            Format = format,
            InputMode = mode,
            Orientation = orientation,
        };

        picker.Confirmed += (_, e) => status.Text = $"Confirmed {Format(e.Time)}";
        picker.Cancelled += (_, _) => status.Text = $"Cancelled, value kept at {Format(picker.Time)}";
        return picker;
    }

    private static string Format(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}";

    private static Control Blocks(TextBlock status)
    {
        var dial = new MaterialClockDial
        {
            Value = 4,
            Selection = MaterialClockDialSelection.Hour,
        };
        AutomationProperties.SetName(dial, "Hour clock face");

        var minutes = new MaterialClockDial
        {
            Value = 20,
            Selection = MaterialClockDialSelection.Minute,
        };
        AutomationProperties.SetName(minutes, "Minute clock face");

        var face24 = new MaterialClockDial
        {
            Value = 20,
            Selection = MaterialClockDialSelection.Hour,
            Is24Hour = true,
        };
        AutomationProperties.SetName(face24, "24 hour clock face");

        var readout = new TextBlock { Text = "Hour 4" };
        dial.ValueChanged += (_, _) => readout.Text = $"Hour {dial.Value}";
        minutes.ValueChanged += (_, _) => readout.Text = $"Minute {minutes.Value}";
        face24.ValueChanged += (_, _) => readout.Text = $"24-hour {face24.Value:00}";

        var periods = new MaterialPeriodSelector { IsPm = true };
        periods.IsPmChanged += (_, _) => readout.Text = periods.IsPm ? "PM" : "AM";

        var horizontalPeriods = new MaterialPeriodSelector
        {
            Orientation = Orientation.Horizontal,
            Width = 216,
            Height = 38,
        };

        var hourCell = new MaterialTimeInputSegment
        {
            Value = 7,
            Minimum = 0,
            Maximum = 12,
            Label = "Hour",
            HelpText = "Enter a value between 1 and 12",
        };
        var minuteCell = new MaterialTimeInputSegment
        {
            Value = 30,
            Minimum = 0,
            Maximum = 59,
            Variant = MaterialTimeSegmentVariant.Keyboard,
            Label = "Minute",
        };
        hourCell.ValueChanged += (_, _) => readout.Text = $"Cell hour {hourCell.Value}";
        minuteCell.ValueChanged += (_, _) => readout.Text = $"Cell minute {minuteCell.Value:00}";
        minuteCell.EntryCompleted += (_, _) => status.Text = $"Minute entry completed: {minuteCell.Value:00}";

        var cells = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        cells.Children.Add(hourCell);
        cells.Children.Add(minuteCell);

        var grid = new WrapPanel { Orientation = Orientation.Horizontal };
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(dial));
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(minutes));
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(face24));
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(periods));
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(horizontalPeriods));
        grid.Children.Add(new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 24, 16) }.With(cells));
        grid.Children.Add(readout);

        return grid;
    }

    private static StackPanel With(this StackPanel panel, Control child)
    {
        panel.Children.Add(child);
        return panel;
    }
}
