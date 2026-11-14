using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

internal sealed partial class CatalogWindow
{
    internal TabControl ClassicPages { get; private set; } = null!;
    internal TabControl ExpressivePages { get; private set; } = null!;
    internal CatalogInspector Inspector { get; private set; } = null!;
    internal Control? CurrentPage { get; private set; }
    private readonly Border _preview = new() { ClipToBounds = true };
    private readonly MaterialNavigationRail _navigation = new() { IsExpanded = true, ShowFab = false, ShowMenuButton = false };
    private readonly TextBlock _pageTitle = new();
    private readonly TextBlock _sectionLabel = new();
    private readonly ToggleButton _edition = new() { Content = "MD3E", IsChecked = true, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(12, 0), Width = 72, Height = 56 };
    private TabItem? _selectedItem;
    private bool _isExpressive = true;
    private bool _changingSection;

    private void BuildWorkspace()
    {
        ClassicPages = (TabControl)StandardControlsGallery.Create(_theme);
        ExpressivePages = DesignSystemGallery.Create();
        Inspector = new CatalogInspector(_theme, this);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*,380"), ColumnSpacing = 16, Margin = new Thickness(12) };
        var nav = new Grid { RowDefinitions = new RowDefinitions("56,56,*,Auto"), RowSpacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        var collapseIcon = new PathIcon
        {
            Data = Geometry.Parse("M3,6 L15,6 L15,8 L3,8 Z M3,11 L12,11 L12,13 L3,13 Z M3,16 L15,16 L15,18 L3,18 Z M20,7 L22,9 L19,12 L22,15 L20,17 L15,12 Z"),
            Width = 24,
            Height = 24,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new Avalonia.Media.ScaleTransform(1, 1)
        };
        collapseIcon.Bind(ForegroundProperty, this.GetResourceObservable("MaterialOnSurfaceBrush"));
        // A local template keeps the navigation glyph independent of the gallery's icon-button anatomy.
        var collapse = new Button { Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center };
        collapse.Template = new Avalonia.Controls.Templates.FuncControlTemplate<Button>((button, scope) =>
        {
            var background = new Border { CornerRadius = new CornerRadius(28), Child = collapseIcon };
            background.Bind(BackgroundProperty, button.GetResourceObservable("MaterialSurfaceContainerBrush"));
            collapseIcon.HorizontalAlignment = HorizontalAlignment.Center;
            collapseIcon.VerticalAlignment = VerticalAlignment.Center;
            return background;
        });
        Avalonia.Automation.AutomationProperties.SetName(collapse, "Свернуть или развернуть навигацию");
        collapse.Click += (_, _) =>
        {
            _navigation.IsExpanded = !_navigation.IsExpanded;
            body.ColumnDefinitions[0].Width = new GridLength(_navigation.IsExpanded ? 220 : 96);
            collapseIcon.RenderTransform = new Avalonia.Media.ScaleTransform(_navigation.IsExpanded ? 1 : -1, 1);
            Avalonia.Automation.AutomationProperties.SetName(collapse, _navigation.IsExpanded ? "Свернуть навигацию" : "Развернуть навигацию");
        };
        nav.Children.Add(collapse);
        _edition.Margin = new Thickness(0);
        MaterialAssist.SetButtonVariant(_edition, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(_edition, MaterialControlSize.Medium);
        MaterialAssist.SetButtonShape(_edition, MaterialButtonShape.Round);
        Avalonia.Automation.AutomationProperties.SetName(_edition, "Material 3 Expressive");
        ToolTip.SetTip(_edition, "Включено — MD3 Expressive; выключено — MD3 Classic");
        _edition.IsCheckedChanged += (_, _) => { if (!_changingSection) ChangeSection(_edition.IsChecked == true); };
        Grid.SetRow(_edition, 1);
        nav.Children.Add(_edition);
        _navigation.SelectionChanged += (_, _) =>
        {
            if (!_changingSection && _navigation.SelectedIndex >= 0)
                SelectPage(_isExpressive, _navigation.SelectedIndex);
        };
        var railScroll = new ScrollViewer { Content = _navigation, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(railScroll, 2);
        nav.Children.Add(railScroll);
        var settings = new Button { Content = "Настройки", HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 0), Height = 56 };
        ToolTip.SetTip(settings, "Настройки студии");
        Avalonia.Automation.AutomationProperties.SetName(settings, "Настройки студии");
        Window? settingsWindow = null;
        settings.Click += (_, _) =>
        {
            if (settingsWindow is not null) { settingsWindow.Activate(); return; }
            settingsWindow = new Window
            {
                Title = "Настройки студии", Width = 440, Height = 720, MinWidth = 360, MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer { Content = Inspector.StudioSettings, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
            };
            settingsWindow.Closed += (_, _) =>
            {
                ((ScrollViewer)settingsWindow.Content!).Content = null;
                settingsWindow = null;
            };
            settingsWindow.Show(this);
        };
        Grid.SetRow(settings, 3);
        nav.Children.Add(settings);
        body.Children.Add(nav);

        var stage = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 20, Margin = new Thickness(24) };
        var introduction = new StackPanel { Spacing = 8, Margin = new Thickness(8, 12, 8, 0) };
        _sectionLabel.FontSize = 12;
        _sectionLabel.FontWeight = FontWeight.Bold;
        _sectionLabel.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("MaterialPrimaryBrush"));
        _pageTitle.FontSize = 40;
        _pageTitle.FontWeight = FontWeight.Bold;
        introduction.Children.Add(_sectionLabel);
        introduction.Children.Add(_pageTitle);
        stage.Children.Add(introduction);
        var viewport = new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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
        ChangeSection(true);
        Opened += (_, _) => { if (CurrentPage is { } page) Inspector.SetPage(page, _isExpressive); };
    }

    private Border Surface(Control child, string brush, double radius)
    {
        var border = new Border { Child = child, CornerRadius = new CornerRadius(radius) };
        border.Bind(BackgroundProperty, this.GetResourceObservable(brush));
        return border;
    }

    private void ChangeSection(bool expressive)
    {
        _isExpressive = expressive;
        _changingSection = true;
        _edition.IsChecked = expressive;
        _edition.Content = expressive ? "MD3E" : "MD3";
        var pages = expressive ? ExpressivePages : ClassicPages;
        _navigation.Items.Clear();
        foreach (var item in pages.Items.Cast<TabItem>())
            _navigation.Items.Add(new MaterialNavigationRailItem { Content = item.Header, Icon = NavigationIcon(item.Header?.ToString()), ShowLabel = true });
        _navigation.SelectedIndex = 0;
        _changingSection = false;
        if (pages.ItemCount > 0) SelectPage(expressive, 0);
    }

    private static Geometry NavigationIcon(string? page) => Geometry.Parse(page switch
    {
        "Buttons" => "F0 M19,7H5C3.9,7,3,7.9,3,9v6c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V9C21,7.9,20.1,7,19,7z M19,15H5V9h14V15z M7,11h10v2H7V11z",
        "Selection" => "F0 M19,3H5C3.9,3,3,3.9,3,5v14c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V5C21,3.9,20.1,3,19,3z M19,19H5V5h14V19z M10,17l-5-5l1.41-1.41L10,14.17l7.59-7.59L19,8L10,17z",
        "Navigation rail" => "F0 M4,4h16c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H4c-1.1,0-2-0.9-2-2V6C2,4.9,2.9,4,4,4z M4,6v12h5V6H4z M11,6v12h9V6H11z M5,8h3v2H5V8z M5,12h3v2H5V12z",
        "Snackbar" => "F0 M19,5H5C3.9,5,3,5.9,3,7v10c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V7C21,5.9,20.1,5,19,5z M19,17H5V7h14V17z M7,13h7v2H7V13z M7,9h10v2H7V9z",
        "FAB" => "F0 M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M12,20c-4.41,0-8-3.59-8-8s3.59-8,8-8s8,3.59,8,8S16.41,20,12,20z M13,7h-2v4H7v2h4v4h2v-4h4v-2h-4V7z",
        "XR" => "F0 M20,6H4C2.9,6,2,6.9,2,8v8c0,1.1,0.9,2,2,2h4.5l2-2h3l2,2H20c1.1,0,2-0.9,2-2V8C22,6.9,21.1,6,20,6z M7,14c-1.66,0-3-1.34-3-3s1.34-3,3-3s3,1.34,3,3S8.66,14,7,14z M17,14c-1.66,0-3-1.34-3-3s1.34-3,3-3s3,1.34,3,3S18.66,14,17,14z",
        "App bars" => "F0 M4,4h16c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H4c-1.1,0-2-0.9-2-2V6C2,4.9,2.9,4,4,4z M4,6v3h16V6H4z M4,11v7h16v-7H4z M6,7h3v1H6V7z",
        "Button groups" => "F0 M2,7h20v10H2V7z M4,9v6h4V9H4z M10,9v6h4V9H10z M16,9v6h4V9H16z",
        "Chips" => "F0 M21.41,11.58l-9-9C12.05,2.22,11.55,2,11,2H4C2.9,2,2,2.9,2,4v7c0,0.55,0.22,1.05,0.59,1.42l9,9c0.36,0.36,0.86,0.58,1.41,0.58c0.55,0,1.05-0.22,1.41-0.59l7-7c0.37-0.36,0.59-0.86,0.59-1.41C22,12.44,21.77,11.94,21.41,11.58z M13,20.01L4,11V4h7v0.01l9,9L13,20.01z M6.5,8C5.67,8,5,7.33,5,6.5S5.67,5,6.5,5S8,5.67,8,6.5S7.33,8,6.5,8z",
        "Dialogs" => "F0 M19,4H5C3.89,4,3,4.89,3,6v12c0,1.1,0.89,2,2,2h14c1.1,0,2-0.9,2-2V6C21,4.89,20.1,4,19,4z M19,18H5V6h14V18z M7,8h10v2H7V8z M7,11h7v2H7V11z M13,14h4v2h-4V14z",
        "Sheets" => "F0 M19,3H5C3.9,3,3,3.9,3,5v14c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V5C21,3.9,20.1,3,19,3z M19,11H5V5h14V11z M19,19H5v-6h14V19z M10,14h4v1h-4V14z",
        "Navigation bar" => "F0 M4,4h16c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H4c-1.1,0-2-0.9-2-2V6C2,4.9,2.9,4,4,4z M4,6v8h16V6H4z M4,16v2h16v-2H4z M6,17h2v1H6V17z M11,17h2v1h-2V17z M16,17h2v1h-2V17z",
        "Navigation drawer" => "F0 M4,4h16c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H4c-1.1,0-2-0.9-2-2V6C2,4.9,2.9,4,4,4z M4,6v12h7V6H4z M13,6v12h7V6H13z M5,8h5v2H5V8z M5,11h5v2H5V11z",
        "Search" => "F0 M15.5,14h-0.79l-0.28-0.27C15.41,12.59,16,11.11,16,9.5C16,5.91,13.09,3,9.5,3S3,5.91,3,9.5S5.91,16,9.5,16c1.61,0,3.09-0.59,4.23-1.57l0.27,0.28v0.79l5,4.99L20.49,19L15.5,14z M9.5,14C7.01,14,5,11.99,5,9.5S7.01,5,9.5,5S14,7.01,14,9.5S11.99,14,9.5,14z",
        "Date pickers" => "F0 M19,4h-1V2h-2v2H8V2H6v2H5C3.89,4,3.01,4.9,3.01,6L3,20c0,1.1,0.89,2,2,2h14c1.1,0,2-0.9,2-2V6C21,4.9,20.1,4,19,4z M19,20H5V10h14V20z M19,8H5V6h14V8z M7,12h2v2H7V12z M11,12h2v2h-2V12z M15,12h2v2h-2V12z",
        "Lists" or "Collections" => "F0 M3,13h2v-2H3V13z M3,17h2v-2H3V17z M3,9h2V7H3V9z M7,13h14v-2H7V13z M7,17h14v-2H7V17z M7,7v2h14V7H7z",
        "Tabs" => "F0 M21,3H3C1.9,3,1,3.9,1,5v14c0,1.1,0.9,2,2,2h18c1.1,0,2-0.9,2-2V5C23,3.9,22.1,3,21,3z M21,19H3V9h18V19z M21,7H3V5h18V7z M4,6h4v1H4V6z M10,6h4v1h-4V6z",
        "Text fields" or "Input" => "F0 M4,4h16c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H4c-1.1,0-2-0.9-2-2V6C2,4.9,2.9,4,4,4z M4,6v12h16V6H4z M7,8h10v2H7V8z M7,11h6v2H7V11z",
        "Sliders" or "Ranges" => "F0 M3,17v2h6v-2H3z M3,5v2h10V5H3z M13,21v-2h8v-2h-8v-2h-2v6H13z M7,9v2H3v2h4v2h2V9H7z M21,13v-2H11v2H21z M17,9h4V7h-4V5h-2v6H17z",
        "Progress" => "F0 M12,4V1L8,5l4,4V6c3.31,0,6,2.69,6,6c0,1.01-0.25,1.97-0.7,2.8l1.46,1.46C19.54,15.03,20,13.57,20,12C20,7.58,16.42,4,12,4z M12,18c-3.31,0-6-2.69-6-6c0-1.01,0.25-1.97,0.7-2.8L5.24,7.74C4.46,8.97,4,10.43,4,12c0,4.42,3.58,8,8,8v3l4-4l-4-4V18z",
        "Menus" => "F0 M3,18h18v-2H3V18z M3,13h18v-2H3V13z M3,6v2h18V6H3z",
        "Tooltips" => "F0 M20,2H4C2.9,2,2,2.9,2,4v12c0,1.1,0.9,2,2,2h14l4,4V4C22,2.9,21.1,2,20,2z M20,16H4V4h16V16z M11,12h2v2h-2V12z M11,6h2v4h-2V6z",
        "Shapes" => "F0 M12,2L6,12h12L12,2z M12,6.5l2.7,4.5H9.3L12,6.5z M4,14h6v6H4V14z M18,14a3,3 0 1,1 0,6a3,3 0 0,1 0,-6z",
        "Avatars" => "F0 M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M12,20c-4.41,0-8-3.59-8-8s3.59-8,8-8s8,3.59,8,8S16.41,20,12,20z M12,6c1.66,0,3,1.34,3,3s-1.34,3-3,3s-3-1.34-3-3S10.34,6,12,6z M12,14c2.67,0,8,1.34,8,4v1H4v-1C4,15.34,9.33,14,12,14z",
        "Assets" => "F0 M21,19V5c0-1.1-0.9-2-2-2H5C3.9,2,3,2.9,3,4v14c0,1.1,0.9,2,2,2h14C20.1,20,21,19.1,21,19z M5,18l4-5l3,4l4-5l4,6H5z M19,4v10.5l-3.5-4.5l-4,5l-3-4L5,15.5V4H19z",
        "Cards" => "F0 M20,4H4C2.89,4,2.01,4.89,2.01,6L2,18c0,1.11,0.89,2,2,2h16c1.11,0,2-0.89,2-2V6C22,4.89,21.11,4,20,4z M20,18H4v-6h16V18z M20,8H4V6h16V8z",
        "Carousel" => "F0 M7,19h10V5H7V19z M9,7h6v10H9V7z M2,17h4V7H2V17z M18,7v10h4V7H18z",
        "Time pickers" => "F0 M11.99,2C6.47,2,2,6.48,2,12s4.47,10,9.99,10C17.52,22,22,17.52,22,12S17.52,2,11.99,2z M12,20c-4.42,0-8-3.58-8-8s3.58-8,8-8s8,3.58,8,8S16.42,20,12,20z M12.5,7H11v6l5.25,3.15l0.75-1.23l-4.5-2.67V7z",
        "Typography" => "F0 M5,4v3h5v12h3V7h5V4H5z",
        "Examples" => "F0 M4,4h6v6H4V4z M14,4h6v6h-6V4z M4,14h6v6H4V14z M14,14h6v6h-6V14z",
        _ => "F0 M4,4h6v6H4V4z M14,4h6v6h-6V4z M4,14h6v6H4V14z M14,14h6v6h-6V14z"
    });

    internal void SelectPage(bool expressive, int index)
    {
        if (_isExpressive != expressive)
        {
            _isExpressive = expressive;
            _changingSection = true;
            _edition.IsChecked = expressive;
            _edition.Content = expressive ? "MD3E" : "MD3";
            var destinations = expressive ? ExpressivePages : ClassicPages;
            _navigation.Items.Clear();
            foreach (var destination in destinations.Items.Cast<TabItem>())
                _navigation.Items.Add(new MaterialNavigationRailItem { Content = destination.Header, Icon = NavigationIcon(destination.Header?.ToString()), ShowLabel = true });
            _changingSection = false;
        }
        var pages = expressive ? ExpressivePages : ClassicPages;
        var item = (TabItem)pages.Items[index]!;
        if (_selectedItem != item)
        {
            var previous = CurrentPage;
            if (previous is not null)
            {
                previous.PropertyChanged -= PreviewSizeChanged;
            }
            SetPreviewContent(null);
            if (_selectedItem is not null) _selectedItem.Content = previous;
            var page = (Control)item.Content!;
            item.Content = null;
            _selectedItem = item;
            CurrentPage = page;
            SetPreviewContent(page);
            Inspector.SetPage(page, expressive);
            page.PropertyChanged += PreviewSizeChanged;
            if (_preview.Parent is ScrollViewer viewport) UpdatePreviewWidth(viewport.Bounds.Width);
        }
        _changingSection = true;
        _navigation.SelectedIndex = index;
        _changingSection = false;
        _sectionLabel.Text = expressive ? "MD3 EXPRESSIVE / РАСШИРЕННЫЕ КОМПОНЕНТЫ" : "MD3 CLASSIC / БАЗОВЫЕ КОНТРОЛЫ";
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
