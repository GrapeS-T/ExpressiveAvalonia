using System.Net;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Catalog;

internal static class PreviewExporter
{
    private const int TileSize = 1600;
    private static readonly string DirectoryPath = Path.GetFullPath("artifacts/previews");
    private static readonly List<string> Cards = new();
    private static int _images;

    public static async Task Export(CatalogWindow window)
    {
        System.IO.Directory.CreateDirectory(DirectoryPath);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            window.RequestedThemeVariant = theme;
            var tabs = window.ExpressivePages;
            for (var i = 0; i < tabs.ItemCount; i++)
            {
                window.SelectPage(i);
                var item = (TabItem)tabs.Items[i]!;
                var page = window.CurrentPage!;
                window.SetPreviewContent(null);
                var original = window.Content;
                window.Content = page;
                try
                {
                    await CaptureVariants(window, page, $"{theme} / MD3 Expressive / {item.Header}");
                }
                finally
                {
                    window.Content = null;
                    window.Content = original;
                    window.SetPreviewContent(page);
                }
            }
        }
        var html = "<!doctype html><html lang=ru><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>ExpressiveAvalonia — превью</title><style>body{font:16px system-ui;margin:24px;background:#17171c;color:#eee}header{position:sticky;top:0;background:#17171c;padding:12px;z-index:1}input{padding:10px;width:min(600px,80%)}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:20px}article{background:#272730;padding:16px;border-radius:12px}img{width:100%;height:320px;object-fit:contain;object-position:top;background:#ddd}a{color:#cbc4ff}h2{font-size:16px}p{line-height:1.5}</style><header><h1>ExpressiveAvalonia — все страницы каталога</h1><p>Реальные контролы Avalonia · светлая и тёмная темы · вложенные вкладки · полное содержимое прокрутки. Крупные страницы разбиты на плитки; нажмите снимок для оригинального размера. Интерактивные popup-состояния открываются в живом каталоге.</p><input type=search placeholder='Фильтр по семейству или теме' aria-label='Фильтр'></header><main>" + string.Join("\n", Cards) + "</main><script>document.querySelector('input').oninput=e=>{for(const card of document.querySelectorAll('article'))card.hidden=!card.textContent.toLowerCase().includes(e.target.value.toLowerCase())}</script></html>";
        await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "index.html"), html);
        Console.WriteLine($"PREVIEW PASS: {_images} images; {Path.Combine(DirectoryPath, "index.html")}");
        window.Close();
    }

    private static async Task Settle(Window window)
    {
        window.UpdateLayout();
        await Task.Delay(80);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Render);
    }

    private static async Task CaptureVariants(Window window, Control page, string title)
    {
        await Settle(window);
        var nested = page as TabControl ?? page.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (nested is not null)
        {
            var selected = nested.SelectedIndex;
            for (var i = 0; i < nested.ItemCount; i++)
            {
                nested.SelectedIndex = i;
                await Settle(window);
                var item = (TabItem)nested.Items[i]!;
                // Recurse through the selected tab's subtree, retaining the surrounding page.
                if (item.Content is Control child)
                    await CaptureNested(window, page, child, $"{title} / {item.Header}");
                else await Capture(window, page, $"{title} / {item.Header}");
            }
            nested.SelectedIndex = selected;
        }
        else await Capture(window, page, title);
    }

    private static async Task CaptureNested(Window window, Control page, Control subtree, string title)
    {
        var nested = subtree as TabControl ?? subtree.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (nested is null) { await Capture(window, page, title); return; }
        var selected = nested.SelectedIndex;
        for (var i = 0; i < nested.ItemCount; i++)
        {
            nested.SelectedIndex = i;
            await Settle(window);
            var item = (TabItem)nested.Items[i]!;
            if (item.Content is Control child)
                await CaptureNested(window, page, child, $"{title} / {item.Header}");
            else await Capture(window, page, $"{title} / {item.Header}");
        }
        nested.SelectedIndex = selected;
    }

    private static async Task Capture(Window window, Control page, string title)
    {
        var scrolls = page.GetVisualDescendants().OfType<ScrollViewer>().Prepend(page as ScrollViewer).Where(s => s is not null).Cast<ScrollViewer>().Distinct().ToArray();
        var states = scrolls.Select(s => (Scroll: s, Horizontal: s.HorizontalScrollBarVisibility, Vertical: s.VerticalScrollBarVisibility, s.Offset, s.MinWidth)).ToArray();
        foreach (var s in scrolls)
        {
            if (s.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && s.Content is Control content)
            {
                content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                s.SetCurrentValue(Control.MinWidthProperty, Math.Max(s.MinWidth, content.DesiredSize.Width));
            }
            s.SetCurrentValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            s.SetCurrentValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            s.Offset = default;
        }
        var host = new Canvas { ClipToBounds = true, Background = window.Background };
        window.Content = null;
        host.Children.Add(page);
        window.Content = host;
        try
        {
            // A finite width preserves responsive layouts and text wrapping; infinite height exposes every row.
            page.Measure(new Size(1400, double.PositiveInfinity));
            var size = new Size(Math.Max(1400, page.DesiredSize.Width), Math.Max(1, page.DesiredSize.Height));
            page.Arrange(new Rect(size));
            await Task.Delay(80);
            page.Measure(new Size(1400, double.PositiveInfinity));
            size = new Size(Math.Ceiling(Math.Max(1400, page.DesiredSize.Width)), Math.Ceiling(Math.Max(1, page.DesiredSize.Height)));
            page.Arrange(new Rect(size));
            var links = new StringBuilder();
            for (var y = 0; y < size.Height; y += TileSize)
            for (var x = 0; x < size.Width; x += TileSize)
            {
                var width = (int)Math.Min(TileSize, size.Width - x);
                var height = (int)Math.Min(TileSize, size.Height - y);
                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0, 0, width, height));
                page.Arrange(new Rect(-x, -y, size.Width, size.Height));
                var file = $"preview-{++_images:D4}.png";
                using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
                bitmap.Render(host);
                bitmap.Save(Path.Combine(DirectoryPath, file), new PngBitmapEncoderOptions());
                links.Append($"<a href='{file}'><img loading='lazy' src='{file}' alt='{WebUtility.HtmlEncode(title)} — {x},{y}'></a>");
            }
            Cards.Add($"<article><h2>{WebUtility.HtmlEncode(title)}</h2><p>{size.Width} × {size.Height}</p>{links}</article>");
            Console.WriteLine($"Preview: {title} ({size.Width} x {size.Height})");
        }
        finally
        {
            window.Content = null;
            host.Children.Remove(page);
            window.Content = page;
            foreach (var state in states)
            {
                state.Scroll.SetCurrentValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, state.Horizontal);
                state.Scroll.SetCurrentValue(ScrollViewer.VerticalScrollBarVisibilityProperty, state.Vertical);
                state.Scroll.SetCurrentValue(Control.MinWidthProperty, state.MinWidth);
                state.Scroll.Offset = state.Offset;
            }
        }
    }
}
