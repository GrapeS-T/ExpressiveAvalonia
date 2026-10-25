using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Source-family gallery for the standard, centered and range sliders.</summary>
public static class SlidersGallery
{
    public static Control Create()
    {
        var pages = new TabControl();
        pages.Items.Add(new TabItem { Header = "Sizes", Content = Scroll(Sizes()) });
        pages.Items.Add(new TabItem { Header = "Centered", Content = Scroll(Centered()) });
        pages.Items.Add(new TabItem { Header = "Range", Content = Scroll(Ranges()) });
        pages.Items.Add(new TabItem { Header = "States", Content = Scroll(States()) });
        return pages;
    }

    private static StackPanel Panel() => new() { Spacing = 20, Margin = new Thickness(24) };
    private static Control Scroll(Control content) => new ScrollViewer
    {
        Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    private static TextBlock Caption(string text, int size = 16) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = FontWeight.Medium,
    };

    private static Slider Standard(MaterialControlSize size, double value, double width = 320)
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = value, Width = width };
        MaterialAssist.SetControlSize(slider, size);
        return slider;
    }

    private static Slider CenteredSlider(MaterialControlSize size, double value, double width = 320)
    {
        var slider = new Slider { Minimum = -100, Maximum = 100, Value = value, Width = width };
        MaterialAssist.SetControlSize(slider, size);
        MaterialSlider.SetIsCentered(slider, true);
        return slider;
    }

    private static MaterialRangeSlider Range(MaterialControlSize size, double lower, double upper, double width = 320)
    {
        var slider = new MaterialRangeSlider
        {
            Minimum = 0,
            Maximum = 100,
            LowerValue = lower,
            UpperValue = upper,
            Width = width,
        };
        MaterialAssist.SetControlSize(slider, size);
        return slider;
    }

    private static Control Sizes()
    {
        var panel = Panel();
        panel.Children.Add(Caption("Standard slider · five source sizes", 24));
        foreach (var size in Enum.GetValues<MaterialControlSize>())
        {
            panel.Children.Add(Caption($"{size} · track {TrackThickness(size)} DIP", 14));
            var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 12 };
            foreach (var value in new[] { 0d, 50d, 100d })
                row.Children.Add(Standard(size, value));
            panel.Children.Add(row);
        }

        panel.Children.Add(Caption("Vertical orientation", 20));
        var vertical = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 28 };
        foreach (var size in Enum.GetValues<MaterialControlSize>())
        {
            var slider = Standard(size, 60, 120);
            slider.Orientation = Orientation.Vertical;
            slider.Height = 200;
            slider.Width = double.NaN;
            vertical.Children.Add(slider);
        }
        panel.Children.Add(vertical);
        return panel;
    }

    private static string TrackThickness(MaterialControlSize size) => size switch
    {
        MaterialControlSize.ExtraSmall => "16",
        MaterialControlSize.Small => "24",
        MaterialControlSize.Medium => "40",
        MaterialControlSize.Large => "56",
        MaterialControlSize.ExtraLarge => "96",
        _ => "40",
    };

    private static Control Centered()
    {
        var panel = Panel();
        panel.Children.Add(Caption("Centered slider · fill grows from the zero point", 24));
        foreach (var size in Enum.GetValues<MaterialControlSize>())
        {
            panel.Children.Add(Caption($"{size} · values -50 / 0 / +50", 14));
            var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 12 };
            foreach (var value in new[] { -50d, 0d, 50d })
                row.Children.Add(CenteredSlider(size, value));
            panel.Children.Add(row);
        }

        panel.Children.Add(Caption("Vertical centered", 20));
        var vertical = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 28 };
        foreach (var value in new[] { -50d, 0d, 50d })
        {
            var slider = CenteredSlider(MaterialControlSize.Medium, value, 120);
            slider.Orientation = Orientation.Vertical;
            slider.Height = 220;
            slider.Width = double.NaN;
            vertical.Children.Add(slider);
        }
        panel.Children.Add(vertical);
        return panel;
    }

    private static Control Ranges()
    {
        var panel = Panel();
        panel.Children.Add(Caption("Range slider · independent lower/upper handles", 24));
        var live = new TextBlock { Text = "Drag either handle", FontSize = 14 };
        foreach (var size in Enum.GetValues<MaterialControlSize>())
        {
            panel.Children.Add(Caption($"{size} · coincident, -50 and +50 snapshots", 14));
            var row = new WrapPanel { ItemSpacing = 24, LineSpacing = 12 };
            row.Children.Add(Range(size, 50, 50));
            row.Children.Add(Range(size, 21.5, 50));
            row.Children.Add(Range(size, 50, 77.4));
            panel.Children.Add(row);
        }

        var interactive = Range(MaterialControlSize.Medium, 20, 80);
        interactive.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialRangeSlider.LowerValueProperty ||
                e.Property == MaterialRangeSlider.UpperValueProperty)
                live.Text = $"Lower {interactive.LowerValue:0.##} · Upper {interactive.UpperValue:0.##}";
        };
        panel.Children.Add(Caption("Live values · keyboard: arrows, PageUp/PageDown, Home/End", 14));
        panel.Children.Add(interactive);
        panel.Children.Add(live);
        return panel;
    }

    private static Control States()
    {
        var panel = Panel();
        panel.Children.Add(Caption("Stop indicators · source's eleven stops", 20));
        var stops = Standard(MaterialControlSize.Medium, 40);
        MaterialSlider.SetShowStops(stops, true);
        panel.Children.Add(stops);

        var rangeStops = Range(MaterialControlSize.Medium, 20, 70);
        MaterialSlider.SetShowStops(rangeStops, true);
        panel.Children.Add(rangeStops);

        panel.Children.Add(Caption("Leading icon slot · source default volume_up", 20));
        var icon = Standard(MaterialControlSize.ExtraLarge, 50);
        MaterialSlider.SetShowIcon(icon, true);
        panel.Children.Add(icon);

        panel.Children.Add(Caption("Disabled", 20));
        var disabled = Standard(MaterialControlSize.Medium, 50);
        disabled.IsEnabled = false;
        panel.Children.Add(disabled);
        var disabledRange = Range(MaterialControlSize.Medium, 20, 80);
        disabledRange.IsEnabled = false;
        panel.Children.Add(disabledRange);

        panel.Children.Add(Caption("Reversed direction", 20));
        var reversed = Standard(MaterialControlSize.Medium, 50);
        reversed.IsDirectionReversed = true;
        panel.Children.Add(reversed);

        panel.Children.Add(Caption("Right-to-left flow", 20));
        var rtl = Standard(MaterialControlSize.Medium, 30);
        rtl.FlowDirection = FlowDirection.RightToLeft;
        panel.Children.Add(rtl);

        panel.Children.Add(Caption("Keyboard · Tab to focus, arrows/PageUp/PageDown/Home/End", 20));
        var keyboard = Standard(MaterialControlSize.Large, 35);
        MaterialSlider.SetShowStops(keyboard, true);
        keyboard.TickFrequency = 10;
        keyboard.IsSnapToTickEnabled = true;
        panel.Children.Add(keyboard);
        return panel;
    }
}
