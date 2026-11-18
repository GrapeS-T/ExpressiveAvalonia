using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

internal sealed partial class CatalogWindow
{
    internal TabControl ExpressivePages { get; private set; } = null!;
    internal CatalogInspector Inspector { get; private set; } = null!;
    internal Control? CurrentPage { get; private set; }
    private readonly Border _preview = new() { ClipToBounds = true };
    private readonly MaterialNavigationRail _navigation = new()
    {
        Mode = MaterialNavigationRailMode.Wide,
        IsExpanded = true,
        ShowFab = false,
        ShowMenuButton = false,
    };
    private readonly TextBlock _pageTitle = new();
    private readonly TextBlock _sectionLabel = new();
    private TabItem? _selectedItem;
    private bool _changingSection;

    private void BuildWorkspace()
    {
        ExpressivePages = DesignSystemGallery.Create();
        Inspector = new CatalogInspector(_theme, this, ResetCurrentPage);
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,380"),
            ColumnSpacing = 16,
            Margin = new Thickness(12),
        };

        var nav = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        nav.Bind(WidthProperty, new Binding(nameof(Width)) { Source = _navigation });

        var collapse = new Button
        {
            Theme = (Avalonia.Styling.ControlTheme)Application.Current!.FindResource("MaterialNavigationRailMenuButtonTheme")!,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new PathIcon { Width = 24, Height = 24, Data = NavigationGlyph("menu_open") },
        };
        AutomationProperties.SetName(collapse, "Свернуть навигацию");
        ToolTip.SetTip(collapse, "Свернуть или развернуть навигацию");

        MaterialExtendedFab settings = null!;
        collapse.Click += (_, _) =>
        {
            _navigation.IsExpanded = !_navigation.IsExpanded;
            ((PathIcon)collapse.Content!).Data = NavigationGlyph(_navigation.IsExpanded ? "menu_open" : "menu");
            settings.IsExpanded = _navigation.IsExpanded;
            AutomationProperties.SetName(
                collapse,
                _navigation.IsExpanded ? "Свернуть навигацию" : "Развернуть навигацию");
        };

        var header = new Border
        {
            Margin = new Thickness(20, 24, 20, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = collapse,
        };
        nav.Children.Add(header);

        _navigation.SelectionChanged += (_, _) =>
        {
            if (!_changingSection && _navigation.SelectedIndex >= 0)
                SelectPage(_navigation.SelectedIndex);
        };
        var railScroll = new ScrollViewer
        {
            Content = _navigation,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        Grid.SetRow(railScroll, 1);
        nav.Children.Add(railScroll);

        settings = new MaterialExtendedFab
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Mode = MaterialExtendedFabMode.Classic,
            ColorRole = MaterialFabColorRole.PrimaryContainer,
            Label = "Настройки",
            Icon = NavigationGlyph("settings"),
        };
        settings.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Border>("Surface") is { } surface)
                surface.BoxShadow = new BoxShadows();
        };
        ToolTip.SetTip(settings, "Настройки студии");
        AutomationProperties.SetName(settings, "Настройки студии");

        Window? settingsWindow = null;
        settings.Click += (_, _) =>
        {
            if (settingsWindow is not null)
            {
                settingsWindow.Activate();
                return;
            }
            settingsWindow = new Window
            {
                Title = "Настройки студии",
                Width = 440,
                Height = 680,
                MinWidth = 360,
                MinHeight = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer
                {
                    Content = Inspector.StudioSettings,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                },
            };
            settingsWindow.Bind(
                BackgroundProperty,
                settingsWindow.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
            settingsWindow.Closed += (_, _) =>
            {
                ((ScrollViewer)settingsWindow.Content!).Content = null;
                settingsWindow = null;
            };
            settingsWindow.Show(this);
        };

        var footer = new Border
        {
            Margin = new Thickness(16, 8, 16, 16),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = settings,
        };
        Grid.SetRow(footer, 2);
        nav.Children.Add(footer);
        body.Children.Add(nav);

        var stage = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 20,
            Margin = new Thickness(24),
        };
        var introduction = new StackPanel { Spacing = 8, Margin = new Thickness(8, 12, 8, 0) };
        _sectionLabel.FontSize = 12;
        _sectionLabel.FontWeight = FontWeight.Bold;
        _sectionLabel.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("MaterialPrimaryBrush"));
        _pageTitle.FontSize = 40;
        _pageTitle.FontWeight = FontWeight.Bold;
        introduction.Children.Add(_sectionLabel);
        introduction.Children.Add(_pageTitle);
        stage.Children.Add(introduction);

        var viewport = new ScrollViewer
        {
            Content = _preview,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        viewport.SizeChanged += (_, _) => UpdatePreviewWidth(viewport.Bounds.Width);
        Grid.SetRow(viewport, 1);
        stage.Children.Add(viewport);

        var canvas = Surface(stage, "MaterialSurfaceContainerLowestBrush", 36);
        canvas.ClipToBounds = true;
        Grid.SetColumn(canvas, 1);
        body.Children.Add(canvas);

        Grid.SetColumn(Inspector, 2);
        body.Children.Add(Inspector);
        Content = body;

        foreach (var item in ExpressivePages.Items.Cast<TabItem>())
        {
            var title = item.Header?.ToString() ?? string.Empty;
            var destination = new MaterialNavigationRailItem
            {
                Content = title,
                Label = NavigationLabel(title),
                Icon = NavigationIcon(title),
                ShowLabel = true,
            };
            AutomationProperties.SetName(destination, title);
            ToolTip.SetTip(destination, title);
            _navigation.Items.Add(destination);
        }

        if (ExpressivePages.ItemCount > 0)
            SelectPage(0);
    }

    private void ResetCurrentPage()
    {
        if (CurrentPage is not DeferredGallery deferred)
            return;
        deferred.Reset();
        Inspector.SetPage(deferred);
        if (_preview.Parent is ScrollViewer viewport)
            UpdatePreviewWidth(viewport.Bounds.Width);
    }

    private Border Surface(Control child, string brush, double radius)
    {
        var border = new Border { Child = child, CornerRadius = new CornerRadius(radius) };
        border.Bind(BackgroundProperty, this.GetResourceObservable(brush));
        return border;
    }

    private static Geometry NavigationGlyph(string name) => MaterialIcons.TryGetByName(name, out var icon)
        ? icon.Geometry
        : throw new InvalidOperationException($"Missing Material icon: {name}");

    private static string NavigationLabel(string title) => title switch
    {
        "Navigation rail" => "Nav rail",
        "Navigation bar" => "Nav bar",
        "Navigation drawer" => "Drawer",
        "Date pickers" => "Dates",
        "Time pickers" => "Time",
        "Text fields" => "Text",
        "Button groups" => "Groups",
        "Pull to refresh" => "Refresh",
        "Scroll fields" => "Scroll",
        _ => title
    };

    private static Geometry NavigationIcon(string page) => NavigationGlyph(page switch
    {
        "Buttons" => "add_circle", "Selection" => "check_box", "Navigation rail" => "menu_open",
        "Snackbar" => "notifications", "FAB" => "add", "XR" => "stars_filled",
        "App bars" => "drag_handle", "Button groups" => "groups", "Chips" => "bookmark",
        "Dialogs" => "chat_bubble", "Sheets" => "archive", "Navigation bar" => "tab",
        "Navigation drawer" => "menu", "Search" => "search", "Date pickers" => "today",
        "Lists" => "inbox", "Tabs" => "tab", "Text fields" => "text_fields",
        "Sliders" => "swap_horiz", "Progress" => "schedule", "Menus" => "more_vert",
        "Tooltips" => "comment", "Shapes" => "stars", "Avatars" => "account_circle",
        "Assets" => "image", "Cards" => "sticker", "Carousel" => "photo",
        "Time pickers" => "schedule", "Examples" => "folder", "Pull to refresh" => "swap_vert",
        "Scroll fields" => "swap_vert", "Scrollbars" => "drag_handle", _ => "folder"
    });

    internal void SelectPage(int index)
    {
        var item = (TabItem)ExpressivePages.Items[index]!;
        if (_selectedItem != item)
        {
            var previous = CurrentPage;
            if (previous is not null)
                previous.PropertyChanged -= PreviewSizeChanged;

            SetPreviewContent(null);
            if (_selectedItem is not null)
                _selectedItem.Content = previous;

            var page = (Control)item.Content!;
            item.Content = null;
            _selectedItem = item;
            CurrentPage = page;
            (page as DeferredGallery)?.Realize();
            SetPreviewContent(page);
            Inspector.SetPage(page);
            page.PropertyChanged += PreviewSizeChanged;
            if (_preview.Parent is ScrollViewer viewport)
                UpdatePreviewWidth(viewport.Bounds.Width);
        }
        _changingSection = true;
        _navigation.SelectedIndex = index;
        _changingSection = false;
        _sectionLabel.Text = "MD3 EXPRESSIVE / РАСШИРЕННЫЕ КОМПОНЕНТЫ";
        _pageTitle.Text = item.Header?.ToString();
    }

    private void PreviewSizeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WidthProperty && _preview.Parent is ScrollViewer viewport)
            UpdatePreviewWidth(viewport.Bounds.Width);
    }

    private void UpdatePreviewWidth(double available)
    {
        if (available <= 0) return;
        _preview.Width = CurrentPage is { Width: var width } && double.IsFinite(width)
            ? Math.Max(available, width) : available;
    }

    internal void SetPreviewContent(Control? page) => _preview.Child = page;
}
