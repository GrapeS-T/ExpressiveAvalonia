using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One selected asset — an exported icon glyph or the tonal-palette board — with its sizing in the inspector.</summary>
public static class AssetsGallery
{
    public static Control Create() => new AssetsPlayground();
}

internal sealed class AssetsPlayground : Grid, IComponentPlayground
{
    private static readonly (string Label, Color Color)[] Seeds =
    {
        ("Baseline purple", Color.Parse("#6750A4")),
        ("Green", Color.Parse("#386A20")),
        ("Blue", Color.Parse("#0061A4")),
        ("Orange", Color.Parse("#8B5000")),
    };

    private static readonly string[] IconCategories = { "Все глифы", "Общие имена", "Уникальные имена" };
    private static readonly string[] PaletteCategories = { "Живые палитры", "Доска источника" };

    private readonly MaterialIcon _glyph = new()
    {
        IsDecorative = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _glyphName = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 20 };
    private readonly TextBlock _glyphId = new() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13 };
    private readonly Button _copy = new() { Content = "Копировать source id", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly MaterialTonalPalette _palette = new()
    {
        Title = "Tonal palettes",
        HorizontalAlignment = HorizontalAlignment.Center,
        SwatchHeight = 72,
        RowSpacing = 12,
        GroupSpacing = 24,
    };
    private readonly StackPanel _iconStage = new()
    {
        Spacing = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly ScrollViewer _paletteStage = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };
    private readonly TextBlock _status = new()
    {
        Text = "Выберите глиф или палитру в инспекторе.",
        HorizontalAlignment = HorizontalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly MaterialButtonGroup _type;
    private readonly ComboBox _category;
    private readonly ComboBox _resource;
    private readonly StackPanel _iconSettings;
    private readonly StackPanel _paletteSettings;
    private readonly Slider _glyphSize;
    private readonly Slider _swatchWidth;
    private readonly Slider _swatchHeight;
    private readonly Slider _labelWidth;
    private readonly ToggleSwitch _showName;
    private readonly ToggleSwitch _showTitle;
    private readonly ToggleSwitch _toneLabels;
    private readonly ToggleSwitch _copyOnClick;
    private List<MaterialIconInfo> _icons = [];
    private bool _syncing;
    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal AssetsPlayground()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        _iconStage.Children.Add(_glyph);
        _iconStage.Children.Add(_glyphName);
        _iconStage.Children.Add(_glyphId);
        _iconStage.Children.Add(_copy);
        MaterialAssist.SetButtonVariant(_copy, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(_copy, MaterialControlSize.Small);
        _paletteStage.Content = _palette;
        var stage = new Grid { Margin = new Thickness(24) };
        stage.Children.Add(_iconStage);
        stage.Children.Add(_paletteStage);
        Children.Add(stage);
        Grid.SetRow(_status, 1);
        _status.Margin = new Thickness(24, 0, 24, 20);
        Children.Add(_status);

        var selectorSection = InspectorLayout.Section(Settings, divider: false);
        _type = InspectorLayout.Choice(selectorSection, "Тип", new[] { "Иконки", "Палитры" }, 0);
        AutomationProperties.SetName(_type, "Тип ассета");
        _category = InspectorLayout.Combo(selectorSection, "Категория", IconCategories, 0);
        AutomationProperties.SetName(_category, "Категория ассетов");
        _resource = InspectorLayout.Combo(selectorSection, "Ресурс");
        AutomationProperties.SetName(_resource, "Выбранный ресурс");

        _iconSettings = InspectorLayout.Section(Settings, "Параметры глифа");
        _glyphSize = InspectorLayout.Slider(_iconSettings, "Размер глифа", 24, 240, 96, 8, "dp", "0");
        var iconSwitches = InspectorLayout.Switches(_iconSettings);
        _showName = InspectorLayout.Switch(iconSwitches, "Подписывать имя и source id", true);

        _paletteSettings = InspectorLayout.Section(Settings, "Тональная палитра");
        _paletteSettings.IsVisible = false;
        _swatchWidth = InspectorLayout.Slider(_paletteSettings, "Ширина образца", 40, 100, 61, 1, "dp", "0");
        _swatchHeight = InspectorLayout.Slider(_paletteSettings, "Высота образца", 40, 140, 72, 4, "dp", "0");
        _labelWidth = InspectorLayout.Slider(_paletteSettings, "Ширина подписи строки", 80, 220, 140, 10, "dp", "0");
        var paletteSwitches = InspectorLayout.Switches(_paletteSettings);
        _showTitle = InspectorLayout.Switch(paletteSwitches, "Заголовок палитры", true);
        _toneLabels = InspectorLayout.Switch(paletteSwitches, "Подписи тонов", true);
        _copyOnClick = InspectorLayout.Switch(paletteSwitches, "Копировать hex по нажатию", true);

        InspectorLayout.Note(
            Settings,
            "Глифы адресуются по design source id: восемнадцать имён экспортированы дважды, " +
            "поэтому «Общие имена» показывает обе геометрии. Нажатие на образец палитры копирует " +
            "его hex; «Доска источника» показывает точные заливки local-005641.");

        _type.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            RebuildCategories();
        };
        _category.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            RebuildResources();
        };
        _resource.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            Apply();
        };
        foreach (var slider in new[] { _glyphSize, _swatchWidth, _swatchHeight, _labelWidth })
            slider.PropertyChanged += (_, e) => { if (!_syncing && e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _showName, _showTitle, _toneLabels, _copyOnClick })
            toggle.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        _copy.Click += async (_, _) =>
        {
            if (_icons.Count == 0) return;
            var icon = _icons[Math.Clamp(_resource.SelectedIndex, 0, _icons.Count - 1)];
            _status.Text = await CopyAsync(_copy, icon.SourceId)
                ? $"Скопирован {icon.SourceId} ({icon.Name})."
                : $"Буфер обмена недоступен; source id — {icon.SourceId}.";
        };
        _palette.SwatchActivated += (_, e) => _status.Text = e.Copied
            ? $"Скопирован {e.RowName} {e.Tone} · {e.Hex}."
            : $"Буфер обмена недоступен; {e.RowName} {e.Tone} — {e.Hex}.";

        RebuildCategories();
    }

    private void RebuildCategories()
    {
        _syncing = true;
        _category.ItemsSource = _type.SelectedIndex == 0 ? IconCategories : PaletteCategories;
        _category.SelectedIndex = 0;
        _syncing = false;
        RebuildResources();
    }

    private void RebuildResources()
    {
        _syncing = true;
        if (_type.SelectedIndex == 0)
        {
            var shared = MaterialIcons.All
                .GroupBy(icon => icon.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .ToHashSet();
            _icons = _category.SelectedIndex switch
            {
                1 => MaterialIcons.All.Where(shared.Contains).ToList(),
                2 => MaterialIcons.All.Where(icon => !shared.Contains(icon)).ToList(),
                _ => MaterialIcons.All.ToList(),
            };
            _resource.ItemsSource = _icons.Select(icon => $"{icon.Name} · {icon.SourceId}").ToArray();
            _resource.SelectedIndex = _icons.Count > 0 ? 0 : -1;
            _resource.IsEnabled = _icons.Count > 0;
        }
        else
        {
            _icons = [];
            if (_category.SelectedIndex == 1)
            {
                _resource.ItemsSource = new[] { "local-005641 · .Tonal palettes" };
                _resource.SelectedIndex = 0;
                _resource.IsEnabled = false;
            }
            else
            {
                _resource.ItemsSource = Seeds.Select(seed => seed.Label).ToArray();
                _resource.SelectedIndex = 0;
                _resource.IsEnabled = true;
            }
        }
        _syncing = false;
        Apply();
    }

    private void Apply()
    {
        var icons = _type.SelectedIndex == 0;
        _iconStage.IsVisible = icons;
        _paletteStage.IsVisible = !icons;
        _iconSettings.IsVisible = icons;
        _paletteSettings.IsVisible = !icons;

        var size = _glyphSize.Value;
        _glyph.Width = size;
        _glyph.Height = size;
        _glyphName.IsVisible = _showName.IsChecked == true;
        _glyphId.IsVisible = _showName.IsChecked == true;
        // With the name visible the adjacent text already conveys the meaning, so the glyph is
        // decorative; hiding the name exposes the ContentDescription/automation name instead.
        _glyph.IsDecorative = _showName.IsChecked == true;
        _copy.IsEnabled = _icons.Count > 0;
        if (icons)
        {
            if (_icons.Count > 0)
            {
                var icon = _icons[Math.Clamp(_resource.SelectedIndex, 0, _icons.Count - 1)];
                _glyph.SourceId = icon.SourceId;
                _glyphName.Text = icon.Name;
                _glyphId.Text = $"{icon.SourceId} · {icon.FileName}";
                AutomationProperties.SetName(_glyph, $"{icon.Name} {icon.SourceId}");
            }
            else
            {
                _glyph.SourceId = null;
                _glyphName.Text = "Нет глифов";
                _glyphId.Text = string.Empty;
            }
            return;
        }

        var source = _category.SelectedIndex == 1;
        _palette.Palettes = source ? MaterialTonalPalettes.Source : null;
        if (!source)
        {
            var seed = Seeds[Math.Clamp(_resource.SelectedIndex, 0, Seeds.Length - 1)];
            _palette.SeedColor = seed.Color;
        }
        _palette.SwatchWidth = _swatchWidth.Value;
        _palette.SwatchHeight = _swatchHeight.Value;
        _palette.RowLabelWidth = _labelWidth.Value;
        _palette.ShowTitle = _showTitle.IsChecked == true;
        _palette.ShowToneLabels = _toneLabels.IsChecked == true;
        _palette.CopyOnClick = _copyOnClick.IsChecked == true;
    }

    private static async Task<bool> CopyAsync(Visual source, string text)
    {
        if (TopLevel.GetTopLevel(source)?.Clipboard is not { } clipboard)
            return false;
        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
