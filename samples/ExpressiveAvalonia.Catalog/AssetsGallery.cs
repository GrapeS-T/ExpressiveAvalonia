using System;
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

/// <summary>
/// Gallery for the icon and auxiliary-asset families: the 140 exported 24 dp glyphs looked up by
/// design source identity (local-000031…local-005658) and the .Tonal palettes board
/// (local-005641) rendered as a live palette control.
/// </summary>
public static class AssetsGallery
{
    public static Control Create()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Icons", Content = new DeferredGallery(() => IconsPage()) });
        tabs.Items.Add(new TabItem { Header = "Tonal palettes", Content = new DeferredGallery(() => PalettePage()) });
        return tabs;
    }

    private static Control IconsPage()
    {
        var status = new TextBlock { Text = "Click an icon to copy its design source id." };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var count = new TextBlock { FontSize = 14 };
        var grid = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        var search = new TextBox
        {
            PlaceholderText = "Search by name or source id",
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(Heading("Icons · 140 exported glyphs"));
        panel.Children.Add(Note("Every glyph is rendered from its own exported geometry and resolved through " +
                                "MaterialIcons by design node id. Eighteen display names appear twice in the source " +
                                "(alarm, settings, check_box, …); both identities stay addressable, so the same name " +
                                "can produce two different shapes. Clicking a tile copies the source id."));
        panel.Children.Add(search);
        panel.Children.Add(count);
        panel.Children.Add(new ScrollViewer
        {
            Height = 480,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = grid
        });
        panel.Children.Add(status);

        void Rebuild()
        {
            var term = search.Text?.Trim() ?? string.Empty;
            var matches = MaterialIcons.All
                .Where(icon => term.Length == 0 ||
                               icon.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               icon.SourceId.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            grid.Children.Clear();
            foreach (var icon in matches)
                grid.Children.Add(new ViewportGallery(() => Tile(icon, status), 72) { Width = 120 });

            var duplicates = MaterialIcons.All
                .GroupBy(icon => icon.Name, StringComparer.Ordinal)
                .Count(group => group.Count() > 1);
            count.Text = $"{matches.Count} of {MaterialIcons.All.Count} glyphs · " +
                         $"{MaterialIcons.Names.Count} distinct names · {duplicates} names shared by two identities";
        }

        search.TextChanged += (_, _) => Rebuild();
        Rebuild();
        return panel;
    }

    private static Control Tile(MaterialIconInfo icon, TextBlock status)
    {
        var ambiguous = MaterialIcons.IsAmbiguousName(icon.Name);
        var glyph = new MaterialIcon
        {
            SourceId = icon.SourceId,
            IsDecorative = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var tile = new Button
        {
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    glyph,
                    new TextBlock
                    {
                        Text = icon.Name,
                        FontSize = 12,
                        MaxWidth = 104,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };
        // The glyph follows the button's own variant colour instead of the icon default.
        glyph[!TemplatedControl.ForegroundProperty] = tile[!TemplatedControl.ForegroundProperty];
        MaterialAssist.SetButtonVariant(tile, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(tile, MaterialControlSize.ExtraSmall);
        ToolTip.SetTip(tile, $"{icon.SourceId} · {icon.FileName}" +
                             (ambiguous ? " · name shared by two source identities" : string.Empty));
        AutomationProperties.SetName(tile, $"Copy source id for {icon.Name} {icon.SourceId}");
        tile.Click += async (_, _) =>
        {
            status.Text = await CopyAsync(tile, icon.SourceId)
                ? $"Copied {icon.SourceId} ({icon.Name})."
                : $"Clipboard unavailable; source id is {icon.SourceId}.";
        };
        return tile;
    }

    private static Control PalettePage()
    {
        var status = new TextBlock { Text = "Activate a cell to copy its hex value." };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var palette = new MaterialTonalPalette();
        palette.SwatchActivated += (_, e) => status.Text = e.Copied
            ? $"Copied {e.RowName} {e.Tone} · {e.Hex}."
            : $"Clipboard unavailable; {e.RowName} {e.Tone} is {e.Hex}.";

        var sourceToggle = new ToggleSwitch
        {
            Content = "Exact source board colors",
            IsChecked = false
        };
        sourceToggle.IsCheckedChanged += (_, _) =>
        {
            palette.Palettes = sourceToggle.IsChecked == true ? MaterialTonalPalettes.Source : null;
            status.Text = sourceToggle.IsChecked == true
                ? "Showing the literal .Tonal palettes board fills (local-005641)."
                : "Showing live Tonal Spot palettes derived from the theme seed.";
        };

        var seeds = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        foreach (var (name, color) in new (string Name, Color Color)[]
                 {
                     ("Baseline purple", Color.Parse("#6750A4")),
                     ("Green", Color.Parse("#386A20")),
                     ("Blue", Color.Parse("#0061A4")),
                     ("Orange", Color.Parse("#8B5000"))
                 })
        {
            var seed = new Button { Content = name };
            MaterialAssist.SetButtonVariant(seed, MaterialButtonVariant.Tonal);
            MaterialAssist.SetControlSize(seed, MaterialControlSize.ExtraSmall);
            seed.Click += (_, _) =>
            {
                palette.ClearValue(MaterialTonalPalette.PalettesProperty);
                palette.SeedColor = color;
                sourceToggle.IsChecked = false;
                status.Text = $"Seed {name} · {color}";
            };
            seeds.Children.Add(seed);
        }

        var themeSeed = new Button { Content = "Use theme seed" };
        MaterialAssist.SetButtonVariant(themeSeed, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(themeSeed, MaterialControlSize.ExtraSmall);
        themeSeed.Click += (_, _) =>
        {
            palette.ClearValue(MaterialTonalPalette.PalettesProperty);
            palette.ClearValue(MaterialTonalPalette.SeedColorProperty);
            sourceToggle.IsChecked = false;
            status.Text = "Seed follows the active Material theme.";
        };
        seeds.Children.Add(themeSeed);

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(Heading("Tonal palettes · local-005641"));
        panel.Children.Add(Note("The source board is six rows of thirteen tones (0…100) at 61 × 108 with a 140 label " +
                                "column and an extra gap before the neutral group. Cells are real buttons: Tab reaches " +
                                "them, arrow keys move within and between rows, Home/End jump to a row edge, " +
                                "PageUp/PageDown jump to the first/last row, and Space or Enter copies the hex value."));
        panel.Children.Add(sourceToggle);
        panel.Children.Add(new TextBlock { Text = "Seed", FontSize = 14 });
        panel.Children.Add(seeds);
        panel.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = palette
        });
        panel.Children.Add(status);
        return panel;
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

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 28,
        FontWeight = FontWeight.Bold
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = 14,
        MaxWidth = 900,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.8
    };
}
