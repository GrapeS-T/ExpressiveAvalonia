using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Gallery for the app bar, bottom app bar, toolbar and header family.</summary>
public static class AppBarsGallery
{
    private static readonly Geometry Back = Geometry.Parse(
        "M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z");
    private static readonly Geometry Menu = Geometry.Parse(
        "M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z");
    private static readonly Geometry Search = Geometry.Parse(
        "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z");
    private static readonly Geometry Favorite = Geometry.Parse(
        "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z");
    private static readonly Geometry Share = Geometry.Parse(
        "M18 16.08c-.76 0-1.44.3-1.96.77L8.91 12.7c.05-.23.09-.46.09-.7s-.04-.47-.09-.7l7.05-4.11c.54.5 1.25.81 2.04.81 1.66 0 3-1.34 3-3s-1.34-3-3-3-3 1.34-3 3c0 .24.04.47.09.7L8.04 9.81C7.5 9.31 6.79 9 6 9c-1.66 0-3 1.34-3 3s1.34 3 3 3c.79 0 1.5-.31 2.04-.81l7.12 4.16c-.05.21-.08.43-.08.65 0 1.61 1.31 2.92 2.92 2.92s2.92-1.31 2.92-2.92-1.31-2.92-2.92-2.92z");
    private static readonly Geometry Edit = Geometry.Parse(
        "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.9959.9959 0 0 0 0-1.41l-2.34-2.34a.9959.9959 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
    private static readonly Geometry Add = Geometry.Parse(
        "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");
    private static readonly Geometry Settings = Geometry.Parse(
        "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z");
    private static readonly Geometry Person = Geometry.Parse(
        "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z");
    private static readonly Geometry Palette = Geometry.Parse(
        "M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z");

    public static Control Create()
    {
        var status = new TextBlock { Text = "Действий: 0", FontSize = 14 };
        var count = 0;

        ICommand Command(string label) => new DelegateCommand(_ =>
        {
            status.Text = $"Действий: {++count} · {label}";
        });

        var panel = new StackPanel { Spacing = 32, Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "App bars, toolbars и заголовки", FontSize = 32, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = "Tab и стрелки перемещают фокус между действиями, Home/End — к краям, Enter/Space выполняют команду, Escape закрывает overflow. " +
                   "Действия сверх лимита уходят в меню «ещё»; полоса с Elevation=OnScroll поднимает тень, когда содержимое прокручивается под ней.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(status);

        // 1. Small app bar.
        var small = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.Small,
            Title = "Входящие",
            LeadingIcon = Back,
            LeadingText = "Назад",
            LeadingCommand = Command("Назад"),
            Actions =
            {
                new MaterialBarAction("Поиск", Search, Command("Поиск")),
                new MaterialBarAction("Избранное", Favorite, Command("Избранное"))
            }
        };
        panel.Children.Add(Demo("App bar · small", "64, заголовок Title Large, ведущая иконка и два действия.", small));

        // 2. Small-centred app bar with a monogram avatar.
        var centered = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.SmallCentered,
            Title = "Фотографии",
            LeadingIcon = Menu,
            LeadingText = "Меню",
            LeadingCommand = Command("Меню"),
            AvatarText = "A",
            Actions = { new MaterialBarAction("Поиск", Search, Command("Поиск")) }
        };
        panel.Children.Add(Demo("App bar · small-centred", "Заголовок по центру, монограмма-аватар 32 в трейлинге.", centered));

        // 3. Small image app bar.
        var image = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.SmallImage,
            LeadingIcon = Back,
            LeadingText = "Назад",
            LeadingCommand = Command("Назад"),
            Thumbnail = Thumbnail("Витрина"),
            Actions =
            {
                new MaterialBarAction("Поделиться", Share, Command("Поделиться")),
                new MaterialBarAction("Избранное", Favorite, Command("Избранное"))
            }
        };
        panel.Children.Add(Demo("App bar · small-image", "Средний слот занят изображением высотой 40 вместо заголовка.", image));

        // 4. Search app bar.
        var search = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.Search,
            SearchPlaceholder = "Поиск продукта",
            LeadingIcon = Menu,
            LeadingText = "Меню",
            LeadingCommand = Command("Меню"),
            AvatarText = "М",
            Actions = { new MaterialBarAction("Поделиться", Share, Command("Поделиться")) }
        };
        search.SearchSubmitted += (_, _) => status.Text = $"Поиск: «{search.SearchText}»";
        search.SearchDismissed += (_, _) => status.Text = "Поиск закрыт (Escape)";
        panel.Children.Add(Demo("App bar · search", "Встроенное поле поиска: Enter отправляет, Escape очищает и затем закрывает.", search));

        // 5. Medium app bar with a supporting text.
        var medium = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.Medium,
            Title = "Заголовок",
            Subtitle = "Поддерживающий текст",
            LeadingIcon = Menu,
            LeadingText = "Меню",
            LeadingCommand = Command("Меню"),
            AvatarText = "M",
            Actions =
            {
                new MaterialBarAction("Поиск", Search, Command("Поиск")),
                new MaterialBarAction("Поделиться", Share, Command("Поделиться"))
            }
        };
        panel.Children.Add(Demo("App bar · medium", "112: ряд иконок 56 и заголовок 28/36 с подписью 14/20.", medium));

        // 6. Large app bar whose five actions overflow.
        var large = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.Large,
            Title = "Большой заголовок",
            Subtitle = "Подпись 16/24",
            LeadingIcon = Back,
            LeadingText = "Назад",
            LeadingCommand = Command("Назад"),
            AvatarContent = PersonAvatar(),
            Actions =
            {
                new MaterialBarAction("Поиск", Search, Command("Поиск")),
                new MaterialBarAction("Поделиться", Share, Command("Поделиться")),
                new MaterialBarAction("Избранное", Favorite, Command("Избранное")),
                new MaterialBarAction("Изменить", Edit, Command("Изменить")),
                new MaterialBarAction("Настройки", Settings, Command("Настройки"))
            }
        };
        panel.Children.Add(Demo("App bar · large", "120: заголовок 36/44; три действия видимы, два — в overflow (MaxVisibleActions=3).", large));

        // 7. Elevation on scroll, driven by the attached scroll viewer.
        var scrolledBar = new MaterialAppBar
        {
            Configuration = MaterialAppBarConfiguration.Small,
            Elevation = MaterialAppBarElevation.OnScroll,
            Title = "Прокрутите список",
            LeadingIcon = Menu,
            LeadingText = "Меню",
            LeadingCommand = Command("Меню"),
            Actions = { new MaterialBarAction("Настройки", Settings, Command("Настройки")) }
        };
        var list = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        for (var i = 1; i <= 20; i++)
            list.Children.Add(new TextBlock { Text = $"Строка {i}", Margin = new Thickness(16, 8) });
        var scroll = new ScrollViewer { Content = list, Height = 220 };
        scrolledBar.ScrollSource = scroll;
        var scrollStack = new StackPanel();
        scrollStack.Children.Add(scrolledBar);
        scrollStack.Children.Add(scroll);
        panel.Children.Add(Demo("App bar · elevation on-scroll", "Фон SurfaceContainer; тень уровня 2 появляется при прокрутке (ScrollSource).", scrollStack));

        // 8. Bottom app bar with four icons and a FAB.
        var bottom = new MaterialBottomAppBar
        {
            Fab = Fab(Command("Создать")),
            Actions =
            {
                new MaterialBarAction("Поиск", Search, Command("Поиск")),
                new MaterialBarAction("Избранное", Favorite, Command("Избранное")),
                new MaterialBarAction("Поделиться", Share, Command("Поделиться")),
                new MaterialBarAction("Изменить", Edit, Command("Изменить"))
            }
        };
        panel.Children.Add(Demo("Bottom app bar · 4 иконки", "80, четыре действия и FAB 56 в трейлинге.", bottom));

        // 9. Bottom app bar whose extra actions overflow, with arbitrary overflow content.
        var bottomOverflow = new MaterialBottomAppBar
        {
            Fab = Fab(Command("Создать")),
            OverflowContent = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Профиль", FontSize = 12 },
                    new TextBlock { Text = "Дополнительное содержимое меню", FontSize = 12, TextWrapping = TextWrapping.Wrap }
                }
            },
            Actions =
            {
                new MaterialBarAction("Поиск", Search, Command("Поиск")),
                new MaterialBarAction("Избранное", Favorite, Command("Избранное")),
                new MaterialBarAction("Поделиться", Share, Command("Поделиться")),
                new MaterialBarAction("Изменить", Edit, Command("Изменить")),
                new MaterialBarAction("Настройки", Settings, Command("Настройки")),
                new MaterialBarAction("Палитра", Palette, Command("Палитра"))
            }
        };
        panel.Children.Add(Demo("Bottom app bar · overflow", "Шесть действий: четыре видимы, остальные и OverflowContent — в меню «ещё».", bottomOverflow));

        // 10. Toolbars: docked / floating, horizontal / vertical, standard / vibrant.
        panel.Children.Add(new TextBlock { Text = "Toolbars", FontSize = 24, FontWeight = FontWeight.Medium });
        panel.Children.Add(Demo("Toolbar · docked horizontal · standard", "Квадратная полоса 64, действия распределены по ширине.", Toolbar(
            MaterialToolbarConfiguration.Docked, Orientation.Horizontal, MaterialToolbarColor.Standard, Command)));
        panel.Children.Add(Demo("Toolbar · docked horizontal · vibrant", "Secondary container и соответствующие иконки.", Toolbar(
            MaterialToolbarConfiguration.Docked, Orientation.Horizontal, MaterialToolbarColor.Vibrant, Command)));
        panel.Children.Add(Demo("Toolbar · floating horizontal · standard", "Радиус full, тень уровня 3, отступ 8.", Toolbar(
            MaterialToolbarConfiguration.Floating, Orientation.Horizontal, MaterialToolbarColor.Standard, Command)));
        panel.Children.Add(Demo("Toolbar · floating horizontal · vibrant", "Vibrant: Secondary container.", Toolbar(
            MaterialToolbarConfiguration.Floating, Orientation.Horizontal, MaterialToolbarColor.Vibrant, Command)));

        var verticalRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 48 };
        verticalRow.Children.Add(Demo("Toolbar · floating vertical · standard", "Столбец 64; overflow открывается сбоку.", Toolbar(
            MaterialToolbarConfiguration.Floating, Orientation.Vertical, MaterialToolbarColor.Standard, Command)));
        verticalRow.Children.Add(Demo("Toolbar · floating vertical · vibrant", "Столбец 64 в vibrant-цвете.", Toolbar(
            MaterialToolbarConfiguration.Floating, Orientation.Vertical, MaterialToolbarColor.Vibrant, Command)));
        panel.Children.Add(verticalRow);

        // 11. Section header and page header building blocks.
        panel.Children.Add(new TextBlock { Text = "Заголовки", FontSize = 24, FontWeight = FontWeight.Medium });
        panel.Children.Add(new MaterialSectionHeader
        {
            Label = "Компоненты",
            LeadingContent = new PathIcon { Data = Palette, Width = 20, Height = 20 },
            TrailingContent = new TextBlock { Text = "12", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }
        });
        panel.Children.Add(new MaterialPageHeader
        {
            Logo = new PathIcon { Data = Palette, Width = 40, Height = 40 },
            Title = "Typography",
            LinkText = "See design guideline",
            LinkCommand = Command("See design guideline"),
            Definition = "Use typography to present your design and content as clearly and efficiently as possible."
        });

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Control Toolbar(
        MaterialToolbarConfiguration configuration,
        Orientation orientation,
        MaterialToolbarColor color,
        Func<string, ICommand> command)
    {
        var toolbar = new MaterialToolbar
        {
            Configuration = configuration,
            Orientation = orientation,
            Color = color,
            VerticalAlignment = VerticalAlignment.Top,
            Actions =
            {
                new MaterialBarAction("Поиск", Search, command("Поиск")),
                new MaterialBarAction("Избранное", Favorite, command("Избранное")),
                new MaterialBarAction("Поделиться", Share, command("Поделиться")),
                new MaterialBarAction("Изменить", Edit, command("Изменить")),
                new MaterialBarAction("Настройки", Settings, command("Настройки")),
                new MaterialBarAction("Палитра", Palette, command("Палитра"))
            }
        };
        return toolbar;
    }

    private static Control Demo(string title, string description, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.Medium });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var frame = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true
        };
        frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable("MaterialSurfaceBrush"));
        stack.Children.Add(frame);
        return stack;
    }

    private static Control Thumbnail(string caption)
    {
        var text = new TextBlock
        {
            Text = caption,
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0)
        };
        return new Border
        {
            Height = 40,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = text,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#6750A4"), 0),
                    new GradientStop(Color.Parse("#9A82DB"), 1)
                }
            }
        };
    }

    private static Control PersonAvatar()
    {
        var avatar = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(999),
            Child = new PathIcon { Data = Person, Width = 20, Height = 20 }
        };
        avatar.Bind(Border.BackgroundProperty, avatar.GetResourceObservable("MaterialSecondaryContainerBrush"));
        return avatar;
    }

    private static Control Fab(ICommand command)
    {
        var fab = new Button
        {
            Content = new PathIcon { Data = Add, Width = 24, Height = 24 },
            Command = command,
            Width = 56,
            Height = 56,
            MinWidth = 56,
            MinHeight = 56,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        MaterialAssist.SetButtonVariant(fab, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(fab, MaterialControlSize.Medium);
        AutomationProperties.SetName(fab, "Создать");
        return fab;
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public DelegateCommand(Action<object?> execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
