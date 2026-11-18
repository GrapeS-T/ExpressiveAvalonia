using Avalonia;
using Avalonia.Automation;
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
        public StackPanel Settings { get; } = InspectorLayout.CreatePane();

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
        private readonly TextBlock _readout;
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
                AllowMousePull = true,
                IndicatorType = MaterialRefreshIndicator.Loading,
                Width = 360,
                Height = 480,
                Content = new ScrollViewer
                {
                    Content = list,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                },
            };
            _customProgress.Bind(
                ProgressBar.ValueProperty,
                _refresh.GetObservable(MaterialPullToRefresh.DistanceFractionProperty));
            _customProgress.Bind(
                ProgressBar.IsIndeterminateProperty,
                _refresh.GetObservable(MaterialPullToRefresh.IsRefreshingProperty));

            var frame = new Border
            {
                Child = _refresh,
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("MaterialOutlineVariantBrush"));
            Children.Add(frame);

            var config = InspectorLayout.Section(Settings, "Конфигурация", divider: false);
            var indicator = InspectorLayout.Combo(
                config,
                "Тип индикатора",
                new[] { "Circular arrow", "Expressive loading", "Custom linear progress" },
                selected: 1);
            AutomationProperties.SetName(indicator, "Тип индикатора обновления");

            var threshold = InspectorLayout.Slider(
                config,
                "Threshold (срабатывание)",
                40,
                200,
                80,
                step: 10,
                unit: "dp",
                format: "0");
            AutomationProperties.SetName(threshold, "Threshold (срабатывание), dp");

            var travel = InspectorLayout.Slider(
                config,
                "Indicator travel (MaxDistance)",
                40,
                240,
                80,
                step: 10,
                unit: "dp",
                format: "0");
            AutomationProperties.SetName(travel, "Indicator travel (MaxDistance), dp");

            _readout = InspectorLayout.Readout(config);

            var state = InspectorLayout.Section(Settings, "Поведение и состояние");
            var switches = InspectorLayout.Switches(state);
            var mouse = InspectorLayout.Switch(switches, "Тянуть мышью (desktop extension)", isChecked: true);
            var busy = InspectorLayout.Switch(switches, "Внешний IsRefreshing");
            InspectorLayout.ActionButton(
                state,
                "Завершить обновление",
                () =>
                {
                    _completion.Stop();
                    _refresh.IsRefreshing = false;
                });

            InspectorLayout.Note(
                Settings,
                "Threshold управляет только тем, когда отпускание запрашивает refresh; MaxDistance — насколько далеко уезжает встроенный индикатор. Custom linear progress получает DistanceFraction как Value и переключает IsIndeterminate на IsRefreshing. Жест доступен только у начала списка; колесо мыши прокручивает, но не обновляет. После запроса приложение держит IsRefreshing две секунды. Touch/pen используют тот же жест без mouse opt-in.");

            var syncingBusy = false;
            _refresh.PropertyChanged += (_, e) =>
            {
                if (e.Property == MaterialPullToRefresh.IsRefreshingProperty)
                {
                    syncingBusy = true;
                    try
                    {
                        busy.IsChecked = _refresh.IsRefreshing;
                    }
                    finally
                    {
                        syncingBusy = false;
                    }
                }
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
            DetachedFromVisualTree += (_, _) =>
            {
                _completion.Stop();
                _refresh.IsRefreshing = false;
            };

            indicator.SelectionChanged += (_, _) =>
            {
                if (indicator.SelectedIndex == 2)
                {
                    _refresh.IndicatorContent = _customProgress;
                    return;
                }
                _refresh.IndicatorContent = null;
                _refresh.IndicatorType = indicator.SelectedIndex == 1
                    ? MaterialRefreshIndicator.Loading
                    : MaterialRefreshIndicator.Circular;
            };
            threshold.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty)
                    return;
                _refresh.Threshold = threshold.Value;
                UpdateReadout();
            };
            travel.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty)
                    return;
                _refresh.MaxDistance = travel.Value;
                UpdateReadout();
            };
            mouse.IsCheckedChanged += (_, _) => _refresh.AllowMousePull = mouse.IsChecked == true;
            busy.IsCheckedChanged += (_, _) =>
            {
                if (syncingBusy)
                    return;
                if (busy.IsChecked != true)
                    _completion.Stop();
                _refresh.IsRefreshing = busy.IsChecked == true;
            };

            UpdateReadout();
        }

        private void UpdateReadout() =>
            _readout.Text = $"Threshold {_refresh.Threshold:0} dp · indicator travel {_refresh.MaxDistance:0} dp · " +
                            $"fraction {_refresh.DistanceFraction:0.00}{(_refresh.IsRefreshing ? " · refreshing" : "")}";
    }
}
