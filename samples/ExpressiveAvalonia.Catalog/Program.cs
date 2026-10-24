using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

internal static class Program
{
    public static bool Smoke;
    [STAThread]
    public static void Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CatalogApp>().UsePlatformDetect();
}

public sealed class CatalogApp : Application
{
    private readonly MaterialTheme _theme = new();

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(_theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new CatalogWindow(_theme);
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class CatalogWindow : Window
{
    private readonly MaterialTheme _theme;
    private readonly TextBlock _status = new() { Text = "Нажатий: 0" };
    private int _clicks;
    private readonly Button _action;
    private readonly Button _disabled;
    private readonly ThemeVariantScope _localDark;

    public CatalogWindow(MaterialTheme theme)
    {
        _theme = theme;
        Title = "ExpressiveAvalonia · Theme laboratory";
        Width = 1200;
        Height = 900;
        var panel = new StackPanel { Margin = new Thickness(32), Spacing = 20 };
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Theme", Content = new ScrollViewer { Content = panel } });
        tabs.Items.Add(new TabItem { Header = "Standard controls", Content = StandardControlsGallery.Create(theme) });
        Content = tabs;
        panel.Children.Add(new TextBlock { Text = "ExpressiveAvalonia", FontSize = 36, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "Лаборатория токенов · начальная тема, не полный MD3", FontSize = 18 });
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        var toggle = new Button { Content = "Светлая / тёмная" };
        toggle.Click += (_, _) => RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        var seed = new Button { Content = "Изменить seed" };
        seed.Click += (_, _) => _theme.SeedColor = _theme.SeedColor == Color.Parse("#6750A4") ? Color.Parse("#006C4C") : Color.Parse("#6750A4");
        var contrast = new Button { Content = "Контраст" };
        contrast.Click += (_, _) => _theme.Contrast = _theme.Contrast == 0 ? 1 : 0;
        actions.Children.Add(toggle);
        actions.Children.Add(seed);
        actions.Children.Add(contrast);
        panel.Children.Add(actions);
        panel.Children.Add(new TextBlock { Text = "Filled button · Tab / Enter / Space", FontSize = 22 });
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        _action = new Button { Content = "_Действие" };
        _action.Click += (_, _) => _status.Text = $"Нажатий: {++_clicks}";
        _disabled = new Button { Content = "Недоступно", IsEnabled = false };
        buttons.Children.Add(_action);
        buttons.Children.Add(_disabled);
        panel.Children.Add(buttons);
        panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "Цветовые роли · обновляются без пересоздания UI", FontSize = 22 });
        var roles = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        foreach (var role in new[] { "Primary", "Secondary", "Tertiary", "Error" })
            roles.Children.Add(Swatch(role));
        panel.Children.Add(roles);
        _localDark = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark };
        var localPanel = new StackPanel { Spacing = 12 };
        localPanel.Children.Add(new TextBlock { Text = "Локальная тёмная тема (независимо от окна)" });
        localPanel.Children.Add(new Button { Content = "Dark scope", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
        var localBorder = new Border { Padding = new Thickness(20), CornerRadius = new CornerRadius(20), Child = localPanel };
        localBorder.Bind(BackgroundProperty, localBorder.GetResourceObservable("MaterialSurfaceContainerBrush"));
        _localDark.Child = localBorder;
        panel.Children.Add(_localDark);
        if (Program.Smoke)
            Opened += (_, _) => DispatcherTimer.RunOnce(RunSmoke, TimeSpan.FromSeconds(1));
    }

    private static Border Swatch(string role)
    {
        var label = new TextBlock { Text = role, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable($"MaterialOn{role}Brush"));
        var border = new Border { Width = 180, Height = 72, CornerRadius = new CornerRadius(16), Child = label, Padding = new Thickness(20) };
        border.Bind(BackgroundProperty, border.GetResourceObservable($"Material{role}Brush"));
        return border;
    }

    private void RunSmoke()
    {
        try
        {
            Color Primary(Control control) => ((ISolidColorBrush)control.FindResource(control.ActualThemeVariant, "MaterialPrimaryBrush")!).Color;
            var initial = Primary(_action);
            _action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (_clicks != 1 || _status.Text != "Нажатий: 1") throw new InvalidOperationException("Action did not update state.");
            if (_disabled.IsEffectivelyEnabled) throw new InvalidOperationException("Disabled button enabled.");
            RequestedThemeVariant = ThemeVariant.Dark;
            var dark = Primary(_action);
            if (dark == initial) throw new InvalidOperationException("Theme did not update.");
            if (Primary(_localDark) != dark) throw new InvalidOperationException("Local dark scope mismatch.");
            RequestedThemeVariant = ThemeVariant.Light;
            if (Primary(_localDark) != dark) throw new InvalidOperationException("Local dark scope lost.");
            _theme.SeedColor = Color.Parse("#006C4C");
            if (Primary(_action) == initial) throw new InvalidOperationException("Seed did not update resources.");
            var normal = Primary(_action);
            _theme.Contrast = 1;
            if (Primary(_action) == normal) throw new InvalidOperationException("Contrast did not update resources.");
            if (_clicks != 1) throw new InvalidOperationException("State lost on theme update.");
            // Visual acceptance uses baseline purple and standard contrast, not the contrast probe.
            _theme.Contrast = 0;
            _theme.SeedColor = Color.Parse("#6750A4");
            DispatcherTimer.RunOnce(() =>
            {
                try
                {
                    Capture("theme");
                    var root = (TabControl)Content!;
                    root.SelectedIndex = 1;
                    var gallery = (TabControl)((TabItem)root.Items[1]!).Content!;
                    SmokePage(gallery, 0);
                }
                catch (Exception exception) { Fail(exception); }
            }, TimeSpan.FromMilliseconds(250));
        }
        catch (Exception exception) { Fail(exception); }
    }

    private void Capture(string name)
    {
        UpdateLayout();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Width, (int)Height));
        bitmap.Render(this);
        Directory.CreateDirectory("artifacts");
        bitmap.Save($"artifacts/catalog-{name}.png", new PngBitmapEncoderOptions());
    }

    private void SmokePage(TabControl gallery, int index)
    {
        if (index >= gallery.ItemCount)
        {
            if (ActualThemeVariant == ThemeVariant.Light)
            {
                RequestedThemeVariant = ThemeVariant.Dark;
                SmokePage(gallery, 0);
            }
            else SmokePopup();
            return;
        }
        gallery.SelectedIndex = index;
        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                Capture($"{(ActualThemeVariant == ThemeVariant.Dark ? "dark" : "light")}-page-{index}");
                Console.WriteLine($"Rendered page {index}: {((TabItem)gallery.Items[index]!).Header}");
                SmokePage(gallery, index + 1);
            }
            catch (Exception exception) { Fail(exception); }
        }, TimeSpan.FromMilliseconds(index == 3 ? 15000 : 300));
    }

    private void SmokePopup()
    {
        var owner = new Button { Content = "Rich tooltip owner", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var action = new Button { Content = "Закрыть" };
        var rich = new ExpressiveAvalonia.Controls.RichTooltip { Title = "Проверка popup", Content = "Реальный desktop flyout", Actions = action };
        Content = owner;
        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                owner.Focus(); rich.ShowAt(owner);
                DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        if (!rich.IsOpen) throw new InvalidOperationException("Rich tooltip did not open.");
                        action.Focus();
                        action.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
                        if (rich.IsOpen) throw new InvalidOperationException("Rich tooltip Escape did not dismiss.");
                        Console.WriteLine("SMOKE PASS: theme transitions, six pages in light/dark at contrast 0, desktop rich tooltip open/Escape.");
                        Close();
                    }
                    catch (Exception exception) { rich.Hide(); Fail(exception); }
                }, TimeSpan.FromMilliseconds(300));
            }
            catch (Exception exception) { Fail(exception); }
        }, TimeSpan.FromMilliseconds(100));
    }

    private void Fail(Exception exception)
    {
        Console.Error.WriteLine(exception);
        Environment.ExitCode = 1;
        Close();
    }
}
