using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>Gallery for the Material 3 card family: containers, anatomies, layouts and states.</summary>
/// <remarks>
/// Covers the five assigned source packages: Stacked card (local-004836), Horizontal card
/// (local-001649) and the .Building Blocks/Card states Outlined (local-000209),
/// Elevated (local-000197) and Filled (local-000203).
/// </remarks>
public static class CardsGallery
{
    public static Control Create()
    {
        var clicks = new TextBlock { FontSize = 14 };
        var commands = new TextBlock { FontSize = 14 };
        var drags = new TextBlock { FontSize = 14 };
        var clickCount = 0;
        var commandCount = 0;
        var dragCount = 0;
        clicks.Text = "Нажатий по карточке: 0";
        commands.Text = "Команд из карточки: 0";
        drags.Text = "Перетаскиваний: 0";

        var panel = new StackPanel { Spacing = 32, Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "Карточки", FontSize = 32, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = "Filled, elevated и outlined контейнеры; stacked и horizontal анатомия; Layout=Slot заменяет анатомию слотом. " +
                 "Интерактивная карточка получает фокус, состояния hover/pressed/focused/dragged, Enter/Space и команду; " +
                 "вложенные кнопки сохраняют собственный ввод. Фокусное кольцо — 3 dp secondary, 2 dp снаружи контейнера.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(clicks);
        panel.Children.Add(commands);
        panel.Children.Add(drags);

        // 1. The three container treatments on a short card (no media, no actions).
        var containers = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        foreach (var variant in Enum.GetValues<MaterialCardVariant>())
        {
            containers.Children.Add(Demo(
                $"Контейнер · {variant}",
                variant switch
                {
                    MaterialCardVariant.Outlined => "Surface + 1 dp outline, без тени в покое.",
                    MaterialCardVariant.Elevated => "Surface-container-low + elevation level 1.",
                    _ => "Surface-container-highest, без обводки и тени.",
                },
                ContainerCard(variant)));
        }
        panel.Children.Add(Section("Стили контейнера", containers));

        // 2. Stacked · media & text for each style (source Layout=Media & text).
        var stacked = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        foreach (var variant in Enum.GetValues<MaterialCardVariant>())
            stacked.Children.Add(FullCard(variant));
        var singleAction = FullCard(MaterialCardVariant.Elevated);
        singleAction.ShowSecondaryAction = false;
        stacked.Children.Add(Demo(
            "Show secondary action = false",
            "Булев параметр источника скрывает вторичное действие.",
            singleAction));
        panel.Children.Add(Section("Stacked · Media & text", stacked));

        // 3. Horizontal · media & text for each style.
        var horizontal = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        foreach (var variant in Enum.GetValues<MaterialCardVariant>())
            horizontal.Children.Add(HorizontalCard(variant));
        panel.Children.Add(Section("Horizontal · Media & text", horizontal));

        // 4. Layout=Slot for each style, with a nested interactive button.
        var slots = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        foreach (var variant in Enum.GetValues<MaterialCardVariant>())
        {
            slots.Children.Add(Demo(
                $"Slot · {variant}",
                "Layout=Slot: анатомия скрыта, содержимое задаёт всю поверхность.",
                SlotCard(variant)));
        }
        panel.Children.Add(Section("Layout · Slot", slots));

        // 5. Real interaction: click, command, keyboard, drag and disabled.
        var interactive = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        var clickable = new MaterialCard
        {
            Width = 320,
            IsInteractive = true,
            Variant = MaterialCardVariant.Elevated,
            Header = "Нажмите или нажмите Enter",
            Subhead = "Клик по карточке",
            AvatarInitial = "A",
            Title = "Интерактивная карточка",
            SupportingText = "Фокус по Tab, активация Enter или Space, состояние :pressed и слой 0.10.",
        };
        clickable.Click += (_, _) =>
        {
            clickCount++;
            clicks.Text = $"Нажатий по карточке: {clickCount}";
        };
        interactive.Children.Add(Demo("Интерактивность", "Click, клавиатура и команда.", clickable));

        var commanded = new MaterialCard
        {
            Width = 320,
            IsInteractive = true,
            Variant = MaterialCardVariant.Filled,
            Header = "Команда",
            Subhead = "ICommand",
            AvatarInitial = "C",
            Title = "Command",
            Command = new DelegateCommand(_ =>
            {
                commandCount++;
                commands.Text = $"Команд из карточки: {commandCount}";
            }),
            PrimaryAction = "Основное",
            SecondaryAction = "Второе",
            PrimaryActionCommand = new DelegateCommand(_ =>
            {
                commandCount++;
                commands.Text = $"Команд из карточки: {commandCount} (primary)";
            }),
            SecondaryActionCommand = new DelegateCommand(_ =>
            {
                commandCount++;
                commands.Text = $"Команд из карточки: {commandCount} (secondary)";
            }),
        };
        interactive.Children.Add(Demo("Команды и действия", "Command карточки и кнопки действий с токенами Filled/Outlined Small.", commanded));

        var focused = new MaterialCard
        {
            Width = 320,
            Variant = MaterialCardVariant.Outlined,
            ShowFocusIndicator = true,
            Header = "Фокусное кольцо",
            Subhead = "ShowFocusIndicator",
            AvatarInitial = "F",
            Title = "Состояние Focused",
            SupportingText = "3 dp secondary, радиус + 2, вне контейнера.",
        };
        interactive.Children.Add(Demo("Focused", "Статический снимок состояния Focused из source-блока.", focused));

        var disabled = new MaterialCard
        {
            Width = 320,
            IsInteractive = true,
            IsEnabled = false,
            Variant = MaterialCardVariant.Outlined,
            Header = "Недоступно",
            Subhead = "Disabled",
            AvatarInitial = "D",
            Title = "Disabled",
            SupportingText = "Слой состояний не рисуется, контейнер приглушён до 0.38.",
        };
        interactive.Children.Add(Demo("Disabled", "Карточка не получает фокус и не активируется.", disabled));

        var draggable = new MaterialCard
        {
            Width = 320,
            IsInteractive = true,
            Variant = MaterialCardVariant.Filled,
            Header = "Перетащите карточку",
            Subhead = "DragData",
            AvatarInitial = "G",
            Title = "Состояние Dragged",
            SupportingText = "Порог 4 dp; во время переноса слой 0.16 и поднятая тень.",
            DragData = TextTransfer("Material card"),
        };
        draggable.DragStarted += (_, _) => drags.Text = "Перетаскивание началось";
        draggable.DragCompleted += (_, e) =>
        {
            dragCount++;
            drags.Text = $"Перетаскиваний: {dragCount} · результат {e.Effects}";
        };
        interactive.Children.Add(Demo("Dragged", "Реальный drag-and-drop платформы, состояние :dragged.", draggable));
        panel.Children.Add(Section("Состояния", interactive));

        // 6. Header action slot: a 48 dp icon button inside the stacked header.
        var withAction = new MaterialCard
        {
            Width = 360,
            Variant = MaterialCardVariant.Elevated,
            Header = "Заголовок",
            Subhead = "Подзаголовок",
            AvatarInitial = "M",
            Title = "Заголовок карточки",
            SupportingText = "Действие в шапке — обычная иконочная кнопка 48 dp.",
            HeaderAction = new Button
            {
                Content = new MaterialIcon { IconName = "more_vert", IsDecorative = true },
                Width = 48,
                Height = 48,
            },
            PrimaryAction = "Открыть",
            SecondaryAction = "Позже",
        };
        MaterialAssist.SetIsIconButton((Button)withAction.HeaderAction!, true);
        MaterialAssist.SetButtonVariant((Button)withAction.HeaderAction!, MaterialButtonVariant.Standard);
        AutomationProperties.SetName((Button)withAction.HeaderAction!, "Дополнительно");
        panel.Children.Add(Section("Действие в шапке", Demo("HeaderAction", "Слот шапки принимает любой контрол; здесь — standard icon button.", withAction)));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static MaterialCard ContainerCard(MaterialCardVariant variant) => new()
    {
        Width = 320,
        Variant = variant,
        Header = "Заголовок",
        Subhead = "Подзаголовок",
        AvatarInitial = "A",
        Title = "Title",
        Subtitle = "Subtitle",
        SupportingText = "Короткая карточка: шапка, заголовок и поддерживающий текст.",
    };

    private static MaterialCard FullCard(MaterialCardVariant variant) => new()
    {
        Width = 360,
        Variant = variant,
        Header = "Заголовок",
        Subhead = "Подзаголовок",
        AvatarInitial = "A",
        Media = Media("Медиа 188"),
        Title = "Title",
        Subtitle = "Subtitle",
        SupportingText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor",
        SecondaryAction = "Второе",
        PrimaryAction = "Основное",
    };

    private static MaterialCard HorizontalCard(MaterialCardVariant variant) => new()
    {
        Width = 360,
        Variant = variant,
        Orientation = MaterialCardOrientation.Horizontal,
        Header = "Header",
        Subhead = "Subhead",
        AvatarInitial = "A",
        Media = Media(null),
    };

    private static MaterialCard SlotCard(MaterialCardVariant variant)
    {
        var card = new MaterialCard
        {
            Width = 280,
            Variant = variant,
            Layout = MaterialCardLayout.Slot,
            Padding = new Thickness(16),
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = "Слот содержимого", FontSize = 16, FontWeight = FontWeight.Medium });
        content.Children.Add(new TextBlock
        {
            Text = "Анатомия скрыта: содержимое задаёт всю поверхность карточки.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new Button { Content = "Действие в слоте", HorizontalAlignment = HorizontalAlignment.Left });
        card.Content = content;
        return card;
    }

    private static Control Media(string? caption)
    {
        var panel = new Panel();
        panel.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#6750A4"), 0),
                    new GradientStop(Color.Parse("#9A82DB"), 1),
                },
            },
        });
        panel.Children.Add(new MaterialIcon
        {
            IconName = "sticker",
            Width = 48,
            Height = 48,
            IsDecorative = true,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (caption is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = caption,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(12),
                VerticalAlignment = VerticalAlignment.Bottom,
            });
        }
        return new Border { Child = panel };
    }

    private static Control Section(string title, Control content)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.Medium });
        stack.Children.Add(content);
        return stack;
    }

    private static Control Demo(string title, string description, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.Medium });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
        stack.Children.Add(content);
        return stack;
    }

    private static IDataTransfer TextTransfer(string text)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(text));
        return transfer;
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
