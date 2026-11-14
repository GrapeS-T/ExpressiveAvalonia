using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Catalog;

internal static class Program
{
    public static bool Smoke;
    public static bool Preview;

    [STAThread]
    public static void Main(string[] args)
    {
        Smoke = args.Contains("--smoke");
        Preview = args.Contains("--preview");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CatalogApp>().UsePlatformDetect();
}

public sealed class CatalogApp : Application
{
    // The library defaults to the pinned Standard motion scheme. The catalog is the Expressive
    // design showcase, so it opts in explicitly instead of relying on a library-wide default.
    private readonly MaterialTheme _theme = new() { MotionScheme = MaterialMotionScheme.Expressive };
    public override void Initialize() => Styles.Add(_theme);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new CatalogWindow(_theme);
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed partial class CatalogWindow : Window
{
    private readonly MaterialTheme _theme;

    public CatalogWindow(MaterialTheme theme)
    {
        _theme = theme;
        Title = "ExpressiveAvalonia · Component studio";
        Width = 1480;
        Height = 960;
        MinWidth = 960;
        MinHeight = 640;
        BuildWorkspace();
        if (Program.Smoke)
            Opened += (_, _) => DispatcherTimer.RunOnce(RunSmoke, TimeSpan.FromSeconds(1));
        if (Program.Preview)
            Opened += async (_, _) =>
            {
                try { await PreviewExporter.Export(this); }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception);
                    Environment.ExitCode = 1;
                    Close();
                }
            };
    }
}
