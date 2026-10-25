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
        var nav = new Grid { RowDefinitions = new RowDefinitions("56,56,*"), RowSpacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        var collapseIcon = new PathIcon
        {
            Data = Geometry.Parse("M3,6 L15,6 L15,8 L3,8 Z M3,11 L12,11 L12,13 L3,13 Z M3,16 L15,16 L15,18 L3,18 Z M20,7 L22,9 L19,12 L22,15 L20,17 L15,12 Z"),
            Width = 24,
            Height = 24
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
        var viewport = new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
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
        SelectPage(expressive, 0);
    }

    private static Geometry NavigationIcon(string? page) => Geometry.Parse(page switch
    {
        "Buttons" or "Button groups" or "Chips" => "M6,6 L18,6 A6,6 0 0 1 18,18 L6,18 A6,6 0 0 1 6,6 Z M7,11 L17,11 L17,13 L7,13 Z",
        "Selection" => "M3,3 L21,3 L21,21 L3,21 Z M6,12 L10,16 L18,8 L16,6 L10,12 L8,10 Z",
        "Navigation rail" or "Navigation drawer" => "M3,3 L21,3 L21,21 L3,21 Z M5,5 L5,19 L9,19 L9,5 Z M11,5 L11,19 L19,19 L19,5 Z",
        "FAB" => "M12,2 A10,10 0 1 1 11.99,2 Z M11,6 L11,11 L6,11 L6,13 L11,13 L11,18 L13,18 L13,13 L18,13 L18,11 L13,11 L13,6 Z",
        "Text fields" or "Input" or "Typography" => "M3,4 L21,4 L21,8 L19,8 L19,6 L13,6 L13,20 L11,20 L11,6 L5,6 L5,8 L3,8 Z",
        "Sliders" or "Ranges" => "M3,7 L8,7 L8,4 L10,4 L10,12 L8,12 L8,9 L3,9 Z M12,7 L21,7 L21,9 L12,9 Z M3,16 L14,16 L14,13 L16,13 L16,21 L14,21 L14,18 L3,18 Z M18,16 L21,16 L21,18 L18,18 Z",
        "Lists" or "Collections" or "Menus" => "M3,4 L6,4 L6,7 L3,7 Z M9,4 L21,4 L21,7 L9,7 Z M3,10 L6,10 L6,13 L3,13 Z M9,10 L21,10 L21,13 L9,13 Z M3,16 L6,16 L6,19 L3,19 Z M9,16 L21,16 L21,19 L9,19 Z",
        "Search" => "M10,2 A8,8 0 1 1 4.34,15.66 A8,8 0 0 1 10,2 Z M10,4 A6,6 0 1 0 10,16 A6,6 0 0 0 10,4 Z M15,14 L22,21 L20,23 L13,16 Z",
        "Date pickers" or "Time pickers" => "M5,2 L7,2 L7,4 L17,4 L17,2 L19,2 L19,4 L22,4 L22,22 L2,22 L2,4 L5,4 Z M4,9 L4,20 L20,20 L20,9 Z",
        _ => "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z"
    });

    internal void SelectPage(bool expressive, int index)
    {
        if (_isExpressive != expressive)
        {
            ChangeSection(expressive);
        }
        var pages = expressive ? ExpressivePages : ClassicPages;
        var item = (TabItem)pages.Items[index]!;
        if (_selectedItem != item)
        {
            var previous = CurrentPage;
            if (previous is not null) previous.PropertyChanged -= PreviewSizeChanged;
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
