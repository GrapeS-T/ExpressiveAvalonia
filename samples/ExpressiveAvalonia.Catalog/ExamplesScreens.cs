using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// The fourteen example surfaces from the design source: seven phone screens and seven browser
/// screens, plus the shared building blocks they are made of.
/// </summary>
/// <remarks>
/// Every surface is assembled from real controls — the device frame, status bar, layout grid,
/// content block, slot host and general item from this family, and the app bar, navigation bar,
/// rail, cards, chips, search bar, avatar and list controls from the sibling families. Text,
/// section order, tile counts and component choices come from the source <c>design.json</c> trees.
/// The source contains no executable behaviour, so interaction follows standard Avalonia
/// semantics: real buttons, real list selection, real focus order, and real scrolling.
/// </remarks>
public static class ExamplesScreens
{
    private static readonly Lazy<IImage> s_carousel = new(LoadCarouselArtwork);

    /// <summary>The exported carousel photograph, decoded once per process.</summary>
    public static IImage CarouselArtwork => s_carousel.Value;

    /// <summary>Home-Mobile (local-001355): sections of tiles, cards, a swipeable song list and a bottom bar.</summary>
    public static Control HomeMobile() => Mobile(
        MaterialAppBarConfiguration.Medium, "Label", "Supporting text", HomeBody(mobile: true),
        navIndex: 0);

    /// <summary>Upcoming-Mobile (local-001375): a media carousel over a five-row upcoming list.</summary>
    public static Control UpcomingMobile() => Mobile(
        MaterialAppBarConfiguration.Small, "Label", "Supporting text", UpcomingBody(mobile: true),
        navIndex: 1);

    /// <summary>Detailed view-Mobile (local-001351): long-form article with a download action.</summary>
    public static Control DetailedMobile() => Mobile(
        MaterialAppBarConfiguration.Medium, "Label", "Supporting text", DetailedBody(mobile: true),
        navIndex: 0);

    /// <summary>Reviews-Mobile (local-001373): place summary, chips and restaurant list.</summary>
    public static Control ReviewsMobile() => Mobile(
        MaterialAppBarConfiguration.Small, "Name", "Supporting text", ReviewsBody(mobile: true),
        navIndex: 1);

    /// <summary>Library-Mobile (local-001369): filter chips, sorting row and a twelve-card grid.</summary>
    public static Control LibraryMobile() => Mobile(
        MaterialAppBarConfiguration.Small, "Label", "Supporting text", LibraryBody(mobile: true),
        navIndex: 2);

    /// <summary>Gallery-Mobile (local-001353): large app bar, image mosaic and card carousel.</summary>
    public static Control GalleryMobile() => Mobile(
        MaterialAppBarConfiguration.Large, "Label", "Supporting text", GalleryBody(mobile: true),
        navIndex: 2);

    /// <summary>Messaging-Mobile (local-001371): conversation with predictive replies and an input bar.</summary>
    public static Control MessagingMobile() => Mobile(
        MaterialAppBarConfiguration.Small, "Name", "Supporting text", MessagingBody(mobile: true),
        navIndex: 0, showGestureBar: true);

    /// <summary>Home-Web (local-001356): the home sections behind a navigation rail and browser chrome.</summary>
    public static Control HomeWeb() => Web(0, HomeBody(mobile: false));

    /// <summary>Upcoming-Web (local-001376).</summary>
    public static Control UpcomingWeb() => Web(1, UpcomingBody(mobile: false));

    /// <summary>Detailed view-Web (local-001352).</summary>
    public static Control DetailedWeb() => Web(0, DetailedBody(mobile: false));

    /// <summary>Reviews-Web (local-001374).</summary>
    public static Control ReviewsWeb() => Web(1, ReviewsBody(mobile: false));

    /// <summary>Library-Web (local-001370).</summary>
    public static Control LibraryWeb() => Web(2, LibraryBody(mobile: false));

    /// <summary>Gallery-Web (local-001354).</summary>
    public static Control GalleryWeb() => Web(2, GalleryBody(mobile: false));

    /// <summary>Messaging-Web (local-001372): conversation list beside the thread.</summary>
    public static Control MessagingWeb() => Web(0, MessagingWebBody(), showRail: false);

    /// <summary>A phone surface: status bar, app bar, scrolling body, navigation bar and gesture bar.</summary>
    public static Control Mobile(
        MaterialAppBarConfiguration configuration,
        string title,
        string? subtitle,
        Control body,
        int navIndex,
        bool showGestureBar = true)
    {
        var appBar = new MaterialAppBar
        {
            Configuration = configuration,
            Title = title,
            Subtitle = subtitle,
            LeadingIcon = Geometry("menu"),
            TrailingContent = TrailingActions()
        };

        return new MaterialDeviceFrame
        {
            Width = 428,
            Height = configuration == MaterialAppBarConfiguration.Large ? 1240 : 960,
            TopContent = appBar,
            BottomContent = NavigationBar(navIndex),
            ShowGestureBar = showGestureBar,
            Content = body
        };
    }

    /// <summary>A browser surface: chrome bar, navigation rail and scrolling content.</summary>
    public static Control Web(int railIndex, Control body, bool showRail = true)
    {
        var shell = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        shell.Children.Add(BrowserChrome());

        Control content = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        if (showRail)
        {
            var layout = new MaterialLayoutGrid
            {
                RequestedNavigation = MaterialNavigationRegion.NavigationRegion,
                AutoNavigation = false,
                NavigationContent = Rail(railIndex),
                Content = content
            };
            Grid.SetRow(layout, 1);
            shell.Children.Add(layout);
        }
        else
        {
            Grid.SetRow(content, 1);
            shell.Children.Add(content);
        }
        return shell;
    }

    /// <summary>The source browser chrome bar: history icons, address field, avatar and overflow.</summary>
    public static Control BrowserChrome()
    {
        var history = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        history.Children.Add(Glyph("arrow_back", 16));
        history.Children.Add(Glyph("arrow_forward", 16));
        history.Children.Add(Glyph("undo", 16));

        var address = new Border
        {
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Margin = new Thickness(16, 0, 0, 0),
            Padding = new Thickness(16, 0, 12, 0)
        };
        address.Bind(Border.BackgroundProperty, address.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
        var addressRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        addressRow.Children.Add(Glyph("link", 15, vertical: VerticalAlignment.Center));
        var url = new TextBlock
        {
            Text = "www.url.com",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        MaterialTypography.SetStyle(url, MaterialTypeStyle.BodyMedium);
        url.Bind(TextBlock.ForegroundProperty, url.GetResourceObservable("MaterialOnSurfaceBrush"));
        Grid.SetColumn(url, 1);
        addressRow.Children.Add(url);
        var star = Glyph("star", 17, vertical: VerticalAlignment.Center);
        Grid.SetColumn(star, 2);
        addressRow.Children.Add(star);
        address.Child = addressRow;

        var avatar = new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Monogram,
            Letter = "M",
            Size = 28,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var more = Glyph("more_vert", 16, vertical: VerticalAlignment.Center);
        more.Margin = new Thickness(12, 0, 16, 0);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Height = 68 };
        grid.Children.Add(history);
        Grid.SetColumn(address, 1);
        grid.Children.Add(address);
        Grid.SetColumn(avatar, 2);
        grid.Children.Add(avatar);
        Grid.SetColumn(more, 3);
        grid.Children.Add(more);
        grid.Bind(Panel.BackgroundProperty, grid.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return grid;
    }

    /// <summary>The source navigation bar with three destinations.</summary>
    public static Control NavigationBar(int selectedIndex)
    {
        var bar = new MaterialNavigationBar
        {
            ItemIconPosition = MaterialNavigationItemIconPosition.Top
        };
        bar.Items.Add(NavItem("Home", "stars", selectedIndex == 0));
        bar.Items.Add(NavItem("Search", "search", selectedIndex == 1));
        bar.Items.Add(NavItem("Library", "folder", selectedIndex == 2));
        bar.SelectedIndex = selectedIndex;
        return bar;
    }

    private static MaterialNavigationBarItem NavItem(string label, string icon, bool selected) => new()
    {
        Content = label,
        Icon = Geometry(icon),
        SelectedIcon = Geometry(icon),
        ShowLabel = true,
        IsSelected = selected
    };

    /// <summary>The source 72 DIP navigation rail with three destinations.</summary>
    public static Control Rail(int selectedIndex)
    {
        var rail = new MaterialNavigationRail
        {
            IsExpanded = false,
            ShowMenuButton = true,
            MenuIcon = Geometry("menu"),
            ShowFab = true,
            FabIcon = Geometry("edit")
        };
        rail.Items.Add(RailItem("Home", "stars", selectedIndex == 0));
        rail.Items.Add(RailItem("Search", "search", selectedIndex == 1));
        rail.Items.Add(RailItem("Library", "folder", selectedIndex == 2));
        rail.SelectedIndex = selectedIndex;
        return rail;
    }

    private static MaterialNavigationRailItem RailItem(string label, string icon, bool selected) => new()
    {
        Content = label,
        Label = label,
        Icon = Geometry(icon),
        SelectedIcon = Geometry(icon),
        ShowLabel = true,
        IsSelected = selected
    };

    private static Control TrailingActions()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(IconButton("search", "Search"));
        row.Children.Add(IconButton("notifications", "Notifications"));
        row.Children.Add(IconButton("account_circle", "Account"));
        return row;
    }

    /// <summary>A text-styled icon button using the exported glyphs.</summary>
    public static Button IconButton(string icon, string name)
    {
        var button = new Button
        {
            Content = Glyph(icon, 20),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        MaterialAssist.SetIsIconButton(button, true);
        MaterialAssist.SetContainerInset(button, new Thickness(0));
        AutomationProperties.SetName(button, name);
        return button;
    }

    /// <summary>A filled button in the source medium size.</summary>
    public static Button ActionButton(string label, string? icon = null)
    {
        var button = new Button
        {
            Content = icon is null ? label : Glyph(icon, 20),
            MinHeight = 40,
            Padding = icon is null ? new Thickness(24, 10, 24, 10) : new Thickness(12)
        };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        MaterialAssist.SetButtonShape(button, MaterialButtonShape.Round);
        AutomationProperties.SetName(button, label);
        return button;
    }

    /// <summary>A text button in the source medium size.</summary>
    public static Button TextAction(string label)
    {
        var button = new Button { Content = label };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        return button;
    }

    /// <summary>An exported source glyph as a fill-only path.</summary>
    public static Control Glyph(string name, double size, string brushKey = "MaterialOnSurfaceVariantBrush",
        VerticalAlignment vertical = VerticalAlignment.Center)
    {
        if (!MaterialIcons.TryGetByName(name, out var info))
            throw new ArgumentOutOfRangeException(nameof(name), name, "No exported glyph has that name.");
        var path = new ShapePath
        {
            Data = info.Geometry,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = vertical,
            IsHitTestVisible = false
        };
        path.Bind(Shape.FillProperty, path.GetResourceObservable(brushKey));
        return path;
    }

    /// <summary>The exported geometry for an icon name, for controls that take a <see cref="Geometry"/>.</summary>
    public static Geometry Geometry(string name) =>
        MaterialIcons.TryGetByName(name, out var info)
            ? info.Geometry
            : throw new ArgumentOutOfRangeException(nameof(name), name, "No exported glyph has that name.");

    /// <summary>An avatar photograph by one-based source index.</summary>
    public static MaterialAvatar Avatar(int index, double size = 36) => new()
    {
        Kind = MaterialAvatarKind.Image,
        Source = MaterialAvatars.GetByIndex(index).Image,
        Size = size,
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>A general item tile carrying the exported carousel artwork and a caption.</summary>
    /// <remarks>Pass a width of 0 (or less) to let the tile stretch to its parent.</remarks>
    public static MaterialGeneralItem Tile(string label, double width, double height, string? supporting = null,
        bool selected = false)
    {
        var tile = new MaterialGeneralItem
        {
            Height = height,
            Label = label,
            SupportingText = supporting,
            ShowSupportingText = supporting is not null,
            ShowCarouselText = true,
            IsSelected = selected,
            Content = Artwork()
        };
        if (width > 0) tile.Width = width;
        return tile;
    }

    /// <summary>The exported carousel photograph in a rounded, clipped frame.</summary>
    public static Control Artwork(string? tint = null)
    {
        var image = new Image
        {
            Source = CarouselArtwork,
            Stretch = Stretch.UniformToFill
        };
        var host = new Grid();
        host.Children.Add(image);
        if (tint is not null)
        {
            var wash = new Border();
            wash.Bind(Border.BackgroundProperty, wash.GetResourceObservable(tint));
            wash.Opacity = 0.55;
            host.Children.Add(wash);
        }
        return host;
    }

    /// <summary>A section header with the source trailing chevron action.</summary>
    public static Control SectionHeader(string title, bool showAction = true)
    {
        var header = new MaterialSectionHeader
        {
            Label = title,
            TrailingContent = showAction ? IconButton("chevron_forward", $"{title} actions") : null
        };
        return header;
    }

    /// <summary>A horizontally scrolling carousel of children.</summary>
    public static Control Carousel(IEnumerable<Control> items, double spacing = 8)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var item in items) strip.Children.Add(item);
        return new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    /// <summary>A list row with a leading thumbnail, headline, supporting line and trailing metadata.</summary>
    public static Control ListRow(string headline, string supporting, string? overline = null, string? trailing = null,
        Control? leading = null)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (overline is not null)
        {
            var over = new TextBlock { Text = overline };
            MaterialTypography.SetStyle(over, MaterialTypeStyle.LabelSmall);
            over.Bind(TextBlock.ForegroundProperty, over.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
            text.Children.Add(over);
        }
        var title = new TextBlock { Text = headline };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleMedium);
        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable("MaterialOnSurfaceBrush"));
        text.Children.Add(title);
        var sub = new TextBlock { Text = supporting, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 320 };
        MaterialTypography.SetStyle(sub, MaterialTypeStyle.BodyMedium);
        sub.Bind(TextBlock.ForegroundProperty, sub.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        text.Children.Add(sub);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 6) };
        if (leading is not null)
        {
            leading.Margin = new Thickness(0, 0, 12, 0);
            grid.Children.Add(leading);
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (trailing is not null)
        {
            var meta = new TextBlock { Text = trailing, VerticalAlignment = VerticalAlignment.Center };
            MaterialTypography.SetStyle(meta, MaterialTypeStyle.LabelMedium);
            meta.Bind(TextBlock.ForegroundProperty, meta.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
            Grid.SetColumn(meta, 2);
            grid.Children.Add(meta);
        }
        return grid;
    }

    // ---- bodies -------------------------------------------------------------------------

    private static Control HomeBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = mobile ? 8 : 16, Margin = mobile ? new Thickness(0) : new Thickness(24) };

        panel.Children.Add(SectionHeader("Section title"));
        var tiles = new List<Control>();
        for (var i = 1; i <= 8; i++) tiles.Add(Tile($"Item {i:00}", 96, 124));
        panel.Children.Add(Carousel(tiles));

        panel.Children.Add(SectionHeader("Section title"));
        var feature = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        feature.Children.Add(Tile("Label", 316, 205, "Supporting Text"));
        feature.Children.Add(Tile("Label", 56, 205));
        panel.Children.Add(feature);
        panel.Children.Add(CaptionRow("Label", 316, "Supporting Text"));
        panel.Children.Add(CaptionRow("Label", 56, null));

        panel.Children.Add(SectionHeader("Section title"));
        var cards = new List<Control>();
        for (var i = 1; i <= 8; i++)
        {
            var card = new MaterialCard
            {
                Variant = MaterialCardVariant.Filled,
                Layout = MaterialCardLayout.Slot,
                Width = 96,
                Height = 120,
                Content = Tile($"Card {i:00}", 96, 96)
            };
            cards.Add(card);
        }
        panel.Children.Add(Carousel(cards));

        panel.Children.Add(SectionHeader("Section title"));
        var wide = new MaterialCard
        {
            Variant = MaterialCardVariant.Elevated,
            Layout = MaterialCardLayout.MediaText,
            Width = 380,
            Media = Artwork(),
            Title = "Headline",
            Subtitle = "Description duis aute irure dolor in reprehenderit in voluptate velit.",
            SupportingText = "Today \u2022 23 min",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(wide);

        panel.Children.Add(SectionHeader("Section title"));
        var songs = new StackPanel { Spacing = 4 };
        songs.Children.Add(SwipeSongRow("Title", "Artist", 1));
        songs.Children.Add(SwipeSongRow("Title", "Artist", 2));
        songs.Children.Add(SwipeSongRow("Title", "Artist", 3));
        panel.Children.Add(songs);

        panel.Children.Add(SectionHeader("Artist"));
        var artists = new List<Control>();
        for (var i = 1; i <= 8; i++) artists.Add(Avatar(i, 48));
        panel.Children.Add(Carousel(artists));
        var supporting = new TextBlock
        {
            Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 4, 16, 16)
        };
        MaterialTypography.SetStyle(supporting, MaterialTypeStyle.BodyMedium);
        supporting.Bind(TextBlock.ForegroundProperty, supporting.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        panel.Children.Add(supporting);
        return panel;
    }

    private static Control CaptionRow(string label, double width, string? supporting)
    {
        var stack = new StackPanel { Width = width, Spacing = 0, Margin = new Thickness(16, 0, 0, 0) };
        var title = new TextBlock { Text = label };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleSmall);
        stack.Children.Add(title);
        if (supporting is not null)
        {
            var sub = new TextBlock { Text = supporting };
            MaterialTypography.SetStyle(sub, MaterialTypeStyle.BodySmall);
            sub.Bind(TextBlock.ForegroundProperty, sub.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
            stack.Children.Add(sub);
        }
        return stack;
    }

    private static Control SwipeSongRow(string title, string artist, int avatarIndex)
    {
        var row = new MaterialSwipeListItem
        {
            Content = ListRow(title, artist, leading: Avatar(avatarIndex, 40), trailing: "3:24")
        };
        row.SwipeActions.Add(new MaterialSwipeAction { Label = "Favorite", Icon = Geometry("favorite") });
        row.SwipeActions.Add(new MaterialSwipeAction { Label = "Delete", Icon = Geometry("delete"), IsPrimary = true });
        return row;
    }

    private static Control UpcomingBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = mobile ? 8 : 16, Margin = mobile ? new Thickness(0) : new Thickness(24) };

        var tiles = new List<Control>();
        for (var i = 1; i <= 4; i++) tiles.Add(Tile("Label", 160, 200, "Supporting Text"));
        panel.Children.Add(Carousel(tiles));

        var showAll = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(16, 8, 16, 0)
        };
        var showAllLabel = new TextBlock { Text = "Show all", VerticalAlignment = VerticalAlignment.Center };
        MaterialTypography.SetStyle(showAllLabel, MaterialTypeStyle.LabelLarge);
        showAllLabel.Bind(TextBlock.ForegroundProperty, showAllLabel.GetResourceObservable("MaterialPrimaryBrush"));
        showAll.Children.Add(showAllLabel);
        showAll.Children.Add(Glyph("chevron_forward", 18, "MaterialPrimaryBrush"));
        panel.Children.Add(showAll);

        panel.Children.Add(SectionHeader("Section title"));
        var list = new StackPanel { Spacing = 4 };
        for (var i = 0; i < 5; i++)
            list.Children.Add(ListRow("List item", "Supporting line text lorem ipsum dolor sit amet, consectetur.",
                "Overline", "100+", Avatar(i + 3, 40)));
        panel.Children.Add(list);
        return panel;
    }

    private static Control DetailedBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = 12, Margin = mobile ? new Thickness(16) : new Thickness(24) };
        var headline = new TextBlock { Text = "Headline" };
        MaterialTypography.SetStyle(headline, MaterialTypeStyle.HeadlineMedium);
        panel.Children.Add(headline);

        var supporting = new TextBlock { Text = "supporting text" };
        MaterialTypography.SetStyle(supporting, MaterialTypeStyle.BodyMedium);
        supporting.Bind(TextBlock.ForegroundProperty, supporting.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        panel.Children.Add(supporting);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(ActionButton("Download", "download"));
        var published = new TextBlock { Text = "Published date", VerticalAlignment = VerticalAlignment.Center };
        MaterialTypography.SetStyle(published, MaterialTypeStyle.LabelMedium);
        published.Bind(TextBlock.ForegroundProperty, published.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        actions.Children.Add(published);
        panel.Children.Add(actions);

        var body = new TextBlock
        {
            Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut " +
                   "labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco " +
                   "laboris nisi ut aliquip ex ea commodo consequat.\n\nDuis aute irure dolor in reprehenderit in " +
                   "voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat " +
                   "non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640
        };
        MaterialTypography.SetStyle(body, MaterialTypeStyle.BodyLarge);
        panel.Children.Add(body);

        panel.Children.Add(SectionHeader("Section title"));
        var related = new StackPanel { Spacing = 8 };
        for (var i = 0; i < 3; i++)
        {
            var card = new MaterialCard
            {
                Variant = MaterialCardVariant.Outlined,
                Layout = MaterialCardLayout.MediaText,
                Orientation = MaterialCardOrientation.Horizontal,
                Width = mobile ? 380 : 560,
                Header = "Title",
                Subtitle = "Description duis aute irure dolor in reprehenderit in voluptate velit.",
                SupportingText = "Today \u2022 23 min",
                Media = Artwork(),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            related.Children.Add(card);
        }
        panel.Children.Add(related);
        return panel;
    }

    private static Control ReviewsBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = 12, Margin = mobile ? new Thickness(16) : new Thickness(24) };
        var title = new TextBlock { Text = "Title" };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.HeadlineSmall);
        panel.Children.Add(title);
        var subtitle = new TextBlock { Text = "Subtitle" };
        MaterialTypography.SetStyle(subtitle, MaterialTypeStyle.BodyMedium);
        subtitle.Bind(TextBlock.ForegroundProperty, subtitle.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        panel.Children.Add(subtitle);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(ActionButton("Add to my itinerary", "add"));
        var distance = new TextBlock { Text = "12 mins from hotel", VerticalAlignment = VerticalAlignment.Center };
        MaterialTypography.SetStyle(distance, MaterialTypeStyle.LabelMedium);
        distance.Bind(TextBlock.ForegroundProperty, distance.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        actions.Children.Add(distance);
        panel.Children.Add(actions);

        panel.Children.Add(SectionHeader("Within 5 miles  \u2022 $$-$$$"));

        var chips = new MaterialChipGroup
        {
            SelectionMode = MaterialChipSelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var label in new[] { "Label", "Label", "Label" })
            chips.Items.Add(new MaterialChip { Kind = MaterialChipKind.Filter, Content = label });
        panel.Children.Add(chips);

        var list = new StackPanel { Spacing = 4 };
        for (var i = 0; i < 3; i++)
            list.Children.Add(ListRow("List item", "Supporting line text lorem ipsum dolor sit amet, consectetur.",
                "Category \u2022 $$ \u2022 1.2 miles away", "100+", Avatar(i + 5, 40)));
        panel.Children.Add(list);
        panel.Children.Add(TextAction("View 231 restaurants"));
        return panel;
    }

    private static Control LibraryBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = 8, Margin = mobile ? new Thickness(0) : new Thickness(24) };

        var chips = new MaterialChipGroup
        {
            Layout = MaterialChipGroupLayout.SingleRow,
            SelectionMode = MaterialChipSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(16, 4, 16, 0)
        };
        for (var i = 0; i < 10; i++)
            chips.Items.Add(new MaterialChip
            {
                Kind = MaterialChipKind.Filter,
                Content = "Label",
                IsSelected = i == 1
            });
        panel.Children.Add(chips);

        var sorting = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(16, 4, 16, 4)
        };
        sorting.Children.Add(TextAction("Sort by"));
        sorting.Children.Add(IconButton("swap_vert", "Change sort direction"));
        panel.Children.Add(sorting);

        var grid = new WrapPanel { ItemSpacing = 8, LineSpacing = 12, Margin = new Thickness(16) };
        var dates = new[] { "Updated today", "Updated yesterday", "Updated 2 days ago" };
        for (var i = 0; i < 12; i++)
        {
            var card = new MaterialCard
            {
                Variant = MaterialCardVariant.Filled,
                Layout = MaterialCardLayout.MediaText,
                Width = 121,
                Media = Artwork(),
                Title = "Title",
                SupportingText = dates[i % 3]
            };
            grid.Children.Add(card);
        }
        panel.Children.Add(grid);
        return panel;
    }

    private static Control GalleryBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = 8, Margin = mobile ? new Thickness(0) : new Thickness(24) };

        // The source mosaic: full-width row, two uneven tiles, full-width, then the mirrored pair.
        panel.Children.Add(Tile("Label", 0, 176));
        var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("158*,250*") };
        var left = Tile("Label", 0, 176);
        var right = Tile("Label", 0, 176);
        left.Margin = new Thickness(0, 0, 4, 0);
        right.Margin = new Thickness(4, 0, 0, 0);
        pair.Children.Add(left);
        Grid.SetColumn(right, 1);
        pair.Children.Add(right);
        panel.Children.Add(pair);
        panel.Children.Add(Tile("Label", 0, 176));
        var mirror = new Grid { ColumnDefinitions = new ColumnDefinitions("250*,158*") };
        var wideLeft = Tile("Label", 0, 176);
        var narrowRight = Tile("Label", 0, 176);
        wideLeft.Margin = new Thickness(0, 0, 4, 0);
        narrowRight.Margin = new Thickness(4, 0, 0, 0);
        mirror.Children.Add(wideLeft);
        Grid.SetColumn(narrowRight, 1);
        mirror.Children.Add(narrowRight);
        panel.Children.Add(mirror);

        panel.Children.Add(SectionHeader("Section title"));
        var cards = new List<Control>();
        for (var i = 1; i <= 9; i++)
        {
            var stack = new StackPanel { Spacing = 4, Width = 96 };
            stack.Children.Add(Tile("Label", 96, 96));
            var caption = new TextBlock { Text = "Label" };
            MaterialTypography.SetStyle(caption, MaterialTypeStyle.BodyMedium);
            stack.Children.Add(caption);
            cards.Add(stack);
        }
        panel.Children.Add(Carousel(cards));
        return panel;
    }

    private static Control MessagingBody(bool mobile)
    {
        var panel = new StackPanel { Spacing = 6, Margin = mobile ? new Thickness(16, 8, 16, 8) : new Thickness(24) };

        panel.Children.Add(Subhead("Yesterday"));
        panel.Children.Add(Incoming("So excited!", 1));
        panel.Children.Add(Incoming("What should we make?", 1));
        panel.Children.Add(Incoming("Pasta?", 1));

        var link = new MaterialCard
        {
            Variant = MaterialCardVariant.Filled,
            Layout = MaterialCardLayout.MediaText,
            Width = 224,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 4),
            Media = Artwork(),
            Title = "Homemade Dumplings",
            SupportingText = "everydumplingever.com"
        };
        panel.Children.Add(link);
        panel.Children.Add(Outgoing("or we could make this?"));
        panel.Children.Add(Incoming("Sounds good!", 1));

        var predictive = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var text in new[] { "Let's do it", "Great!", "Great!" })
        {
            var chip = new MaterialChip { Kind = MaterialChipKind.Suggestion, Content = text };
            predictive.Children.Add(chip);
        }
        panel.Children.Add(predictive);

        var composer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        composer.Children.Add(IconButton("add_circle", "Add attachment"));
        composer.Children.Add(IconButton("mood", "Emoji"));
        var search = new MaterialSearchBar
        {
            PlaceholderText = "Hinted search text",
            Width = 300,
            // The compact composer is narrower than the source's 360 DIP collapsed minimum.
            MinWidth = 0,
            ShowTrailingIcon = true,
            TrailingIcon = Geometry("send"),
            TrailingIconName = "Send"
        };
        composer.Children.Add(search);
        panel.Children.Add(composer);
        return panel;
    }

    private static Control MessagingWebBody()
    {
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("313,*") };
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        list.Children.Add(Subhead("Conversations"));
        for (var i = 0; i < 7; i++)
            list.Children.Add(ListRow("Name", "Supporting line text lorem ipsum dolor sit amet.", "Overline", "10 min",
                Avatar(i + 1, 40)));
        var listScroll = new ScrollViewer { Content = list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        split.Children.Add(listScroll);
        var thread = MessagingBody(mobile: false);
        Grid.SetColumn(thread, 1);
        split.Children.Add(thread);
        return split;
    }

    private static Control Subhead(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 4) };
        MaterialTypography.SetStyle(block, MaterialTypeStyle.LabelLarge);
        block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        return block;
    }

    private static Control Incoming(string text, int avatarIndex)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(Avatar(avatarIndex, 36));
        var bubble = new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(4, 20, 20, 20),
            MaxWidth = 260,
            Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
        };
        bubble.Bind(Border.BackgroundProperty, bubble.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
        row.Children.Add(bubble);
        return row;
    }

    private static Control Outgoing(string text)
    {
        var bubble = new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(20, 4, 20, 20),
            MaxWidth = 260,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }
        };
        bubble.Bind(Border.BackgroundProperty, bubble.GetResourceObservable("MaterialPrimaryContainerBrush"));
        bubble.Child.Bind(TextBlock.ForegroundProperty,
            bubble.GetResourceObservable("MaterialOnPrimaryContainerBrush"));
        return bubble;
    }

    private static IImage LoadCarouselArtwork()
    {
        var uri = new Uri("avares://ExpressiveAvalonia/Assets/DesignSystem/Carousel/carousel-item.png");
        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }
}
