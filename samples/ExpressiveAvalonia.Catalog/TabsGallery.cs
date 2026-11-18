using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>A live tab strip with its configuration exposed in the inspector.</summary>
public static class TabsGallery
{
    public static Control Create() => new TabsPlayground();
}

internal sealed class TabsPlayground : Grid, IComponentPlayground
{
    private static readonly Geometry Icon = Geometry.Parse("M12,3 L14.8,8.7 L21,9.6 L16.5,14 L17.6,20.2 L12,17.3 L6.4,20.2 L7.5,14 L3,9.6 L9.2,8.7 Z");
    private readonly TabControl _tabs = new() { Width = 480, MinHeight = 220 };
    private readonly MaterialButtonGroup _variant;
    private readonly MaterialButtonGroup _layout;
    private readonly Slider _count;
    private readonly Slider _width;
    private readonly ToggleSwitch _scrollable;
    private readonly ToggleSwitch _badge;
    private readonly ToggleSwitch _badgeText;
    private readonly ToggleSwitch _disabled;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private readonly TextBlock _readout = new() { Text = "Выбрана вкладка: 1", HorizontalAlignment = HorizontalAlignment.Center };
    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal TabsPlayground()
    {
        var panel = new StackPanel
        {
            Spacing = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };
        panel.Children.Add(_tabs);
        panel.Children.Add(_readout);
        Children.Add(panel);

        var variantSection = InspectorLayout.Section(Settings, "Вариант", divider: false);
        _variant = InspectorLayout.Choice(variantSection, "Вид", new[] { "Primary", "Secondary" }, 0);
        _layout = InspectorLayout.Choice(variantSection, "Содержимое", new[] { "Текст", "Иконка", "Оба" }, 0);

        var dimensionsSection = InspectorLayout.Section(Settings, "Размеры");
        _width = InspectorLayout.Slider(dimensionsSection, "Ширина", 240, 800, 480, 8, "dp", "0");
        _count = InspectorLayout.Slider(dimensionsSection, "Количество вкладок", 2, 12, 3, 1, format: "0");

        var behaviorSection = InspectorLayout.Section(Settings, "Поведение и значки");
        var behaviorSwitches = InspectorLayout.Switches(behaviorSection);
        _scrollable = InspectorLayout.Switch(behaviorSwitches, "Прокручиваемая полоса", false);
        _badge = InspectorLayout.Switch(behaviorSwitches, "Badge на второй вкладке", false);
        _badgeText = InspectorLayout.Switch(behaviorSwitches, "Число в badge", false);

        var statesSection = InspectorLayout.Section(Settings, "Состояния");
        var stateSwitches = InspectorLayout.Switches(statesSection);
        _disabled = InspectorLayout.Switch(stateSwitches, "Последняя вкладка недоступна", false);
        _enabled = InspectorLayout.Switch(stateSwitches, "Вкладки доступны", true);
        _rtl = InspectorLayout.Switch(stateSwitches, "Справа налево (RTL)", false);

        InspectorLayout.Note(
            Settings,
            "Primary: заголовок 48 dp, с иконкой и текстом — 64 dp, индикатор 3 dp по ширине контента (минимум 24 dp) с пружиной DefaultSpatial. Secondary: индикатор 2 dp на всю ширину вкладки, цвет on-surface. Иконки 24 dp, разделитель 1 dp. Стрелки и Home/End переключают вкладки; ввод в содержимом не меняет выбор.");

        _variant.SelectionChanged += (_, _) => Apply();
        _layout.SelectionChanged += (_, _) => Apply();
        _width.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) _tabs.Width = _width.Value; };
        _count.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _scrollable, _badge, _badgeText, _disabled, _enabled, _rtl })
            toggle.IsCheckedChanged += (_, _) => Apply();
        _tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == _tabs) _readout.Text = $"Выбрана вкладка: {_tabs.SelectedIndex + 1}";
        };
        Apply();
    }

    private void Apply()
    {
        var count = (int)_count.Value;
        // Keep existing pages and their edited contents while changing header anatomy.
        while (_tabs.Items.Count > count) _tabs.Items.RemoveAt(_tabs.Items.Count - 1);
        while (_tabs.Items.Count < count)
        {
            var index = _tabs.Items.Count;
            var editor = new TextBox { Text = $"Page {index + 1}" };
            MaterialTextField.SetLabel(editor, "Содержимое вкладки");
            MaterialTextField.SetVariant(editor, MaterialTextFieldVariant.Outlined);
            _tabs.Items.Add(new TabItem
            {
                Header = $"Tab {index + 1}",
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = $"Содержимое вкладки {index + 1}", FontSize = 20 },
                        editor,
                    },
                },
            });
        }
        MaterialTabs.SetVariant(_tabs, (MaterialTabVariant)_variant.SelectedIndex);
        MaterialTabs.SetIsScrollable(_tabs, _scrollable.IsChecked == true);
        _tabs.IsEnabled = _enabled.IsChecked == true;
        InspectorLayout.ApplyFlowDirection(_tabs, _rtl);
        _badgeText.IsEnabled = _badge.IsChecked == true;
        var layout = new[] { MaterialTabLayout.LabelOnly, MaterialTabLayout.IconOnly, MaterialTabLayout.IconAndLabel }[_layout.SelectedIndex];
        for (var i = 0; i < count; i++)
        {
            var item = (TabItem)_tabs.Items[i]!;
            item.IsEnabled = !(_disabled.IsChecked == true && i == count - 1);
            if (layout != MaterialTabLayout.LabelOnly && item.Icon is null)
                item.Icon = new PathIcon { Data = Icon, Width = 24, Height = 24 };
            if (layout == MaterialTabLayout.LabelOnly) item.Icon = null;
            MaterialTabs.SetLayout(item, layout);
            AutomationProperties.SetName(item, $"Tab {i + 1}");
            MaterialTabs.SetShowBadge(item, i == 1 && _badge.IsChecked == true);
            MaterialTabs.SetBadgeText(item, _badgeText.IsChecked == true ? "3" : null);
        }
        if (_tabs.SelectedIndex < 0 || _tabs.SelectedIndex >= count || !((TabItem)_tabs.SelectedItem!).IsEnabled)
            _tabs.SelectedIndex = 0;
    }
}
