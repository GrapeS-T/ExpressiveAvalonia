using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Catalog;

internal sealed class CatalogInspector : ScrollViewer
{
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private static readonly (string Name, Color Color)[] SeedPresets =
    [
        ("Baseline Purple", Color.Parse("#6750A4")),
        ("Emerald", Color.Parse("#006C4C")),
        ("Ocean Blue", Color.Parse("#0061A4")),
        ("Warm Amber", Color.Parse("#8B5000")),
        ("Crimson Rose", Color.Parse("#9C4146")),
        ("Olive Moss", Color.Parse("#4A672D")),
    ];

    private readonly StackPanel _settings = new() { Spacing = 16, Margin = new Thickness(16, 16, 16, 24) };
    private readonly StackPanel _pageSettings = new() { Spacing = 14 };
    private readonly StackPanel _resetFooter = new() { Spacing = 10, IsVisible = false };
    internal StackPanel StudioSettings { get; } = new() { Spacing = 16, Margin = new Thickness(24) };
    private readonly Action? _resetPage;
    private bool _previewEnabled = true;
    private FlowDirection _previewFlow;
    private double _previewWidth = double.NaN;
    private double _previewHeight;
    private Control? _page;
    private Action? _restorePage;

    internal CatalogInspector(MaterialTheme theme, Window window, Action? resetPage = null)
    {
        _resetPage = resetPage;
        Content = _settings;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AllowAutoHide = false;

        _settings.Children.Add(_pageSettings);

        if (_resetPage is not null)
        {
            _resetFooter.Children.Add(new MaterialDivider());
            InspectorLayout.ActionButton(
                _resetFooter,
                "Сбросить по умолчанию",
                () => _resetPage(),
                MaterialButtonVariant.Outlined,
                iconName: "undo");
            _settings.Children.Add(_resetFooter);
        }

        BuildStudioSettings(theme, window);
    }

    private sealed record OriginalButtonSize(MaterialControlSize Size);

    internal void SetPage(Control page)
    {
        _restorePage?.Invoke();
        _restorePage = null;
        _page = page;
        _pageSettings.Children.Clear();
        Offset = default;
        page.Width = _previewWidth;
        page.MinHeight = _previewHeight;
        page.IsEnabled = _previewEnabled;
        if (_previewFlow == FlowDirection.RightToLeft)
            page.FlowDirection = FlowDirection.RightToLeft;
        else
            page.ClearValue(FlowDirectionProperty);

        var descendants = page.GetLogicalDescendants().OfType<Control>().Prepend(page).ToArray();
        if (descendants.OfType<IComponentPlayground>().FirstOrDefault() is { } playground)
        {
            _pageSettings.Children.Add(playground.Settings);
            _resetFooter.IsVisible = _resetPage is not null;
            return;
        }

        _resetFooter.IsVisible = false;
        var hasButtons = descendants.OfType<Button>().Any(b => b is not MaterialFabBase) || descendants.OfType<ViewportGallery>().Any();
        if (hasButtons)
        {
            var section = InspectorLayout.Section(_pageSettings, "Размер кнопок", divider: false);
            var size = InspectorLayout.Combo(
                section,
                "ControlSize",
                new[] { "Как в примере", "ExtraSmall", "Small", "Medium", "Large", "ExtraLarge" },
                0);
            var originals = new ConditionalWeakTable<Button, OriginalButtonSize>();
            void ApplySize()
            {
                var selected = size.SelectedIndex <= 0
                    ? (MaterialControlSize?)null
                    : Enum.Parse<MaterialControlSize>((string)size.SelectedItem!);
                page.SetValue(ViewportGallery.SizeOverrideProperty, selected);
                foreach (var button in page.GetLogicalDescendants().OfType<Button>().Where(b => b is not MaterialFabBase))
                {
                    var original = originals.GetValue(button, b => new OriginalButtonSize(MaterialAssist.GetControlSize(b)));
                    MaterialAssist.SetControlSize(button, selected ?? original.Size);
                }
            }
            _restorePage = () => page.ClearValue(ViewportGallery.SizeOverrideProperty);
            size.SelectionChanged += (_, _) => ApplySize();
        }
    }

    private void BuildStudioSettings(MaterialTheme theme, Window window)
    {
        var syncing = false;

        var appearance = InspectorLayout.Section(StudioSettings, "Тема и цвет", divider: false);
        var dark = InspectorLayout.Switch(appearance, "Тёмная тема", window.ActualThemeVariant == ThemeVariant.Dark);
        dark.IsCheckedChanged += (_, _) =>
        {
            if (!syncing)
                window.RequestedThemeVariant = dark.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
        };
        window.ActualThemeVariantChanged += (_, _) =>
        {
            syncing = true;
            dark.IsChecked = window.ActualThemeVariant == ThemeVariant.Dark;
            syncing = false;
        };

        var swatchBar = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        var swatchRings = new Border[SeedPresets.Length];
        void SyncSeedSwatches()
        {
            for (var i = 0; i < SeedPresets.Length; i++)
            {
                var active = SeedPresets[i].Color == theme.SeedColor;
                swatchRings[i].BorderThickness = active ? new Thickness(2.5) : new Thickness(1);
                swatchRings[i].Opacity = active ? 1.0 : 0.82;
            }
        }

        for (var i = 0; i < SeedPresets.Length; i++)
        {
            var preset = SeedPresets[i];
            var dot = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(preset.Color),
            };
            var ring = new Border
            {
                Padding = new Thickness(3),
                CornerRadius = new CornerRadius(999),
                Child = dot,
            };
            ring.Bind(Border.BorderBrushProperty, ring.GetResourceObservable("MaterialPrimaryBrush"));
            swatchRings[i] = ring;

            var swatchButton = new Button
            {
                Content = ring,
                Padding = new Thickness(0),
                MinWidth = 40,
                MinHeight = 40,
                Background = Brushes.Transparent,
            };
            MaterialAssist.SetButtonVariant(swatchButton, MaterialButtonVariant.Text);
            MaterialAssist.SetIsIconButton(swatchButton, true);
            ToolTip.SetTip(swatchButton, $"{preset.Name} (#{preset.Color.R:X2}{preset.Color.G:X2}{preset.Color.B:X2})");
            AutomationProperties.SetName(swatchButton, $"Акцент {preset.Name}");
            swatchButton.Click += (_, _) => theme.SeedColor = preset.Color;
            swatchBar.Children.Add(swatchButton);
        }
        SyncSeedSwatches();
        InspectorLayout.Field(appearance, "Акцентный цвет (SeedColor)", swatchBar);

        var contrast = InspectorLayout.Choice(
            appearance,
            "Контрастность",
            new[] { "Обычная", "Средняя", "Высокая" },
            theme.Contrast >= 0.75 ? 2 : theme.Contrast >= 0.25 ? 1 : 0);
        contrast.SelectionChanged += (_, _) =>
        {
            if (syncing) return;
            theme.Contrast = contrast.SelectedIndex switch
            {
                1 => 0.5,
                2 => 1.0,
                _ => 0.0,
            };
        };

        var motion = InspectorLayout.Section(StudioSettings, "Движение (Motion)");
        var motionScheme = InspectorLayout.Choice(
            motion,
            "Схема пружин",
            new[] { "Expressive", "Standard" },
            theme.MotionScheme == MaterialMotionScheme.Expressive ? 0 : 1);
        motionScheme.SelectionChanged += (_, _) =>
        {
            if (syncing) return;
            theme.MotionScheme = motionScheme.SelectedIndex == 0
                ? MaterialMotionScheme.Expressive
                : MaterialMotionScheme.Standard;
        };

        var reduced = InspectorLayout.Switch(motion, "Меньше движения (Reduced motion)", theme.IsReducedMotion);
        reduced.IsCheckedChanged += (_, _) =>
        {
            if (!syncing)
                theme.IsReducedMotion = reduced.IsChecked == true;
        };

        theme.PropertyChanged += (_, e) =>
        {
            syncing = true;
            if (e.Property == MaterialTheme.SeedColorProperty)
                SyncSeedSwatches();
            else if (e.Property == MaterialTheme.ContrastProperty)
                contrast.SelectedIndex = theme.Contrast >= 0.75 ? 2 : theme.Contrast >= 0.25 ? 1 : 0;
            else if (e.Property == MaterialTheme.MotionSchemeProperty)
                motionScheme.SelectedIndex = theme.MotionScheme == MaterialMotionScheme.Expressive ? 0 : 1;
            else if (e.Property == MaterialTheme.IsReducedMotionProperty)
                reduced.IsChecked = theme.IsReducedMotion;
            syncing = false;
        };

        var preview = InspectorLayout.Section(StudioSettings, "Область примера");
        var autoWidth = InspectorLayout.Switch(preview, "Автоматическая ширина", true);
        var (width, widthValue) = InspectorLayout.SliderWithReadout(
            preview, "Ширина области", 360, 1440, 840, 40, "dp");
        width.IsEnabled = false;
        widthValue.Text = "авто";

        void ApplyWidth()
        {
            var auto = autoWidth.IsChecked == true;
            width.IsEnabled = !auto;
            _previewWidth = auto ? double.NaN : width.Value;
            if (_page is not null) _page.Width = _previewWidth;
            widthValue.Text = auto ? "авто" : $"{width.Value:0} dp";
        }
        autoWidth.IsCheckedChanged += (_, _) => ApplyWidth();
        width.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) ApplyWidth(); };

        var height = InspectorLayout.Slider(preview, "Минимальная высота", 0, 1000, 0, 50, "dp");
        height.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            _previewHeight = height.Value;
            if (_page is not null) _page.MinHeight = _previewHeight;
        };

        var stateSwitches = InspectorLayout.Switches(preview);
        var enabled = InspectorLayout.Switch(stateSwitches, "Примеры доступны", true);
        enabled.IsCheckedChanged += (_, _) =>
        {
            _previewEnabled = enabled.IsChecked == true;
            if (_page is not null) _page.IsEnabled = _previewEnabled;
        };

        var rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);
        rtl.IsCheckedChanged += (_, _) =>
        {
            _previewFlow = rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (_page is null) return;
            if (_previewFlow == FlowDirection.RightToLeft)
                _page.FlowDirection = FlowDirection.RightToLeft;
            else
                _page.ClearValue(FlowDirectionProperty);
        };
    }
}
