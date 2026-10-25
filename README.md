# ExpressiveAvalonia

Material Design 3 + Material 3 Expressive для Avalonia. Библиотека развивается; весь каталог MD3 пока не покрыт.

## Реализовано

Avalonia 12.1.4, библиотека `net8.0`, desktop-каталог и тесты `net10.0`. Нет зависимости от FluentTheme/SimpleTheme.

- `MaterialTheme`: динамические `SeedColor`, `Contrast` (-1…1), `FontFamily`, `Motion` (Expressive/Reduced); light/dark и локальный ThemeVariantScope.
- HCT Tonal Spot / Spec2021: 49 цветовых ролей и brushes; эталонные проверки Google для HCT, gamut mapping, light/dark, fixed roles и surface tint при разных контрастах.
- 15 типографических ролей с line height/tracking и emphasized-вариантами; применяются через `MaterialTypography.Style` и `Emphasized`. По умолчанию встроенный Roboto (SIL OFL 1.1), шрифт заменяемый.
- Shape, state opacity, elevation distances и двухслойные тени уровней 0…5, durations, `md.sys.motion.easing`-токены и шесть AndroidX Expressive springs.
- Прерываемые springs с сохранением скорости при retarget и shape morph кнопок; без произвольного press scale. State layers, hover-elevation, индикаторы табов, поля и списки меняют состояние через `Transitions` с easing/duration-токенами. Reduced motion немедленно завершает spatial animation; индикаторы прекращают циклическое движение. На Windows учитывается настройка анимаций ОС; на других ОС доступен явный reduced mode.
- Button/ToggleButton: standard, filled, tonal, outlined, elevated, text; icon mode; пять Expressive-размеров; round/square shape; focus/disabled/state layers. `Standard` — значение по умолчанию: у labeled-кнопки filled-контейнер, у icon-only контейнера нет.
- TextBox: filled/outlined, floating label, prefix/suffix, leading/trailing content, supporting/validation text, opt-in password reveal с сокрытием при уходе фокуса. Нативные editing/selection/IME/clipboard APIs сохраняются.
- CheckBox (включая `:error` и indeterminate), RadioButton, ToggleSwitch; switch использует OnContent/OffContent для необязательных иконок.
- Slider: horizontal/vertical, ticks, snapping, value labels, expressive handle. MaterialRangeSlider: два endpoints, клавиатура/drag/RTL, clamp при пересечении, snapping и отдельные automation providers.
- ProgressBar и CircularProgressIndicator: determinate/indeterminate; остановка таймеров при detach, скрытии и reduced motion.
- ListBox/ListBoxItem с виртуализацией и опциональной anatomy через `MaterialList`; primary/secondary tabs, fixed/scrollable headers и стрелки/Home/End.
- Menu/MenuItem, ContextMenu, MenuFlyout; plain tooltip на hover/focus с Escape; RichTooltip с действиями и возвратом фокуса.
- Служебные шаблоны Window/visual layers, ScrollViewer/ScrollBar, Thumb, RepeatButton, ContentControl, presenters, Separator, ComboBox, Calendar и popup hosts.

## Перенос локальной дизайн-системы

Источник: `artifacts/design-system-anonymized/index.json`, 416 пакетов и 5599 вариантов. `specification.json` описывает варианты и свойства, соответствующий `design.json` — геометрию, анатомию и значения дизайна. Все 5599 файлов `design.json` доступны; в исходном наборе отсутствуют 444 SVG и один preview, поэтому изображения не являются единственным источником реализации.

Ветка переноса добавляет специализированные семейства и страницы `Design system` в desktop-каталог. Они подключаются через ту же `MaterialTheme`, без дополнительной Fluent/Simple темы. Кнопки используют анатомию локального экспорта: `MaterialAssist.IconButtonWidth="Narrow|Default|Wide"`, `Icon`, `SelectedIcon`, `ShowIcon`; выбранные labeled-toggle также меняют round/square силуэт. `MaterialRipple` рисует исходную clipped-геометрию pressed-состояния. Встроенные glyphs доступны отдельно через семейство ресурсов иконок, пользовательский content остаётся допустимым.

Selection controls сохраняют нативные `CheckBox`, `RadioButton`, `ToggleSwitch`; встроенные иконки switch включаются через `MaterialSelection.ShowSwitchIcons="True"`. Слайдеры используют пять `MaterialAssist.ControlSize`; `MaterialSlider.IsCentered`, `ShowStops`, `ShowIcon`, `Icon` и `ValueFormat` задают исходную анатомию. `MaterialProgress.IndicatorType="Wave"` и `Thickness` на `ProgressBar`, `CircularProgressIndicator.IndicatorType` и `LoadingIndicator` представляют expressive progress.

Новые самостоятельные компоненты: `MaterialFab`, `MaterialExtendedFab`, `MaterialFabMenu`; `MaterialSheet`/`MaterialBottomSheet`/`MaterialSideSheet`; `MaterialSearchBar`/`MaterialSearchView`; `MaterialNavigationRail`/`MaterialNavigationRailExpanded`; `MaterialShape`, `MaterialDivider`, `MaterialScrim`, `MaterialFocusIndicator`, `MaterialStateLayer`. Их composition, реальные действия и варианты показаны на одноимённых страницах каталога. Модальные inline-компоненты следует размещать в panel, покрывающей блокируемую область; запросы/навигация приложения не реализуются библиотекой автоматически.

## Запуск и проверки

Нужен .NET SDK 10.

```sh
dotnet build ExpressiveAvalonia.slnx
dotnet test tests/ExpressiveAvalonia.Tests/ExpressiveAvalonia.Tests.csproj
dotnet run --project samples/ExpressiveAvalonia.Catalog
```

Каталог оформлен как Expressive-студия без верхней шапки: слева сворачиваемый Navigation rail, в центре единая скруглённая поверхность с заголовком и прокручиваемыми примерами, справа панель параметров шириной 380 dp без отдельных карточек вокруг групп. Сверху rail расположены кнопка сворачивания и кнопка-переключатель: активная `MD3E` открывает Expressive-семейства, неактивная `MD3` — базовые контролы Classic. Обе кнопки доступны и в компактном rail. Это разделение примеров, а не две независимые темы: общие Material-токены сохраняются.

Справа доступны светлая/тёмная тема, reduced motion, ширина и минимальная высота области, доступность контролов и RTL. Для кнопок можно менять размер; для карточек — ширину и высоту самих блоков. На странице FAB меню вынесены в начало: число действий меняется от 2 до 6, иконки и раскрытие управляются переключателями. Настройки примера сбрасываются при уходе со страницы; оформление студии сохраняется в текущем окне. Панель не блокируется переключателем доступности примеров.

Страницы Classic/Expressive создаются при открытии, а их деревья контролов освобождаются при уходе; посещённые страницы не кэшируются. Вложенные вкладки Assets, Shapes, Sliders и Examples, включая конфигурации клавиатуры, также создают содержимое только при открытии. На странице Buttons ряды вариантов, в Assets плитки иконок и в FAB дополнительные сетки используют загрузку по effective viewport: вне области просмотра остаётся лёгкий контейнер, а дерево примера освобождается (пока внутри нет клавиатурного фокуса). При возвращении локальное состояние такого блока пересоздаётся. Общие ресурсы темы сохраняются. Полный экспорт `--preview` намеренно раскрывает содержимое для снимков и не использует выгрузку по viewport. Сборка не измеряет фактическую экономию ОЗУ; численные результаты профилирования не заявлены.

```sh
dotnet run --project samples/ExpressiveAvalonia.Catalog -- --smoke
```

Полные превью страниц каталога (light/dark, вложенные вкладки, содержимое ниже первого экрана):

```sh
dotnet run --project samples/ExpressiveAvalonia.Catalog -- --preview
```

Откройте `artifacts/previews/index.html` напрямую в браузере: сервер не нужен. PNG-снимки реальных контролов находятся рядом; длинные страницы разбиты на плитки до 1600 пикселей. Поиск фильтрует страницы по семейству и теме. Это обзор элементов каталога, не перебор всех 5599 комбинаций состояний; popup и другие интерактивные состояния доступны в живом каталоге.

Desktop smoke проверяет смену темы/seed/контраста, затем отрисовывает шесть страниц `MD3 Classic` и 29 страниц `MD3 Expressive` в light/dark с seed `#6750A4` и `Contrast=0`; на FAB проверяет изменение реального количества действий до шести и ширины области до 960 dp; открывает/закрывает rich tooltip через Escape. Снимки базовых страниц — `artifacts/catalog-{light|dark}-page-{0..5}.png`, Expressive — `artifacts/catalog-design-{light|dark}-{0..28}.png`. Навигация и панель параметров прокручиваются независимо.

Страница Ranges остаётся открытой по 15 секунд в каждой теме для проверки indeterminate-анимации через desktop compositor. Дуги CircularProgressIndicator после передачи renderer’у не изменяются: повторное открытие опубликованной StreamGeometry приводило к конкурентному изменению native SKPath и падению Skia `GetFillPath` (`0xC0000005`). Короткая отрисовка и headless-тесты не заменяют эту проверку.

Проверено на Windows: 59 автоматических проверок и desktop smoke (70 отрисовок: шесть стандартных страниц и 29 семейств × две темы), rich tooltip с routed Escape. Все 416 пакетов назначены и заявлены исполнителями; самоотчёт и успешная отрисовка страницы не подтверждают попиксельное соответствие всех 5599 вариантов. Все сочетания hover/press/focus, физические устройства ввода, скринридеры, mobile, Linux/macOS пока не проверены. Это не сертификат доступности или полного соответствия MD3.

## Подключение

Добавьте ProjectReference или локально собранный NuGet-пакет. Публикация в NuGet не выполнена.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="clr-namespace:ExpressiveAvalonia.Theming;assembly=ExpressiveAvalonia"
             x:Class="Example.App" RequestedThemeVariant="Default">
  <Application.Styles>
    <md:MaterialTheme SeedColor="#6750A4" Contrast="0" Motion="Expressive" />
  </Application.Styles>
</Application>
```

Не подключайте FluentTheme для скрытой подмены отсутствующих Material-компонентов.

### Кнопки и текстовые поля

В файле с контролами объявите `xmlns:md="clr-namespace:ExpressiveAvalonia.Theming;assembly=ExpressiveAvalonia"`:

```xml
<Button Content="Продолжить"
        md:MaterialAssist.ButtonVariant="Tonal"
        md:MaterialAssist.ControlSize="Medium"
        md:MaterialAssist.ButtonShape="Round" />
<TextBox md:MaterialTextField.Variant="Outlined"
         md:MaterialTextField.Label="Имя"
         md:MaterialTextField.SupportingText="Как к вам обращаться?" />
<TextBlock Text="Заголовок" md:MaterialTypography.Style="HeadlineLarge"
           md:MaterialTypography.Emphasized="True" />
```

Icon-only button: `MaterialAssist.IsIconButton="True"`, Content — geometry/icon пользователя, `AutomationProperties.Name` обязателен. ToggleButton использует тот же API. Без `ButtonVariant` действует `Standard`: у icon-only кнопки нет контейнера (иконка OnSurfaceVariant), у labeled-кнопки — filled-контейнер. У icon-only кнопок `Filled`, `Tonal` и `Outlined` дают соответствующие *IconButtonTokens; `ButtonShape` меняет силуэт только у icon-only toggle при `IsChecked`, как в AndroidX.

MaterialTextField дополнительно: `Prefix`, `Suffix`, `LeadingContent`, `TrailingContent`, `ShowPasswordReveal`. Нативный DataValidationErrors остаётся источником ошибок; пользовательские AutomationProperties могут переопределить доступное имя/описание.

### Диапазон и progress

```xml
<!-- xmlns:controls="clr-namespace:ExpressiveAvalonia.Controls;assembly=ExpressiveAvalonia" -->
<controls:MaterialRangeSlider Minimum="0" Maximum="100"
                             LowerValue="20" UpperValue="80"
                             TickFrequency="10" IsSnapToTickEnabled="True" />
<controls:CircularProgressIndicator Value="65" Width="48" Height="48" />
```

Range slider горизонтальный. LowerValue не может превышать UpperValue; endpoints не меняются местами. SmallChange/LargeChange задают шаг клавиатуры. Имена endpoints можно локализовать через шаблон.

### Lists

На нативном `ListBoxItem` доступны `MaterialList.Headline`, `SupportingText`, `LeadingContent`, `TrailingContent`, `LineCount` (1/2/3). Минимальная высота — 56/72/88 dp соответственно. Без `Headline` сохраняются обычные `Content`/`ContentTemplate`. Leading/trailing content принимает пользовательские иконки, аватары или контролы.

### Tabs, rich tooltip и motion

- `MaterialTabs.Variant="Primary|Secondary"`, `MaterialTabs.IsScrollable="True"` на TabControl.
- `RichTooltip : Flyout`: `Title`, `Content`, `Actions`, `ActionsTemplate`. Назначьте Button.Flyout или вызовите ShowAt(owner); Hide закрывает. Открывается явно, а не hover-таймером, чтобы не красть фокус. Escape/light dismiss используют native flyout lifecycle.
- `MaterialMotion.IsReduced="True"` наследуется поддеревом. Системный Windows reduced mode имеет приоритет.
- `MaterialMotion.Animate(owner, from, to, apply, stiffness, dampingRatio)` возвращает IDisposable SpringAnimation; `Retarget(target)` сохраняет текущую скорость. Dispose останавливает и снимает подписки. Отсоединение завершает к целевому значению. Параметры по умолчанию: stiffness 1400, damping ratio 0.9.

## Ресурсы

- Цвет: `Material{Role}Color` / `Material{Role}Brush`; полный перечень в `Tokens/MaterialColorScheme.cs`.
- Типографика: `Material{Display|Headline|Title|Body|Label}{Large|Medium|Small}{FontSize|LineHeight|FontWeight|LetterSpacing}`. Emphasis вставляет `Emphasized` перед последним суффиксом.
- `MaterialFontFamily`; Roboto не вложен и не загружается по сети.
- `MaterialShape{None|ExtraSmall|Small|Medium|Large|ExtraLarge|Full}CornerRadius`.
- `Material{Hover|Focus|Pressed|Dragged}StateLayerOpacity`.
- `MaterialElevationLevel0` … `5` и `MaterialElevationLevel0Shadow` … `5Shadow`.
- `MaterialDuration{Short|Medium|Long|ExtraLong}{1|2|3|4}` типа TimeSpan.
- `MaterialEasing{Standard|StandardAccelerate|StandardDecelerate|Emphasized|EmphasizedAccelerate|EmphasizedDecelerate|Legacy|LegacyAccelerate|LegacyDecelerate|Linear}` — `IEasing` для `Transitions` и кода.
- `MaterialSpring{Spatial|Effects}{Default|Fast|Slow}{Stiffness|Damping}` — параметры AndroidX ExpressiveMotionTokens.
- Строки шаблонов: `MaterialTextInput{Cut|Copy|Paste|SelectAll}Text`, `MaterialPassword{Show|Hide}Text`, `MaterialRange{Lower|Upper}ValueText`, `MaterialScroll{Left|Right|Up|Down}Text`, `MaterialPage{Left|Right|Up|Down}Text`, `MaterialScrollPositionText`. Переопределите эти ключи в ресурсах приложения, чтобы локализовать встроенные automation names, tooltips и контекстное меню.

Используйте DynamicResource. В прямом FindResource укажите ActualThemeVariant. Визуальная вертикальная линия: Separator Classes="vertical".

## Ограничения

Генератор использует `Shirasagi0012.MaterialColorUtilities` 0.2.0 (Apache-2.0); автор помечает порт WIP. Независимые reference tests проверяют конкретные эталоны, а не весь алгоритм. Scheme — классический Tonal Spot Spec2021/phone, не Spec2025. Seed alpha игнорируется.

Дополнительные семейства (FAB, chips, cards, dialogs/sheets/snackbar, navigation, carousel, date/time pickers и другие) остаются в [матрице покрытия](docs/architecture.md). Визуальный Calendar инфраструктуры не заменяет полноценный Material date picker. Expressive wavy progress tracks не заявлены; реализованы rounded/gapped tracks и stop marker.

Сверка стандартных контролов выполнена по [генерируемым AndroidX Material3-токенам](https://github.com/androidx/androidx/tree/androidx-main/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3/tokens) и соответствующим реализациям layout/motion: исправлены touch targets/shape кнопок, outlined cutout и affix visibility полей, disabled color roles, switch handle geometry, slider gaps/ticks, progress trajectories, list anatomy и tab indicators. Меню используют baseline MenuTokens, а не новый Expressive StandardMenuTokens. Непроверенные интерактивные состояния не объявляются визуально принятыми.

`MaterialMotion.Animate` по умолчанию использует Material Web `md.sys.motion.spring.fast.spatial` (stiffness 1400, damping 0.9); AndroidX `ExpressiveMotionTokens` дают для fast spatial 800/0.6. Оба набора доступны: перегрузка с `MaterialSpring` берёт значения AndroidX. Компоненты библиотеки используют web-пару, поэтому смешивать их с Compose-значениями в одном UI не следует.

## Упаковка и лицензии

```sh
dotnet pack src/ExpressiveAvalonia/ExpressiveAvalonia.csproj -o artifacts/packages
```

MIT — `LICENSE`; происхождение токенов и шаблонных паттернов — `NOTICE`, тексты сторонних лицензий — `licenses/`. Лицензии зависимостей самостоятельны.
