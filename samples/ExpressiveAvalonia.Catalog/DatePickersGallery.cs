using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Catalog entry for the Material date picker family: docked field, docked surface,
/// modal surface, text-input presentation and the full-screen range view.
/// </summary>
public static class DatePickersGallery
{
    private static readonly DateTime Today = DateTime.Today;

    private static readonly (string Name, CultureInfo Culture)[] Cultures =
    [
        ("English (US)", new CultureInfo("en-US")),
        ("Русский", new CultureInfo("ru-RU")),
        ("Deutsch", new CultureInfo("de-DE")),
        ("日本語", new CultureInfo("ja-JP")),
        ("العربية", new CultureInfo("ar-SA")),
    ];

    public static Control Create()
    {
        var root = new StackPanel { Spacing = 28, Margin = new Thickness(24) };
        root.Children.Add(Header(
            "Date pickers",
            "Docked text field, docked calendar, modal dialog, typed input and full-screen range. " +
            "Arrow keys move focus, PageUp/PageDown change month, Home/End move to the week edges, " +
            "Enter selects and Escape cancels. Text input accepts the culture short date pattern or ISO yyyy-MM-dd."));

        var culturePickers = new List<MaterialDatePicker>();
        var cultureFields = new List<MaterialDatePickerField>();

        var dockedField = BuildDockedField(cultureFields);
        root.Children.Add(Section("Docked input date picker", dockedField));

        var dockedSurface = BuildDockedSurface(culturePickers);
        root.Children.Add(Section("Docked calendar surface", dockedSurface));

        var modal = BuildModalSurface(culturePickers);
        root.Children.Add(Section("Modal and text input", modal));

        var input = BuildInputModes(culturePickers);
        root.Children.Add(Section("Typed input: single and range", input));

        var fullScreen = BuildFullScreenRange(culturePickers);
        root.Children.Add(Section("Full-screen range", fullScreen));

        var bounds = BuildBounds();
        root.Children.Add(Section("Bounds, disabled state and culture", bounds));

        root.Children.Add(Section("Culture", BuildCultureRow(culturePickers, cultureFields)));

        root.Children.Add(Section("Modal dialog", BuildDialogSection()));
        return new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private static Control BuildDockedField(List<MaterialDatePickerField> cultureFields)
    {
        var status = Status();
        var field = new MaterialDatePickerField
        {
            Label = "Departure",
            SupportingText = "Type a date or open the calendar",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(3)),
            MinimumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(-30)),
            MaximumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(90)),
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        field.SelectionCommitted += (_, _) => ShowFieldStatus(status, field);
        field.Opened += (_, _) => status.Text = "Calendar open";
        field.Closed += (_, _) => ShowFieldStatus(status, field);
        ShowFieldStatus(status, field);
        cultureFields.Add(field);

        var disabled = new MaterialDatePickerField
        {
            Label = "Disabled",
            SupportingText = "The trigger and text entry are unavailable",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today),
            IsEnabled = false,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 16 };
        row.Children.Add(Column("Single date", field, status));
        row.Children.Add(Column("Disabled", disabled));
        return row;
    }

    private static Control BuildDockedSurface(List<MaterialDatePicker> culturePickers)
    {
        var status = Status();
        var picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Docked,
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(17)),
            DisplayDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(17)),
            MinimumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(-45)),
            MaximumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(120)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        picker.SelectionChanged += (_, _) => ShowPickerStatus(status, picker);
        ShowPickerStatus(status, picker);
        culturePickers.Add(picker);

        var controls = new StackPanel { Spacing = 8 };
        controls.Children.Add(ViewButton("Day view", picker, MaterialDatePickerView.Day));
        controls.Children.Add(ViewButton("Month list", picker, MaterialDatePickerView.Month));
        controls.Children.Add(ViewButton("Year list", picker, MaterialDatePickerView.Year));
        controls.Children.Add(Hint("Month and year menus open the docked lists; the calendar keeps the selection."));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(picker);
        row.Children.Add(Column("Views", controls, status));
        return row;
    }

    private static Control BuildModalSurface(List<MaterialDatePicker> culturePickers)
    {
        var status = Status();
        var picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            Label = "Select date",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(9)),
            DisplayDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(9)),
            AllowInputMode = true,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        picker.SelectionChanged += (_, _) => ShowPickerStatus(status, picker);
        picker.Confirmed += (_, _) => status.Text = "Confirmed: " + Describe(picker);
        picker.Cancelled += (_, _) => status.Text = "Cancelled";
        picker.Cleared += (_, _) => status.Text = "Cleared";
        ShowPickerStatus(status, picker);
        culturePickers.Add(picker);

        var controls = new StackPanel { Spacing = 8 };
        controls.Children.Add(ActionButton("Edit icon / text input", () => picker.IsInputMode = !picker.IsInputMode));
        controls.Children.Add(ActionButton("Switch to range", () => picker.SelectionMode = picker.SelectionMode == MaterialDateSelectionMode.Range
            ? MaterialDateSelectionMode.Single
            : MaterialDateSelectionMode.Range));
        controls.Children.Add(ActionButton("Clear", picker.ClearSelection));
        controls.Children.Add(ActionButton("Today", picker.GoToToday));
        controls.Children.Add(Hint("The header edit button switches to the typed presentation."));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(picker);
        row.Children.Add(Column("Actions", controls, status));
        return row;
    }

    private static Control BuildInputModes(List<MaterialDatePicker> culturePickers)
    {
        var single = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            IsInputMode = true,
            Label = "Select date",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(2)),
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var singleStatus = Status();
        single.SelectionChanged += (_, _) => ShowPickerStatus(singleStatus, single);
        ShowPickerStatus(singleStatus, single);
        culturePickers.Add(single);

        var range = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            IsInputMode = true,
            SelectionMode = MaterialDateSelectionMode.Range,
            Label = "Depart - Return dates",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(4)),
            SelectedEndDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(11)),
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var rangeStatus = Status();
        range.SelectionChanged += (_, _) => ShowPickerStatus(rangeStatus, range);
        range.Confirmed += (_, _) => rangeStatus.Text = "Confirmed: " + Describe(range);
        ShowPickerStatus(rangeStatus, range);
        culturePickers.Add(range);

        var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 16 };
        row.Children.Add(Column("Single input", single, singleStatus));
        row.Children.Add(Column("Range input", range, rangeStatus));
        return row;
    }

    private static Control BuildFullScreenRange(List<MaterialDatePicker> culturePickers)
    {
        var status = Status();
        var picker = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.FullScreen,
            SelectionMode = MaterialDateSelectionMode.Range,
            MonthCount = 6,
            Label = "Depart - Return dates",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(10)),
            SelectedEndDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(16)),
            DisplayDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(-10)),
            Width = 420,
            Height = 560,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        picker.SelectionChanged += (_, _) => ShowPickerStatus(status, picker);
        picker.Confirmed += (_, _) => status.Text = "Saved: " + Describe(picker);
        picker.Cancelled += (_, _) => status.Text = "Closed without saving";
        ShowPickerStatus(status, picker);
        culturePickers.Add(picker);

        var column = new StackPanel { Spacing = 8, Width = 320 };
        column.Children.Add(status);
        column.Children.Add(Hint("Six consecutive months scroll vertically; the weekday row stays fixed. " +
                                 "Range endpoints use the primary circle and the span uses the secondary container."));
        column.Children.Add(ActionButton("Show today", picker.GoToToday));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(picker);
        row.Children.Add(column);
        return row;
    }

    private static Control BuildBounds()
    {
        var boundsStatus = Status();
        var bounded = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            Label = "Within the next 14 days",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(3)),
            MinimumDate = MaterialDatePicker.ToDateTimeOffset(Today),
            MaximumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(14)),
            AllowInputMode = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        bounded.SelectionChanged += (_, _) => boundsStatus.Text = Describe(bounded) +
            $" (allowed {bounded.MinimumDate?.ToString("d", CultureInfo.CurrentCulture)} - " +
            $"{bounded.MaximumDate?.ToString("d", CultureInfo.CurrentCulture)})";
        boundsStatus.Text = Describe(bounded);

        var disabled = new MaterialDatePicker
        {
            Mode = MaterialDatePickerMode.Modal,
            Label = "Disabled",
            SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today),
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 16 };
        row.Children.Add(Column("Bounded", bounded, boundsStatus));
        row.Children.Add(Column("Disabled", disabled));
        return row;
    }

    private static Control BuildCultureRow(
        List<MaterialDatePicker> pickers,
        List<MaterialDatePickerField> fields)
    {
        var combo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (name, _) in Cultures)
            combo.Items.Add(new ComboBoxItem { Content = name });
        combo.SelectedIndex = 0;

        var sample = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray,
        };
        combo.SelectionChanged += (_, _) =>
        {
            var index = Math.Clamp(combo.SelectedIndex, 0, Cultures.Length - 1);
            var culture = Cultures[index].Culture;
            foreach (var picker in pickers)
                picker.Culture = culture;
            foreach (var field in fields)
                field.Culture = culture;
            sample.Text = "Week starts on " + culture.DateTimeFormat.FirstDayOfWeek +
                          "; short pattern " + culture.DateTimeFormat.ShortDatePattern;
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        row.Children.Add(combo);
        row.Children.Add(sample);
        return row;
    }

    private static Control BuildDialogSection()
    {
        var status = Status();
        status.Text = "The dialog blocks its owner and restores focus when closed.";
        var button = new Button { Content = "Open modal date picker", HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(button) is not Window owner)
            {
                status.Text = "A window owner is required.";
                return;
            }
            var picker = new MaterialDatePicker
            {
                Mode = MaterialDatePickerMode.Modal,
                Label = "Select date",
                SelectedDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(1)),
                MinimumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(-365)),
                MaximumDate = MaterialDatePicker.ToDateTimeOffset(Today.AddDays(365)),
            };
            var result = await MaterialDatePickerDialog.ShowAsync(owner, picker);
            status.Text = result.IsConfirmed
                ? $"Confirmed {Format(result.Start)}" + (result.End is { } end ? $" - {Format(end)}" : string.Empty)
                : "Cancelled";
        };

        var column = new StackPanel { Spacing = 12 };
        column.Children.Add(button);
        column.Children.Add(status);
        return column;
    }

    private static Control Header(string title, string description)
    {
        var stack = new StackPanel { Spacing = 8 };
        var heading = new TextBlock { Text = title };
        MaterialTypography.SetStyle(heading, MaterialTypeStyle.HeadlineMedium);
        var body = new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 820,
            Foreground = Brushes.Gray,
        };
        MaterialTypography.SetStyle(body, MaterialTypeStyle.BodyMedium);
        stack.Children.Add(heading);
        stack.Children.Add(body);
        return stack;
    }

    private static Control Section(string title, Control content)
    {
        var stack = new StackPanel { Spacing = 12 };
        var heading = new TextBlock { Text = title };
        MaterialTypography.SetStyle(heading, MaterialTypeStyle.TitleMedium);
        stack.Children.Add(heading);
        stack.Children.Add(content);
        return stack;
    }

    private static Control Column(string caption, params Control[] content)
    {
        var stack = new StackPanel { Spacing = 8 };
        var label = new TextBlock { Text = caption, Foreground = Brushes.Gray };
        MaterialTypography.SetStyle(label, MaterialTypeStyle.LabelMedium);
        stack.Children.Add(label);
        foreach (var child in content)
            stack.Children.Add(child);
        return stack;
    }

    private static TextBlock Status() => new()
    {
        Text = string.Empty,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Gray,
        MaxWidth = 420,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 320,
        Foreground = Brushes.Gray,
        FontSize = 12,
    };

    private static Button ActionButton(string content, Action action)
    {
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button ViewButton(string content, MaterialDatePicker picker, MaterialDatePickerView view)
    {
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        button.Click += (_, _) => picker.View = view;
        return button;
    }

    private static void ShowPickerStatus(TextBlock target, MaterialDatePicker picker) =>
        target.Text = Describe(picker);

    private static void ShowFieldStatus(TextBlock target, MaterialDatePickerField field) =>
        target.Text = field.SelectedDate is { } value
            ? "Selected " + Format(value)
            : "No date selected";

    private static string Describe(MaterialDatePicker picker)
    {
        if (picker.SelectedDate is not { } start)
            return "No date selected";
        if (picker.SelectionMode == MaterialDateSelectionMode.Range && picker.SelectedEndDate is { } end)
            return $"Range {Format(start)} - {Format(end)}";
        return "Selected " + Format(start);
    }

    private static string Format(DateTimeOffset? value) =>
        value is { } date ? date.Date.ToString("d", CultureInfo.CurrentCulture) : "no date";
}
