using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Source families: Linear-determinate progress indicator (local-003548),
/// Linear-indeterminate (local-003573), Circular-determinate (local-001278),
/// Circular-indeterminate (local-001303), Loading indicator (local-004296) and the
/// width-4/width-8 progress building blocks (local-000544…000555, local-000699).
/// </summary>
public static class ProgressGallery
{
    public static Control Create()
    {
        var status = new TextBlock { Text = "Прогресс: 50%", FontSize = 16 };
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };

        panel.Children.Add(Heading("Linear-determinate progress indicator · local-003548"));
        panel.Children.Add(Note("Оси: Type (Flat/Wave) × Thickness (4 dp / 8 dp) × Progress (0/10/20/50/80/100). " +
                                "ProgressBar остаётся нативным: клавиатура, AutomationProperties и диапазон Minimum/Maximum " +
                                "работают штатно, а Type/Thickness/Wave height/Wavelength задаются присоединёнными " +
                                "свойствами MaterialProgress."));

        var interactive = Bar(ProgressIndicatorType.Wave, 4, 50);
        interactive.Width = 320;
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50, Width = 320, TickFrequency = 10 };
        slider.PropertyChanged += (_, change) =>
        {
            if (change.Property != Slider.ValueProperty) return;
            interactive.Value = slider.Value;
            status.Text = $"Прогресс: {slider.Value:0}%";
        };
        panel.Children.Add(Row(slider, interactive, status));

        panel.Children.Add(Subheading("Flat · 4 dp"));
        panel.Children.Add(BarRow(ProgressIndicatorType.Flat, 4));
        panel.Children.Add(Subheading("Flat · 8 dp"));
        panel.Children.Add(BarRow(ProgressIndicatorType.Flat, 8));
        panel.Children.Add(Subheading("Wave · 4 dp · wavelength 40"));
        panel.Children.Add(BarRow(ProgressIndicatorType.Wave, 4));
        panel.Children.Add(Subheading("Wave · 8 dp · wavelength 40 (полоса растёт до 13.5 dp, как в источнике)"));
        panel.Children.Add(BarRow(ProgressIndicatorType.Wave, 8));
        panel.Children.Add(Subheading("Wave · 4 dp · wavelength 20 (Segment - wave building block local-000545)"));
        panel.Children.Add(BarRow(ProgressIndicatorType.Wave, 4, wavelength: 20));
        panel.Children.Add(Subheading("Wave · right-to-left flow"));
        var rtl = Bar(ProgressIndicatorType.Wave, 4, 60);
        rtl.Width = 404;
        rtl.FlowDirection = FlowDirection.RightToLeft;
        panel.Children.Add(rtl);

        panel.Children.Add(Heading("Linear-indeterminate progress indicator · local-003573"));
        panel.Children.Add(Note("Оси: Type × Thickness × Step. Step — кадры одного и того же цикла; " +
                                "непрерывная анимация показывает их последовательность. При reduced motion " +
                                "индикатор останавливается на устойчивом незавершённом кадре."));
        panel.Children.Add(Row(
            Indeterminate(ProgressIndicatorType.Flat, 4),
            Indeterminate(ProgressIndicatorType.Flat, 8),
            Indeterminate(ProgressIndicatorType.Wave, 4),
            Indeterminate(ProgressIndicatorType.Wave, 8)));

        panel.Children.Add(Heading("Circular-determinate progress indicator · local-001278"));
        panel.Children.Add(Note("Оси: Type (Flat/Wave) × Thickness (4 dp / 8 dp) × Progress (0/10/30/50/80/100). " +
                                "Волнистый вариант снят из design.json: центр осевой линии 20.55 при амплитуде 1.5 " +
                                "на артборде 48 (гребень касается осевой линии дорожки), 9 лепестков с шагом 40°, " +
                                "фаза начинается с гребня. Дорожка остаётся ровным кольцом, как в источнике."));
        panel.Children.Add(CircularRow(ProgressIndicatorType.Flat, 4));
        panel.Children.Add(CircularRow(ProgressIndicatorType.Flat, 8));
        panel.Children.Add(CircularRow(ProgressIndicatorType.Wave, 4));
        panel.Children.Add(CircularRow(ProgressIndicatorType.Wave, 8));

        panel.Children.Add(Subheading("Wave · 4 dp · 6 лепестков (WaveLobes) для проверки оси"));
        var fewerLobes = Row(
            Circular(ProgressIndicatorType.Wave, 4, 25),
            Circular(ProgressIndicatorType.Wave, 4, 50),
            Circular(ProgressIndicatorType.Wave, 4, 75),
            Circular(ProgressIndicatorType.Wave, 4, 100));
        foreach (var child in fewerLobes.Children)
            if (child is CircularProgressIndicator circle)
                circle.WaveLobes = 6;
        panel.Children.Add(fewerLobes);

        panel.Children.Add(Heading("Circular-indeterminate progress indicator · local-001303"));
        panel.Children.Add(Note("Оси: Type × Thickness × Step (1/2). Дорожка видна только при Show track; " +
                                "недетерминированное значение не сообщается скринридеру как проценты."));
        panel.Children.Add(Row(
            Circular(ProgressIndicatorType.Flat, 4, null),
            Circular(ProgressIndicatorType.Flat, 8, null),
            Circular(ProgressIndicatorType.Wave, 4, null),
            Circular(ProgressIndicatorType.Wave, 8, null)));
        panel.Children.Add(Note("Show track=False убирает кольцо-дорожку, оставляя только активную дугу."));
        var noTrack = Circular(ProgressIndicatorType.Wave, 4, null);
        noTrack.ShowTrack = false;
        var noTrackFlat = Circular(ProgressIndicatorType.Flat, 4, null);
        noTrackFlat.ShowTrack = false;
        panel.Children.Add(Row(noTrack, noTrackFlat));

        panel.Children.Add(Heading("Loading indicator · local-004296"));
        panel.Children.Add(Note("Оси: Steps (1–7) × Show container (False/True). Семь исходных силуэтов " +
                                "перенесены точными путями из design.json: 12-лучёвая звезда, сглаженная звезда, " +
                                "пятиугольник, скруглённый квадрат, 8-лучёвая звезда, вогнутая 4-лучёвая звезда и " +
                                "почти круг. Анимация плавно сменяет силуэты и поворачивает их; reduced motion " +
                                "оставляет выбранный шаг неподвижным."));
        var steps = new WrapPanel { ItemSpacing = 16, LineSpacing = 8 };
        for (var step = 1; step <= 7; step++)
            steps.Children.Add(new LoadingIndicator { Step = step, IsAnimated = false, ShowContainer = true });
        panel.Children.Add(steps);
        panel.Children.Add(Note("Те же шаги без контейнера (Show container=False)."));
        var bare = new WrapPanel { ItemSpacing = 16, LineSpacing = 8 };
        for (var step = 1; step <= 7; step++)
            bare.Children.Add(new LoadingIndicator { Step = step, IsAnimated = false });
        panel.Children.Add(bare);
        panel.Children.Add(Note("Анимированные индикаторы: с контейнером и без, крупный вариант 96 dp."));
        panel.Children.Add(Row(
            new LoadingIndicator { IsAnimated = true },
            new LoadingIndicator { IsAnimated = true, ShowContainer = true },
            new LoadingIndicator { IsAnimated = true, ShowContainer = true, Width = 96, Height = 96 }));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control BarRow(ProgressIndicatorType type, double thickness, double wavelength = 40)
    {
        var row = Row(
            Bar(type, thickness, 0, wavelength),
            Bar(type, thickness, 10, wavelength),
            Bar(type, thickness, 20, wavelength),
            Bar(type, thickness, 50, wavelength),
            Bar(type, thickness, 80, wavelength),
            Bar(type, thickness, 100, wavelength));
        row.Orientation = Orientation.Vertical;
        row.Spacing = 12;
        return row;
    }

    private static ProgressBar Bar(ProgressIndicatorType type, double thickness, double value, double wavelength = 40)
    {
        var bar = new ProgressBar
        {
            Value = value,
            Width = 404,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        MaterialProgress.SetIndicatorType(bar, type);
        MaterialProgress.SetThickness(bar, thickness);
        MaterialProgress.SetWavelength(bar, wavelength);
        return bar;
    }

    private static ProgressBar Indeterminate(ProgressIndicatorType type, double thickness)
    {
        var bar = new ProgressBar { IsIndeterminate = true, Width = 404 };
        MaterialProgress.SetIndicatorType(bar, type);
        MaterialProgress.SetThickness(bar, thickness);
        return bar;
    }

    private static Control CircularRow(ProgressIndicatorType type, double thickness)
    {
        var row = Row(
            Circular(type, thickness, 0),
            Circular(type, thickness, 10),
            Circular(type, thickness, 30),
            Circular(type, thickness, 50),
            Circular(type, thickness, 80),
            Circular(type, thickness, 100));
        row.VerticalAlignment = VerticalAlignment.Center;
        return row;
    }

    private static CircularProgressIndicator Circular(ProgressIndicatorType type, double thickness, double? value)
    {
        var indicator = new CircularProgressIndicator
        {
            IndicatorType = type,
            StrokeThickness = thickness,
            Width = 48,
            Height = 48,
        };
        if (value is { } determined)
            indicator.Value = determined;
        else
            indicator.IsIndeterminate = true;
        return indicator;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeight.Medium,
    };

    private static TextBlock Subheading(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeight.Medium,
    };

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 760 };

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }
}
