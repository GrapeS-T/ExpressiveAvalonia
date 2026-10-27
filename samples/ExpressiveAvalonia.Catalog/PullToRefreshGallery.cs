using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

public static class PullToRefreshGallery
{
    public static Control Create() => new RefreshPlayground();

    private sealed class RefreshPlayground : Grid, IComponentPlayground
    {
        public StackPanel Settings { get; } = new() { Spacing = 16 };
        private readonly MaterialPullToRefresh _refresh;
        private readonly ProgressBar _customProgress = new()
        {
            Name = "CustomRefreshProgress",
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 4,
            MinHeight = 0,
            Margin = new Thickness(24, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
        };
        private readonly TextBlock _status = new() { Text = "Потяните список вниз", TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _readout = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        private readonly DispatcherTimer _completion = new() { Interval = TimeSpan.FromSeconds(2) };
        private int _refreshCount;

        public RefreshPlayground()
        {
            var list = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
            list.Children.Add(_status);
            for (var i = 1; i <= 30; i++)
                list.Children.Add(new TextBlock { Text = $"Элемент {i:00}", Height = 40, VerticalAlignment = VerticalAlignment.Center });
            _refresh = new MaterialPullToRefresh
            {
                AllowMousePull = true, IndicatorType = MaterialRefreshIndicator.Loading, Width = 360, Height = 480,
                Content = new ScrollViewer { Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }
            };
            _customProgress.Bind(ProgressBar.ValueProperty,
                _refresh.GetObservable(MaterialPullToRefresh.DistanceFractionProperty));
            _customProgress.Bind(ProgressBar.IsIndeterminateProperty,
                _refresh.GetObservable(MaterialPullToRefresh.IsRefreshingProperty));
            _refresh.PropertyChanged += (_, e) =>
            {
                if (e.Property == MaterialPullToRefresh.DistanceFractionProperty ||
                    e.Property == MaterialPullToRefresh.IsRefreshingProperty)
                    UpdateReadout();
            };
            _refresh.RefreshRequested += (_, _) =>
            {
                _refresh.IsRefreshing = true;
                _status.Text = "Обновление…";
                _completion.Start();
            };
            _completion.Tick += (_, _) =>
            {
                _completion.Stop();
                _refresh.IsRefreshing = false;
                _status.Text = $"Обновлено: {++_refreshCount}";
            };
            DetachedFromVisualTree += (_, _) => { _completion.Stop(); _refresh.IsRefreshing = false; };
            Children.Add(new Border
            {
                Child = _refresh, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
            var mouse = new ToggleSwitch { Content = "Тянуть мышью (desktop extension)", IsChecked = true };
            mouse.IsCheckedChanged += (_, _) => _refresh.AllowMousePull = mouse.IsChecked == true;
            var busy = new ToggleSwitch { Content = "Внешний IsRefreshing" };
            busy.IsCheckedChanged += (_, _) => _refresh.IsRefreshing = busy.IsChecked == true;
            var finish = new Button { Content = "Завершить обновление" };
            finish.Click += (_, _) => { _completion.Stop(); _refresh.IsRefreshing = false; busy.IsChecked = false; };
            var indicator = new ComboBox { ItemsSource = new[] { "Circular arrow", "Expressive loading", "Custom linear progress" }, SelectedIndex = 1 };
            indicator.SelectionChanged += (_, _) =>
            {
                if (indicator.SelectedIndex == 2)
                {
                    _refresh.IndicatorContent = _customProgress;
                    return;
                }
                _refresh.IndicatorContent = null;
                _refresh.IndicatorType = indicator.SelectedIndex == 1 ? MaterialRefreshIndicator.Loading : MaterialRefreshIndicator.Circular;
            };
            var threshold = NumberSetting("Threshold (срабатывание), dp", 40, 200, 80, value =>
            {
                _refresh.Threshold = value;
                UpdateReadout();
            });
            var travel = NumberSetting("Indicator travel (MaxDistance), dp", 40, 240, 80, value =>
            {
                _refresh.MaxDistance = value;
                UpdateReadout();
            });
            Settings.Children.Add(indicator);
            Settings.Children.Add(threshold);
            Settings.Children.Add(travel);
            Settings.Children.Add(_readout);
            Settings.Children.Add(mouse);
            Settings.Children.Add(busy);
            Settings.Children.Add(finish);
            Settings.Children.Add(new TextBlock
            {
                Text = "Threshold управляет только тем, когда отпускание запрашивает refresh; MaxDistance — насколько далеко уезжает встроенный индикатор. Custom linear progress получает DistanceFraction как Value и переключает IsIndeterminate на IsRefreshing. Жест доступен только у начала списка; колесо мыши прокручивает, но не обновляет. После запроса приложение держит IsRefreshing две секунды. Touch/pen используют тот же жест без mouse opt-in.",
                TextWrapping = TextWrapping.Wrap
            });
            UpdateReadout();
        }

        private void UpdateReadout() =>
            _readout.Text = $"Threshold {_refresh.Threshold:0} dp · indicator travel {_refresh.MaxDistance:0} dp · " +
                            $"fraction {_refresh.DistanceFraction:0.00}{(_refresh.IsRefreshing ? " · refreshing" : "")}";

        private static Control NumberSetting(string label, double min, double max, double value, Action<double> apply)
        {
            var caption = new TextBlock { Text = $"{label}: {value:0}", TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = value, TickFrequency = 10, IsSnapToTickEnabled = true,
            };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty) return;
                caption.Text = $"{label}: {slider.Value:0}";
                apply(slider.Value);
            };
            return new StackPanel { Spacing = 4, Children = { caption, slider } };
        }
    }
}
