using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Interactive search bar and search view with live component suggestions.</summary>
public static class SearchGallery
{
    public static Control Create() => new SearchPlayground();
}

internal sealed class SearchPlayground : Grid, IComponentPlayground
{
    private static readonly string[] Catalogue = { "Search bar", "Search view", "Navigation rail", "Navigation bar", "Bottom app bar", "Range slider", "Rich tooltip", "Circular progress", "Segmented button", "Text field", "Menu", "Chips" };
    private readonly MaterialSearchBar _bar = new() { PlaceholderText = "Search components", AutoFocus = false };
    private readonly MaterialSearchView _view;
    private readonly ObservableCollection<string> _suggestions = new();
    private readonly Border _host = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
    private readonly TextBlock _status = new() { Text = "Введите запрос", TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly MaterialButtonGroup _kind;
    private readonly MaterialButtonGroup _mode;
    private readonly Slider _width;
    private readonly TextBox _placeholder;
    private readonly ToggleSwitch _leading;
    private readonly ToggleSwitch _trailing;
    private readonly ToggleSwitch _secondary;
    private readonly ToggleSwitch _avatar;
    private readonly ToggleSwitch _showSuggestions;
    private readonly ToggleSwitch _enabled;
    private readonly ToggleSwitch _rtl;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal SearchPlayground()
    {
        _view = new MaterialSearchView
        {
            Variant = MaterialSearchViewVariant.Expressive,
            PlaceholderText = "Search components", AutoFocus = false, Suggestions = _suggestions
        };
        var stage = new StackPanel { Spacing = 24, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stage.Children.Add(_host);
        stage.Children.Add(_status);
        Children.Add(stage);
        _kind = Choice("Представление", new[] { "Search bar", "Search view" });
        _mode = Choice("Расположение view", new[] { "Docked", "Full-screen" });
        var widthLabel = new TextBlock { Text = "Ширина: 480 dp" };
        Settings.Children.Add(widthLabel);
        _width = new Slider { Minimum = 360, Maximum = 720, Value = 480, TickFrequency = 1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_width, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_width, false);
        Avalonia.Automation.AutomationProperties.SetName(_width, "Ширина поиска, dp");
        Settings.Children.Add(_width);
        _placeholder = new TextBox { Text = "Search components" };
        MaterialTextField.SetLabel(_placeholder, "Placeholder");
        MaterialTextField.SetVariant(_placeholder, MaterialTextFieldVariant.Outlined);
        Settings.Children.Add(_placeholder);
        _leading = Toggle("Ведущая иконка строки", true);
        _trailing = Toggle("Правая иконка", true);
        _secondary = Toggle("Вторая правая иконка строки", false);
        _avatar = Toggle("Аватар в строке", false);
        _showSuggestions = Toggle("Подсказки", true);
        _enabled = Toggle("Поиск доступен", true);
        _rtl = Toggle("Справа налево (RTL)", false);
        var open = new Button { Content = "Открыть Search view", HorizontalAlignment = HorizontalAlignment.Stretch };
        open.Click += (_, _) => { _kind.SelectedIndex = 1; Apply(); _view.Open(); };
        Settings.Children.Add(open);
        Settings.Children.Add(new TextBlock
        {
            Text = "Строка 56 dp, ширина 360–720 dp. MD3 — Divided, MD3E — Contained. Full-screen занимает поверхность примера, не всё окно каталога. Enter выполняет поиск; Down переходит к подсказкам, Escape сначала очищает запрос, затем закрывает view.",
            TextWrapping = TextWrapping.Wrap, FontSize = 13
        });
        _kind.SelectionChanged += (_, _) => Apply();
        _mode.SelectionChanged += (_, _) => Apply();
        _width.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            widthLabel.Text = $"Ширина: {_width.Value:0} dp";
            Apply();
        };
        _placeholder.TextChanged += (_, _) => Apply();
        foreach (var toggle in new[] { _leading, _trailing, _secondary, _avatar, _showSuggestions, _enabled, _rtl })
            toggle.IsCheckedChanged += (_, _) => Apply();
        _bar.PropertyChanged += (_, e) => { if (e.Property == MaterialSearchBar.TextProperty) SyncQuery(false); };
        _view.QueryChanged += (_, _) => SyncQuery(true);
        _bar.SearchRequested += (_, _) => { _kind.SelectedIndex = 1; Apply(); _view.Open(); _status.Text = $"Поиск: {_view.Text}"; };
        _bar.TrailingIconInvoked += (_, _) => { _kind.SelectedIndex = 1; Apply(); _view.Open(); };
        _bar.SecondaryIconInvoked += (_, _) => _status.Text = "Нажата дополнительная иконка";
        _bar.DismissRequested += (_, _) => _status.Text = "Запрос пуст";
        _view.SearchRequested += (_, _) => _status.Text = $"Поиск: {_view.Text}";
        _view.SuggestionActivated += (_, e) => _status.Text = $"Выбрано: {e.Suggestion}";
        _view.VoiceInputRequested += (_, _) => _status.Text = "Запрошен голосовой ввод (распознавание не подключено)";
        _view.Dismissed += (_, e) => { _kind.SelectedIndex = 0; Apply(); _status.Text = $"Закрыто: {e.Reason}"; };
        Filter();
        Apply();
    }

    private MaterialButtonGroup Choice(string label, string[] options)
    {
        Settings.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup { Kind = MaterialButtonGroupKind.Connected, ButtonShape = MaterialButtonShape.Round, ControlSize = MaterialControlSize.Small, SelectionMode = MaterialButtonGroupSelectionMode.Single, ShowSelectedIcon = true, SelectedIndex = 0 };
        foreach (var option in options) group.Items.Add(new MaterialButtonGroupItem { Content = option });
        Settings.Children.Add(group);
        return group;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }

    private void SyncQuery(bool fromView)
    {
        if (_syncing) return;
        _syncing = true;
        if (fromView) _bar.Text = _view.Text;
        else _view.Text = _bar.Text;
        _syncing = false;
        Filter();
    }

    private void Filter()
    {
        var query = _view.Text?.Trim() ?? "";
        _suggestions.Clear();
        foreach (var item in Catalogue.Where(item => item.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(6))
            _suggestions.Add(item);
        _status.Text = _suggestions.Count == 0 ? "Ничего не найдено" : $"Подсказок: {_suggestions.Count}";
    }

    private void Apply()
    {
        var view = _kind.SelectedIndex == 1;
        _mode.IsEnabled = view;
        _leading.IsEnabled = _secondary.IsEnabled = _avatar.IsEnabled = !view;
        _showSuggestions.IsEnabled = view;
        _host.Width = _width.Value;
        _host.Height = view ? (_mode.SelectedIndex == 1 ? 480 : 360) : double.NaN;
        _view.Mode = (MaterialSearchViewMode)_mode.SelectedIndex;
        _view.ShowSuggestions = _showSuggestions.IsChecked == true;
        _view.ShowTrailingIcon = _trailing.IsChecked == true;
        _bar.ShowLeadingIcon = _leading.IsChecked == true;
        _bar.ShowTrailingIcon = _trailing.IsChecked == true;
        _bar.ShowSecondaryIcon = _secondary.IsChecked == true;
        _bar.ShowAvatar = _avatar.IsChecked == true;
        _bar.AvatarInitial = "A";
        _bar.PlaceholderText = _view.PlaceholderText = _placeholder.Text;
        _host.IsEnabled = _enabled.IsChecked == true;
        _host.FlowDirection = _rtl.IsChecked == true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var control = view ? (Control)_view : _bar;
        if (_host.Child != control)
        {
            _host.Child = control;
            if (view) _view.Open();
        }
    }
}
