using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Shared Material 3 Expressive inspector primitives for the 380 dp right settings column.
/// Keeps field labels tightly paired with their controls, aligns toggle switches on a clean
/// trailing axis, pairs sliders with live value readouts, and automatically mirrors child
/// visibility/enabled state onto field wrappers so hidden controls never leave orphaned labels.
/// </summary>
internal static class InspectorLayout
{
    public static StackPanel CreatePane(double spacing = 14) => new() { Spacing = spacing };

    public static StackPanel Section(
        StackPanel host,
        string? title = null,
        bool divider = true,
        double spacing = 10)
    {
        var section = new StackPanel { Spacing = spacing };
        if (divider)
        {
            section.Children.Add(new MaterialDivider
            {
                Margin = new Thickness(0, 2, 0, 2),
            });
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var heading = new TextBlock
            {
                Text = title,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2),
            };
            MaterialTypography.SetStyle(heading, MaterialTypeStyle.TitleSmall);
            heading.Bind(TextBlock.ForegroundProperty, heading.GetResourceObservable("MaterialPrimaryBrush"));
            AutomationProperties.SetHeadingLevel(heading, 3);
            section.Children.Add(heading);
        }

        host.Children.Add(section);
        return section;
    }

    public static StackPanel Group(StackPanel host, double spacing = 10)
    {
        var group = new StackPanel { Spacing = spacing };
        host.Children.Add(group);
        return group;
    }

    public static StackPanel Switches(StackPanel host) => Group(host, spacing: 0);

    public static TextBlock Caption(string text)
    {
        var caption = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
        };
        MaterialTypography.SetStyle(caption, MaterialTypeStyle.LabelMedium);
        caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        return caption;
    }

    public static T Field<T>(StackPanel host, string label, T control) where T : Control
    {
        var wrapper = new StackPanel { Spacing = 6 };
        var caption = Caption(label);
        wrapper.Children.Add(caption);
        wrapper.Children.Add(control);

        if (string.IsNullOrEmpty(AutomationProperties.GetName(control)))
            AutomationProperties.SetName(control, label);

        SyncWrapperState(wrapper, control, caption);
        host.Children.Add(wrapper);
        return control;
    }

    public static MaterialButtonGroup Choice(
        StackPanel host,
        string label,
        IReadOnlyList<string> options,
        int selected = 0)
    {
        var compact = options.Count >= 4;
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = compact ? MaterialControlSize.ExtraSmall : MaterialControlSize.Small,
            Density = compact ? 0 : -1,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });

        if (options.Count > 0)
            group.SelectedIndex = Math.Clamp(selected, 0, options.Count - 1);

        return Field(host, label, group);
    }

    public static ComboBox Combo(
        StackPanel host,
        string label,
        IReadOnlyList<string> options,
        int selected = 0) =>
        ComboCore(host, label, options, selected);

    public static ComboBox Combo(
        StackPanel host,
        string label,
        System.Collections.IEnumerable options,
        int selected = 0) =>
        ComboCore(host, label, options, selected);

    private static ComboBox ComboCore(
        StackPanel host,
        string label,
        System.Collections.IEnumerable options,
        int selected)
    {
        var combo = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = selected,
            MinHeight = 44,
            Padding = new Thickness(14, 8),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return Field(host, label, combo);
    }

    public static ComboBox Combo(StackPanel host, string label)
    {
        var combo = new ComboBox
        {
            MinHeight = 44,
            Padding = new Thickness(14, 8),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        return Field(host, label, combo);
    }

    public static TextBox TextField(
        StackPanel host,
        string label,
        string value = "",
        bool multiline = false)
    {
        var box = new TextBox
        {
            Text = value,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 84 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        MaterialTextField.SetVariant(box, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(box, label);
        AutomationProperties.SetName(box, label);
        host.Children.Add(box);
        return box;
    }

    public static ToggleSwitch Switch(StackPanel host, string label, bool isChecked = false)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            MinHeight = 40,
            Background = Brushes.Transparent,
        };

        var caption = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4),
        };
        MaterialTypography.SetStyle(caption, MaterialTypeStyle.BodyMedium);
        caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MaterialOnSurfaceBrush"));

        var toggle = new ToggleSwitch
        {
            Content = null,
            OnContent = null,
            OffContent = null,
            IsChecked = isChecked,
            MinHeight = 40,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        AutomationProperties.SetName(toggle, label);
        Grid.SetColumn(toggle, 1);

        caption.PointerPressed += (_, e) =>
        {
            if (!toggle.IsEffectivelyEnabled || !e.GetCurrentPoint(caption).Properties.IsLeftButtonPressed)
                return;
            toggle.IsChecked = !(toggle.IsChecked == true);
            e.Handled = true;
        };

        row.Children.Add(caption);
        row.Children.Add(toggle);

        SyncWrapperState(row, toggle, caption);
        host.Children.Add(row);
        return toggle;
    }

    public static Slider Slider(
        StackPanel host,
        string label,
        double min,
        double max,
        double value,
        double step = 1,
        string unit = "",
        string format = "0.##",
        bool snapToTick = true)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step > 0 ? step : Math.Max((max - min) / 20, 0.01),
            IsSnapToTickEnabled = snapToTick,
        };
        MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(slider, false);
        AutomationProperties.SetName(slider, label);

        AttachSliderRow(host, label, slider, v => FormatValue(v, format, unit));
        return slider;
    }

    public static Slider CustomSlider(
        StackPanel host,
        string label,
        double min,
        double max,
        double value,
        double step,
        Func<double, string> formatValue,
        bool snapToTick = true)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step > 0 ? step : Math.Max((max - min) / 20, 0.01),
            IsSnapToTickEnabled = snapToTick,
        };
        MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(slider, false);
        AutomationProperties.SetName(slider, label);

        AttachSliderRow(host, label, slider, formatValue);
        return slider;
    }

    public static (Slider Slider, TextBlock ValueText) SliderWithReadout(
        StackPanel host,
        string label,
        double min,
        double max,
        double value,
        double step = 1,
        string unit = "",
        string format = "0.##",
        bool snapToTick = true)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = step > 0 ? step : Math.Max((max - min) / 20, 0.01),
            IsSnapToTickEnabled = snapToTick,
        };
        MaterialAssist.SetControlSize(slider, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(slider, false);
        AutomationProperties.SetName(slider, label);

        var readout = AttachSliderRow(host, label, slider, v => FormatValue(v, format, unit));
        return (slider, readout);
    }

    public static Button ActionButton(
        StackPanel host,
        string content,
        Action onClick,
        MaterialButtonVariant variant = MaterialButtonVariant.Tonal,
        string? iconName = null)
    {
        var button = CreateActionButton(content, onClick, variant, iconName);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        host.Children.Add(button);
        return button;
    }

    public static Button CreateActionButton(
        string content,
        Action onClick,
        MaterialButtonVariant variant = MaterialButtonVariant.Tonal,
        string? iconName = null)
    {
        var button = new Button { Content = content };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        if (!string.IsNullOrWhiteSpace(iconName))
        {
            MaterialAssist.SetIcon(button, new MaterialIcon { IconName = iconName, IsDecorative = true });
            MaterialAssist.SetShowIcon(button, true);
        }
        AutomationProperties.SetName(button, content);
        button.Click += (_, _) => onClick();
        return button;
    }

    public static TextBlock Note(StackPanel host, string text, bool divider = true)
    {
        var wrapper = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        if (divider)
            wrapper.Children.Add(new MaterialDivider());

        var note = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
        };
        MaterialTypography.SetStyle(note, MaterialTypeStyle.BodySmall);
        note.Bind(TextBlock.ForegroundProperty, note.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        wrapper.Children.Add(note);
        host.Children.Add(wrapper);
        return note;
    }

    public static TextBlock Readout(StackPanel host, string initialText = "")
    {
        var block = new TextBlock
        {
            Text = initialText,
            TextWrapping = TextWrapping.Wrap,
        };
        MaterialTypography.SetStyle(block, MaterialTypeStyle.LabelMedium);
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        host.Children.Add(block);
        return block;
    }

    /// <summary>
    /// Applies RTL when <paramref name="rtlToggle"/> is checked, and clears the local value when
    /// unchecked so the Studio Settings global RTL switch continues to inherit cleanly.
    /// </summary>
    public static void ApplyFlowDirection(Visual target, ToggleSwitch rtlToggle)
    {
        if (rtlToggle.IsChecked == true)
            target.FlowDirection = FlowDirection.RightToLeft;
        else
            target.ClearValue(Visual.FlowDirectionProperty);
    }

    /// <summary>
    /// Applies local reduced-motion override only when active, preserving global/OS reduced motion
    /// inheritance when unchecked.
    /// </summary>
    public static void ApplyReducedMotion(Control target, bool forceReduced)
    {
        if (forceReduced)
            MaterialMotion.SetIsReduced(target, true);
        else
            target.ClearValue(MaterialMotion.IsReducedProperty);
    }

    private static TextBlock AttachSliderRow(
        StackPanel host,
        string label,
        Slider slider,
        Func<double, string> formatValue)
    {
        var wrapper = new StackPanel { Spacing = 2 };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };

        var caption = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        MaterialTypography.SetStyle(caption, MaterialTypeStyle.BodyMedium);
        caption.Bind(TextBlock.ForegroundProperty, caption.GetResourceObservable("MaterialOnSurfaceBrush"));

        var valueText = new TextBlock
        {
            Text = formatValue(slider.Value),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        MaterialTypography.SetStyle(valueText, MaterialTypeStyle.LabelLarge);
        valueText.Bind(TextBlock.ForegroundProperty, valueText.GetResourceObservable("MaterialPrimaryBrush"));
        Grid.SetColumn(valueText, 1);

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                valueText.Text = formatValue(slider.Value);
        };

        header.Children.Add(caption);
        header.Children.Add(valueText);
        wrapper.Children.Add(header);
        wrapper.Children.Add(slider);

        SyncWrapperState(wrapper, slider, caption, valueText);
        host.Children.Add(wrapper);
        return valueText;
    }

    private static string FormatValue(double value, string format, string unit)
    {
        var number = value.ToString(format);
        if (string.IsNullOrEmpty(unit))
            return number;
        return unit is "%" or "×" ? $"{number}{unit}" : $"{number} {unit}";
    }

    private static void SyncWrapperState(
        Control wrapper,
        Control control,
        Control caption,
        Control? secondary = null)
    {
        void SyncEnabled()
        {
            var opacity = control.IsEnabled ? 1.0 : 0.45;
            caption.Opacity = opacity;
            if (secondary is not null)
                secondary.Opacity = opacity;
        }

        wrapper.IsVisible = control.IsVisible;
        SyncEnabled();

        control.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty)
                wrapper.IsVisible = control.IsVisible;
            else if (e.Property == InputElement.IsEnabledProperty)
                SyncEnabled();
        };
    }
}
