using Avalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

internal static class DesignSystemGallery
{
    public static TabControl Create()
    {
        var pages = new TabControl();
        ExpressiveAvalonia.Theming.MaterialTabs.SetIsScrollable(pages, true);
        Add("Buttons", ButtonsGallery.Create);
        Add("Selection", SelectionGallery.Create);
        Add("Navigation rail", NavigationRailGallery.Create);
        Add("Snackbar", SnackbarGallery.Create);
        Add("FAB", FabGallery.Create);
        Add("XR", XrGallery.Create);
        Add("App bars", AppBarsGallery.Create);
        Add("Button groups", ButtonGroupsGallery.Create);
        Add("Chips", ChipsGallery.Create);
        Add("Dialogs", DialogsGallery.Create);
        Add("Sheets", SheetsGallery.Create);
        Add("Navigation bar", NavigationBarGallery.Create);
        Add("Navigation drawer", NavigationDrawerGallery.Create);
        Add("Search", SearchGallery.Create);
        Add("Date pickers", DatePickersGallery.Create);
        Add("Lists", ListsGallery.Create);
        Add("Tabs", TabsGallery.Create);
        Add("Text fields", TextFieldsGallery.Create);
        Add("Sliders", SlidersGallery.Create);
        Add("Progress", ProgressGallery.Create);
        Add("Menus", MenusGallery.Create);
        Add("Tooltips", TooltipsGallery.Create);
        Add("Shapes", ShapesGallery.Create);
        Add("Avatars", AvatarsGallery.Create);
        Add("Assets", AssetsGallery.Create);
        Add("Cards", CardsGallery.Create);
        Add("Carousel", CarouselGallery.Create);
        Add("Time pickers", TimePickersGallery.Create);
        Add("Examples", ExamplesGallery.Create);
        Add("Pull to refresh", PullToRefreshGallery.Create);
        Add("Scroll fields", ScrollFieldGallery.Create);
        Add("Scrollbars", ScrollbarsGallery.Create);
        void Add(string title, Func<Control> create) => pages.Items.Add(new TabItem { Header = title, Content = new DeferredGallery(create) });
        return pages;
    }
}
