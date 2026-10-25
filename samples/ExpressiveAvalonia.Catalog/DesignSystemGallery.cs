using Avalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

internal static class DesignSystemGallery
{
    public static TabControl Create()
    {
        var pages = new TabControl();
        ExpressiveAvalonia.Theming.MaterialTabs.SetIsScrollable(pages, true);
        pages.Items.Add(new TabItem { Header = "Buttons", Content = ButtonsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Selection", Content = SelectionGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Navigation rail", Content = NavigationRailGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Snackbar", Content = SnackbarGallery.Create() });
        pages.Items.Add(new TabItem { Header = "FAB", Content = FabGallery.Create() });
        pages.Items.Add(new TabItem { Header = "XR", Content = XrGallery.Create() });
        pages.Items.Add(new TabItem { Header = "App bars", Content = AppBarsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Button groups", Content = ButtonGroupsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Chips", Content = ChipsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Dialogs", Content = DialogsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Sheets", Content = SheetsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Navigation bar", Content = NavigationBarGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Navigation drawer", Content = NavigationDrawerGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Search", Content = SearchGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Date pickers", Content = DatePickersGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Lists", Content = ListsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Tabs", Content = TabsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Text fields", Content = TextFieldsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Sliders", Content = SlidersGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Progress", Content = ProgressGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Menus", Content = MenusGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Tooltips", Content = TooltipsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Shapes", Content = ShapesGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Avatars", Content = AvatarsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Assets", Content = AssetsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Cards", Content = CardsGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Carousel", Content = CarouselGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Time pickers", Content = TimePickersGallery.Create() });
        pages.Items.Add(new TabItem { Header = "Examples", Content = ExamplesGallery.Create() });
        return pages;
    }
}
