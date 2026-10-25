using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Carousel family: the five source layouts on both contexts, the full-screen variant, the
/// multi-ratio building blocks, paging snap, keyboard paging, selection and item activation.
/// </summary>
public static class CarouselGallery
{
    private static readonly Uri SourceImageUri =
        new("avares://ExpressiveAvalonia/Assets/DesignSystem/Carousel/carousel-item.png");

    public static Control Create()
    {
        var image = new Bitmap(AssetLoader.Open(SourceImageUri));
        var status = new TextBlock { Text = "Selected: none", FontSize = 14 };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = "Carousel", FontSize = 28 });
        panel.Children.Add(new TextBlock
        {
            Text = "Source layouts: hero, center-aligned hero, multi-browse, uncontained, multi-aspect ratio and full screen, on the mobile and tablet ladders. "
                 + "Cards page with a wheel or touch pan and snap to card starts; Left/Right move selection, Home/End jump, Enter/Space invoke the focused card. "
                 + "Selection scrolls the card into view with the expressive spatial spring, or instantly under reduced motion.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);

        var hero = Build(image, status, MaterialCarouselLayout.Hero, 5);
        panel.Children.Add(Section("Hero · Mobile · 316 + 56",
            "The source frame shows the first two cards; cards 4 and 5 are the source \"Show 4th/5th item\" toggles.",
            hero));

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var overlay = new Button { Content = "Toggle card text" };
        var showText = true;
        overlay.Click += (_, _) =>
        {
            showText = !showText;
            foreach (var container in hero.GetRealizedContainers())
                if (container is MaterialCarouselItem card)
                    card.ShowContent = showText;
            status.Text = $"Card text (source \"Show carousel text\"): {showText}";
        };
        var fourth = new Button { Content = "Show 4th card" };
        fourth.Click += (_, _) =>
        {
            hero.ShowFourthItem = !hero.ShowFourthItem;
            status.Text = $"Show 4th card: {hero.ShowFourthItem}";
        };
        var fifth = new Button { Content = "Show 5th card" };
        fifth.Click += (_, _) =>
        {
            hero.ShowFifthItem = !hero.ShowFifthItem;
            status.Text = $"Show 5th card: {hero.ShowFifthItem}";
        };
        var next = new Button { Content = "Next" };
        next.Click += (_, _) => Step(hero, 1);
        var previous = new Button { Content = "Previous" };
        previous.Click += (_, _) => Step(hero, -1);
        var last = new Button { Content = "Last" };
        last.Click += (_, _) => hero.ScrollToIndex(hero.ItemCount - 1);
        toolbar.Children.Add(overlay);
        toolbar.Children.Add(fourth);
        toolbar.Children.Add(fifth);
        toolbar.Children.Add(previous);
        toolbar.Children.Add(next);
        toolbar.Children.Add(last);
        panel.Children.Add(toolbar);

        panel.Children.Add(Section("Center-aligned hero · Mobile · 56 + 252 + 56",
            "A peek, the centered card, and a trailing peek.",
            Build(image, status, MaterialCarouselLayout.CenterAlignedHero, 3)));

        panel.Children.Add(Section("Multi-browse · Mobile · 188 + 120 + 56",
            "One medium card followed by progressively narrower peeks; the last card is disabled to show that keyboard paging skips it.",
            Build(image, status, MaterialCarouselLayout.MultiBrowse, 3, disableLast: true)));

        panel.Children.Add(Section("Uncontained · Mobile · 154 + 154 + 72, no trailing inset",
            "Equal medium cards that bleed off both edges.",
            Build(image, status, MaterialCarouselLayout.Uncontained, 3)));

        panel.Children.Add(Section("Multi-aspect ratio · 16:9, 4:3, 1:1, 3:4, 9:16",
            "Each card derives its width from its ratio at the 204 unit card height: 362, 271, 204, 153, 116.",
            Build(image, status, MaterialCarouselLayout.MultiAspectRatio, 5)));

        var tabletRow = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        tabletRow.Children.Add(Section("Hero · Tablet · 184 + 184 + 120 + 56",
            "The tablet ladder inside the source 600 unit width.",
            Build(image, status, MaterialCarouselLayout.Hero, 4, MaterialCarouselContext.Tablet, 600)));
        tabletRow.Children.Add(Section("Multi-browse · Tablet · 184 + 184 + 120 + 56",
            "Same ladder as the tablet hero; only mobile distinguishes these two layouts.",
            Build(image, status, MaterialCarouselLayout.MultiBrowse, 4, MaterialCarouselContext.Tablet, 600)));
        panel.Children.Add(tabletRow);

        var full = new MaterialCarouselFullScreen
        {
            Width = 320,
            Height = 560,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        full.Items.Add(Card(image, "Full screen", "One card fills the viewport", 16d / 9d));
        full.Items.Add(Card(image, "Second page", "Paging snaps card by card", 16d / 9d));
        full.SelectionChanged += (_, _) => status.Text = $"Full screen selected: {full.SelectedIndex}";
        full.ItemInvoked += (_, e) => status.Text = $"Full screen invoked: {e.Item.Label} (index {e.Index})";
        panel.Children.Add(Section("Full screen · Mobile (source 412 x 892)",
            "MaterialCarouselFullScreen fills the viewport with one card per page. The source keeps the card text hidden; these cards opt in through ShowContent.",
            full));

        panel.Children.Add(new TextBlock
        {
            Text = "Cards are ListBoxItem containers: selection, disabled cards, focus and SelectionPattern are native. "
                 + "Disabled cards are skipped by keyboard paging; hidden 4th/5th cards are skipped as well.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static void Step(MaterialCarousel carousel, int delta)
    {
        var count = carousel.ItemCount;
        if (count == 0)
            return;
        var index = Math.Clamp(carousel.SelectedIndex + delta, 0, count - 1);
        carousel.SelectedIndex = index;
        carousel.ScrollToIndex(index);
    }

    private static MaterialCarousel Build(
        Bitmap image,
        TextBlock status,
        MaterialCarouselLayout layout,
        int count,
        MaterialCarouselContext context = MaterialCarouselContext.Mobile,
        double width = double.NaN,
        bool disableLast = false)
    {
        var carousel = new MaterialCarousel
        {
            Layout = layout,
            Context = context,
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        for (var i = 0; i < count; i++)
        {
            var ratio = layout == MaterialCarouselLayout.MultiAspectRatio
                ? SourceRatio(i)
                : 16d / 9d;
            carousel.Items.Add(Card(image, $"Card {i + 1}", $"{layout} · card {i + 1}", ratio));
        }

        if (disableLast && carousel.Items[count - 1] is MaterialCarouselItem disabled)
            disabled.IsEnabled = false;

        carousel.SelectedIndex = 0;
        carousel.SelectionChanged += (_, _) =>
        {
            if (carousel.SelectedIndex >= 0)
                status.Text = $"{layout}: selected card {carousel.SelectedIndex + 1} of {carousel.ItemCount}";
        };
        carousel.ItemInvoked += (_, e) =>
            status.Text = $"{layout}: invoked {e.Item.Label} (index {e.Index})";
        return carousel;
    }

    private static double SourceRatio(int index) => index switch
    {
        0 => 16d / 9d,
        1 => 4d / 3d,
        2 => 1d,
        3 => 3d / 4d,
        _ => 9d / 16d,
    };

    private static MaterialCarouselItem Card(Bitmap image, string label, string supporting, double ratio) => new()
    {
        ImageSource = image,
        Label = label,
        SupportingText = supporting,
        ShowContent = true,
        AspectRatio = ratio
    };

    private static Control Section(string caption, string description, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = caption, FontSize = 16, FontWeight = FontWeight.Medium });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(content);
        return stack;
    }
}
