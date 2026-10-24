# ExpressiveAvalonia: архитектура и матрица покрытия

Статус: реализованы основа темы и стандартные семейства контролов, 2026-10-24. Актуальные API, проверенные сценарии и ограничения перечислены в README.md. Полный каталог MD3 ещё не закрыт.

## Цель и границы

Библиотека Material Design 3 + Material 3 Expressive для Avalonia: тема стандартных контролов и отдельные контролы там, где стандартный контракт не выражает поведение Material. Конечная цель — весь каталог ниже, а не только изменение цветов Fluent.

Реализованы токены, MaterialTheme (SeedColor/Contrast/FontFamily/Motion), MaterialAssist, MaterialTypography, стандартные шаблоны и специализированные MaterialRangeSlider/CircularProgressIndicator/RichTooltip. Дополнительные компоненты ниже остаются целевым контрактом до их реализации.

Нативные Android/iOS оболочки, XR и маршрутизатор приложения не входят в библиотеку компонентов. Это не исключает поддержку ввода touch, адаптивных размеров, RTL и экранных дикторов на поддерживаемых Avalonia платформах. Поддержка каждой платформы заявляется только после проверки на ней.

## Структура решения

```text
src/ExpressiveAvalonia/
  Theming/                 MaterialTheme, параметры темы, генерация ресурсов
  Tokens/                  роли цвета, типографики, формы, elevation и motion
  Themes/                  AXAML, ControlTheme, компонентные ресурсы
  Controls/                только недостающие Material-компоненты
  Primitives/              внутренние панели, presenters, state layers
  Automation/              peers нестандартных контролов
samples/ExpressiveAvalonia.Catalog/
  Pages/                   интерактивная страница на семейство компонентов
  ViewModels/              примеры привязок и команд
tests/ExpressiveAvalonia.Tests/
```

Выпускается один NuGet-пакет `ExpressiveAvalonia`. Каталог и тесты в него не входят. Пространства имён: `ExpressiveAvalonia.Theming`, `ExpressiveAvalonia.Controls`. Внутренние primitives не становятся публичным API без самостоятельного пользовательского сценария.

База реализации: Avalonia 12.1.4, библиотека net8.0, каталог и тесты net10.0. Restore/build и desktop smoke выполнены на Windows. Avalonia 11 — отдельное решение по поддержке, не неявная совместимость.

Не зависеть от FluentTheme или SimpleTheme: иначе отсутствующие шаблоны скрывают пробелы Material и создают две системы токенов. Использовать стандартные контролы и инфраструктуру Avalonia, собственные ControlTheme и необходимые служебные шаблоны. Отдельный MVVM-фреймворк библиотеке не нужен.

## Контракт темы

Реализованный пользовательский синтаксис:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="clr-namespace:ExpressiveAvalonia.Theming;assembly=ExpressiveAvalonia"
             x:Class="Example.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <md:MaterialTheme SeedColor="#6750A4" Motion="Expressive" />
  </Application.Styles>
</Application>
```

- `MaterialTheme` — единственная точка подключения стилей и ресурсов. `SeedColor`, `Motion` и `Contrast` доступны для изменения во время работы.
- Светлая/тёмная схема следует `ActualThemeVariant`; локальный ThemeVariantScope не ломается. Переключение не пересоздаёт контролы и не теряет фокус, выделение или введённый текст.
- `SeedColor` преобразуется алгоритмом Material Color Utilities/HCT, а не сдвигом RGB/HSL. Реализацию/зависимость выбрать после проверки корректности алгоритма, лицензии и совместимости TFM. ОС-палитра — отдельный источник seed, не обещанная функция Avalonia.
- Публичные ключи ресурсов имеют префикс `Material`: `MaterialPrimaryBrush`, `MaterialOnPrimaryBrush`, `MaterialSurfaceBrush`, `MaterialOutlineBrush`, `MaterialBodyLargeFontSize` и т. д. Документируется полный перечень ролей, а не только несколько accent-цветов.
- Все роли цветов MD3, включая surface containers, inverse, error и fixed roles; стандартная и повышенная контрастность; отдельные state-layer opacity.
- Типографика: display/headline/title/body/label, размеры small/medium/large, expressive emphasis; шрифт заменяемый. Шрифт не скачивается во время исполнения, лицензия вложенного шрифта включается в пакет.
- Shape, elevation и motion — отдельные токены. Expressive motion включает прерываемые переходы и физические параметры; не подменяется одинаковым easing для всех компонентов. `Motion="Reduced"` отключает пространственные и циклические декоративные движения; при наличии платформенного сигнала reduced motion имеет приоритет.
- Варианты задаются типизированными attached properties: например `MaterialAssist.ButtonVariant="Tonal"`, `MaterialAssist.ControlSize="Large"`. Не создавать параллельные MdButton/MdTextBox для уже существующих контрактов.
- Не привязывать стили к тексту, конкретному DataContext или именам контролов пользователя. Команды, bindings, validation и item templates остаются стандартными Avalonia.

## Общие правила API компонентов

- Свойства, которым нужна привязка и стилизация, — Avalonia properties; команды — ICommand; пользовательские события — routed events там, где требуется маршрутизация.
- Коллекции — ItemsSource и ItemTemplate; выбор — существующий контракт SelectingItemsControl/SelectionModel, если он подходит. Не вводить вторую систему выбора.
- Icon — пользовательский графический content/geometry, не название обязательного icon-font. Icon-only действия требуют accessible name.
- Навигация сообщает выбранный пункт; не создаёт страницы и не управляет lifetime view models.
- Асинхронные transient UI используют явный host. Нет глобального окна, статического service locator или скрытого dispatcher.
- Слои popup/modal: явный владелец, порядок, scrim, политика light-dismiss, Escape, возврат фокуса. Modal ограничивает Tab своим содержимым; немодальный слой не блокирует остальной UI.
- Локализуемые строки, культура чисел/дат, первый день недели и RTL не фиксируются внутри шаблонов. Политика смены языка определяется приложением.
- Touch target не равен размеру нарисованной иконки. Hover не является единственным способом открыть действие.

## Матрица покрытия

Статус **реализовано, не сертификат визуального соответствия**: Buttons, Icon buttons, Checkbox, Radio buttons, Switch, Text fields, Sliders, Progress, Lists, Tabs, Menus, Tooltips, Dividers. Наличие шаблона, успешная сборка и поведенческие проверки сами по себе не подтверждают соответствие MD3. Приёмка платформ ограничена Windows; физический touch и скринридер не проверены. Progress реализует стандартный rounded/gapped вариант, не wavy. Lists используют нативный ListBoxItem с опциональной anatomy MaterialList (headline/supporting/leading/trailing, 1–3 строки). Остальные строки: **не реализовано**; названия соответствующих классов — проектируемый API. Calendar — инфраструктура, не завершённый date picker.

Проверка основы: HCT-векторы Google закреплены в ColorReferenceTests (официальный commit bd811e4ca2ab94c4ce3cadc89c19b1820b9704b3); typography tracking/emphasis — AndroidX TypeScaleTokens v0_103; тени — Material Web key/ambient geometry. Motion поддерживает retarget с сохранением скорости, остановку/завершение при detach/reduced motion. Автоматический reduced motion доступен Windows; на других платформах задаётся явно.

Доступные служебные шаблоны: Window с VisualLayerManager, ScrollViewer/ScrollBar, Thumb/RepeatButton, ContentControl/presenters, Separator, ComboBox и Calendar primitives, PopupRoot/OverlayPopupHost. FluentTheme и SimpleTheme не подключаются.

| Семейство | Варианты в объёме | База Avalonia / предлагаемый API | Условия готовности сверх визуала |
| --- | --- | --- | --- |
| Buttons | Filled, tonal, outlined, elevated, text; Expressive size/shape | Button + MaterialAssist | Command, default/cancel, keyboard, disabled, state layers, shape transition |
| Icon buttons | Standard, filled, tonal, outlined; toggle | Button / ToggleButton + attached variant | Accessible name, checked state, target size |
| FAB | Обычный, расширенный, размеры и цвета Expressive | FloatingActionButton : Button | Icon/label, expand/collapse, focus, command |
| FAB menu | Expandable FAB и связанные действия | FloatingActionMenu | Открытие, закрытие, Escape, управление фокусом, команды пунктов |
| Button groups | Standard, connected; single/multiple selection | ButtonGroup и ButtonGroupItem | Selection model, стрелки, disabled items, connected shape morph |
| Split buttons | Leading action + trailing menu | SplitButton / ToggleSplitButton, свои шаблоны | Независимые действия, checked, keyboard menu, focus return |
| Badges | Dot, label/count | Badge | Accessible status, overflow count, placement, RTL |
| Cards | Elevated, filled, outlined; interactive | Card + стандартные Button/ToggleButton для действий | Без вложенных конфликтующих кнопок; selection/activation только явно |
| Carousel | Multi-browse, uncontained, hero, full-screen | MaterialCarousel + CarouselItem | Layout/masking, snapping, resize, touch, keyboard, item virtualization |
| Checkbox | Checked, unchecked, indeterminate | CheckBox | Three-state bindings, validation, keyboard, automation |
| Chips | Assist, filter, input, suggestion | Chip, SelectableChip; ItemsControl для групп | Selection, command, отдельное удаление input chip, keyboard |
| Date pickers | Docked, modal, range; calendar/text input | MaterialDatePicker, MaterialDateRangePicker; Calendar где подходит | Parsing, culture, range constraints, cancel/commit, focus |
| Dialogs | Basic, full-screen | MaterialDialog + MaterialDialogHost | Результат, modal semantics, cancel, focus trap/return, owner close |
| Dividers | Horizontal, vertical; inset | Separator | Orientation, margins, contrast |
| Lists | One/two/three-line, expressive grouping, leading/trailing content | ListBox/ListBoxItem, ItemsControl; MaterialListItem content | Selection, virtualization, keyboard, wrapping, independent trailing action |
| Loading indicator | Expressive indeterminate morphing shapes | LoadingIndicator | Lifecycle start/stop, reduced motion, accessible busy state |
| Menus | Popup, nested, selectable items, expressive grouping | Menu, MenuItem, ContextMenu, MenuFlyout | Arrows, accelerators, submenu placement, checks, dismiss, focus |
| Navigation bar | Flexible bar, item label arrangements | NavigationBar + NavigationItem | Single selection, keyboard, badges, RTL; без встроенного router |
| Navigation rail | Collapsed, expanded, modal expanded | NavigationRail + NavigationItem | Expansion, selection, modal focus, адаптация ширины |
| Progress | Linear, circular; determinate/indeterminate; expressive track/stop | ProgressBar linear; CircularProgressIndicator | Clamp/range semantics, accessible value, animation lifecycle |
| Radio buttons | Selected/unselected, groups | RadioButton | GroupName, keyboard group navigation, disabled, automation |
| Search | Search bar и открываемый search view | SearchBar + SearchView, TextBox внутри | Query, submit, suggestions, dismiss, focus, async data у приложения |
| Bottom sheets | Standard/persistent, modal | BottomSheet + SheetHost | Detents, drag, bounds, scrim, nested scrolling, keyboard/focus |
| Side sheets | Standard, modal | SideSheet + SheetHost | Placement/RTL, resize, dismiss, focus; немодальный не блокирует фон |
| Sliders | Continuous, discrete; single/range; expressive handle | Slider + тема; MaterialRangeSlider | Keyboard steps, snapping, min/max, crossing policy, labels, automation |
| Snackbar | Text, optional action, close | SnackbarHost + сообщения | Очередь, timeout/pause, action, announcement; без кражи фокуса |
| Switch | On/off; optional icons | ToggleSwitch | Binding, keyboard, drag, automation |
| Tabs | Primary, secondary; fixed/scrollable | TabControl/TabItem | Selection, arrow/Home/End, indicator, scroll selected into view |
| Text fields | Filled, outlined; prefix/suffix, leading/trailing, supporting text | TextBox + MaterialAssist | Floating label, IME, selection, validation, password reveal policy |
| Time pickers | Dial и text input; 12/24h; modal | MaterialTimePicker | Culture, parse, AM/PM, keyboard dial, cancel/commit |
| Top app bars | Small, medium, large; centered/expressive arrangements | TopAppBar | Scroll coupling, collapse, insets, title semantics, actions |
| Toolbars | Docked, floating; horizontal/vertical | MaterialToolbar | Overflow, keyboard navigation, placement, action groups |
| Tooltips | Plain, rich | ToolTip theme; RichTooltip | Hover/focus/touch, delay, Escape, placement; rich actions keyboard-accessible |

### Классические MD3-компоненты, заменённые в Expressive

Они остаются частью цели «MD3 + Expressive», но не выдаются за рекомендуемые Expressive-компоненты. Это самостоятельные варианты, не compatibility aliases.

| Компонент | Реализация | Рекомендация для нового Expressive UI |
| --- | --- | --- |
| Segmented buttons | SegmentedButtonGroup; single/multiple selection, keyboard и selected semantics | Connected button group |
| Navigation drawer | NavigationDrawer; standard/modal, destinations, scrim и focus | Expanded navigation rail |
| Bottom app bar | BottomAppBar; actions, FAB integration, scroll behavior | Docked toolbar |

### Служебное покрытие Avalonia

Тема обязана предоставить согласованные служебные шаблоны для Window, ScrollViewer/ScrollBar, ContentControl, presenters, Popup, Calendar, ComboBox и внутренних элементов используемых контролов. Это не новые компоненты MD3. DataGrid, TreeView, docking и редакторы кода не входят в официальный каталог Material; их нельзя молча считать покрытыми этой матрицей.

## Критерии готовности

Статусы в будущем: не реализовано → реализуется → проверено. Частичная строка перечисляет оставшиеся варианты и не считается готовой.

Для каждого семейства:

1. Каталог демонстрирует все заявленные варианты, light/dark, enabled/disabled, hover/pressed/focus/selected/error, где они применимы.
2. Проверены мышь, клавиатура, touch при применимости, RTL, масштабирование и длинный локализованный текст.
3. Accessible role/name/value/state и focus behavior соответствуют действию, а не внешнему виду. Скринридер проверяется отдельно от automation tree.
4. Изменение темы и данных не сбрасывает пользовательское состояние; отписки и закрытие popup не оставляют работающие таймеры/анимации.
5. Поведенческие тесты проверяют пользовательский контракт: диапазоны, выбор, cancel/commit, переходы фокуса, приоритет validation. Не snapshot исходного AXAML и не наличие свойства.
6. Есть runtime smoke на реальном каталоге; headless тест не заменяет визуальную и input-проверку. Платформы проверки перечислены явно.
7. Есть документация API, пример подключения, ограничения и привязка к конкретной странице спецификации. Пакет проверен в отдельном приложении-потребителе без FluentTheme.

## Этапы реализации и зависимости

1. **Контракты и токены.** Восстановить пакеты, проверить TFM; схема ресурсов, HCT/color reference vectors, typography/shape/motion, тема и минимальное приложение-потребитель.
2. **Стандартные контролы.** Buttons, selection controls, text fields, slider, linear progress, tabs, menus и служебные шаблоны. На этом этапе фиксируется attached API.
3. **Composition и коллекции.** Cards, badges, chips, lists, button groups, split buttons, FAB; selection/virtualization и automation.
4. **Слои и transient UI.** Общий внутренний механизм overlay; dialogs, sheets, snackbar, tooltips, search, FAB menu. Общая политика focus/dismiss сначала, компоненты после неё.
5. **Навигация и адаптивные layouts.** Navigation bar/rail/drawer, app bars, toolbars, responsive behavior без встроенного router.
6. **Специализированные компоненты.** Date/range/time pickers, range slider, carousel, circular progress и expressive loading indicator. Motion primitives из первого этапа используются повторно.
7. **Приёмка полного каталога.** Закрыть все строки, проверить чистого NuGet-потребителя, доступность, производительность и объявленные платформы.

Приложение-каталог развивается с первого runtime-компонента, не откладывается до последнего этапа. Библиотека не объявляется полной до закрытия основной и классической матриц.

## Решения до фиксации публичного API

- Подтвердить зависимости и минимальные версии: Avalonia/TFM, Material Color Utilities, шрифт. Не писать собственную упрощённую колориметрию ради отсутствующей зависимости.
- Извлечь точные размеры, формы и motion-параметры из component specs; данные overview не являются полной спецификацией.
- Проверить возможности Avalonia spring animation, gesture routing и automation; недостающие реализации оставить внутренними.
- Различать локальную проверку Windows и заявленную кроссплатформенность. Linux/macOS и mobile требуют собственных runtime-проверок.

## Источники

- Каталог: https://m3.material.io/components
- Expressive: https://m3.material.io/blog/building-with-m3-expressive
- Button groups: https://m3.material.io/components/button-groups/overview
- FAB menu: https://m3.material.io/components/fab-menu/overview
- Toolbars: https://m3.material.io/components/toolbars/specs
- Navigation drawer и рекомендация замены: https://m3.material.io/components/navigation-drawer/overview
- Segmented buttons и рекомендация замены: https://m3.material.io/components/segmented-buttons/guidelines
- Motion: https://m3.material.io/styles/motion/overview/how-it-works
- Версии Avalonia: https://api.nuget.org/v3-flatcontainer/avalonia/index.json

Официальный сайт частично отдаёт контент через клиентское приложение. Каталог и Expressive-замены подтверждены поисковыми выдержками официального сайта; точные численные спецификации и полный вариантный состав необходимо сверять на этапе реализации. Эта матрица задаёт план покрытия, не является сертификатом соответствия.
