# ExpressiveAvalonia

Material Design 3 + Material 3 Expressive для Avalonia. Библиотека развивается; весь каталог MD3 пока не покрыт.

## Реализовано

Avalonia 12.1.4, библиотека `net8.0`, desktop-каталог и тесты `net10.0`. Нет зависимости от FluentTheme/SimpleTheme.

- `MaterialTheme`: динамические `SeedColor`, `Contrast` (-1…1), `FontFamily`, `MotionScheme` (Standard/Expressive) и отдельный `IsReducedMotion`; light/dark и локальный ThemeVariantScope.
- HCT Tonal Spot / Spec2021: 49 цветовых ролей и brushes; эталонные проверки Google для HCT, gamut mapping, light/dark и fixed roles. `SurfaceTint` следует Compose `primary`, включая contrast.
- 15 типографических ролей с line height/tracking и emphasized-вариантами; применяются через `MaterialTypography.Style` и `Emphasized`. По умолчанию встроенный Roboto (SIL OFL 1.1), шрифт заменяемый.
- `MaterialTypography.AutoSize` + `MaterialTextAutoSize.StepBased`: opt-in дискретный подбор размера на native TextBlock/TextLayout, включая Run/Span/InlineUIContainer и live изменения rich text. Явный caller FontSize имеет приоритет; `CreateLink` создаёт native inline HyperlinkButton с Material primary/underline и сохранёнными caller overrides.
- Shape, state opacity, elevation distances и двухслойные тени уровней 0…5, durations, `md.sys.motion.easing`-токены; шесть spatial/effects springs для Standard и Expressive. По умолчанию библиотека использует Standard, каталог явно выбирает Expressive.
- `MaterialSurface.TonalElevation` и inherited `TonalElevationEnabled` на Card/InteractionSurface: один SurfaceTint по absolute ancestor elevation; shadow независим. Custom/dedicated container brushes и caller Background binding не переписываются.
- Аналитические прерываемые springs с duration/visibility thresholds, сохранением скорости при retarget и начальной скоростью для реверса; finite tween/delay и spline/exponential decay работают через Avalonia frame callbacks. Oklab color interpolation следует Compose. Reduced motion завершает конечные переходы и останавливает индикаторы; на Windows учитывается настройка анимаций ОС.
- Button/ToggleButton: standard, filled, tonal, outlined, elevated, text; icon mode; пять Expressive-размеров; round/square shape; focus/disabled/state layers. `Standard` — значение по умолчанию: у labeled-кнопки filled-контейнер, у icon-only контейнера нет.
- TextBox: filled/outlined, Inside/Cutout/Above label и Start/Center/End alignment (логические края RTL), prefix/suffix, leading/trailing content, supporting/validation text. Password reveal скрывается при уходе фокуса; `MaterialTextField.TextObfuscationMode` задаёт System/Visible/Hidden/RevealLastTyped (1500 ms). Явные Hidden и RevealLastTyped обеспечивают ненулевой нативный `PasswordChar`, не заменяя исходный binding; при выходе из режима исходное значение восстанавливается. Нативные editing/selection/IME/clipboard APIs сохраняются.
- CheckBox (включая `:error` и indeterminate), RadioButton, ToggleSwitch; switch использует OnContent/OffContent для необязательных иконок.
- Slider: horizontal/vertical, ticks, snapping, value labels, expressive handle. MaterialRangeSlider: два endpoints, клавиатура/drag/RTL, clamp при пересечении, snapping и отдельные automation providers.
- ProgressBar и CircularProgressIndicator: determinate/indeterminate; остановка таймеров при detach, скрытии и reduced motion.
- ListBox/ListBoxItem с виртуализацией и опциональной anatomy через `MaterialList`: automatic line count учитывает измеренный wrapping supporting text, явный `LineCount` имеет приоритет. Primary/secondary tabs, fixed/scrollable headers и стрелки/Home/End.
- Menu/MenuItem, ContextMenu, MenuFlyout; plain tooltip на hover/focus с Escape; RichTooltip с действиями и возвратом фокуса. `RichTooltip.IsPersistent` управляет timeout: без действий default 1500 ms, actionable content по умолчанию persistent.
- Tooltip caret — opt-in: `MaterialToolTip.ShowCaret` для plain и `RichTooltip.ShowCaret` для rich. По умолчанию выключен, как `caretShape=null`; треугольник 16×8 DIP следует фактическому native popup placement и anchor.
- Служебные шаблоны Window/visual layers, ScrollViewer/ScrollBar, Thumb, RepeatButton, ContentControl, presenters, Separator, ComboBox, Calendar и popup hosts.
- `LoadingIndicator`: determinate/custom polygon sequences, contained/uncontained и семифигурный indeterminate цикл с настоящим RoundedPolygon/Morph из закреплённого AndroidX. `ContainerShape`/`ContainerShapeStretch` задают общий контур fill/clip; live `Polygons` replacement следует remembered morph/rotation state. Локальные нестандартные фигуры сохранены отдельно.
- `MaterialPullToRefresh`: внешний `IsRefreshing`, threshold/tension и cancellation, Circular/Loading и custom `IndicatorContent`/`IndicatorTemplate`; `MaxDistance` задаёт visual travel независимо от `Threshold`. Обновление также доступно по F5 и через automation Invoke. `MaterialRefreshIndicatorBox` — standalone transform/shape/clip/elevation для custom indicator. Mouse pull — opt-in extension, колесо не вызывает refresh. `MaterialScrollField`: виртуализированное колесо выбора с modulo state, immediate/animated выбором и native automation; automation сообщает индекс видимого варианта, а не внутреннюю страницу.
- `MaterialSplitButton.IsTrailingCheckable` / `IsTrailingChecked`: независимый trailing toggle без привязки к открытому меню; отдельные commands/disabled flags и native toggle automation.
- `MaterialCarousel.UserScrollEnabled=false` отключает drag/wheel/paging/automation scroll, но не item content, focus/activation и программный `ScrollToItem`.
- Time picker/input/scroll: Standard/Vibrant/Uncontained, native edit rejection без фильтрации/обрезки, `Culture` для localized decimal digits; `AutoAdvance` — явная caller policy. Sheet handle предоставляет native Invoke/ExpandCollapse с anchor/veto/dismiss rules.
- `MaterialExtendedFab.Mode=Classic` добавляет baseline 56 DIP анатомию и text-only перегрузку; Expressive сохраняет Small/Medium/Large. Snackbar по умолчанию показывает весь wrapped message (`LineCount=Auto`); OneLine/TwoLines — явные capped extensions. `XrNavigationRail.Variant=Wide` даёт desktop-композицию с отдельным `Header`, шириной 96 collapsed / 220–360 expanded, без пространственного runtime.

Текущий перенос основан на AndroidX commit `77b2199d6f056c2fe829ce25523c34a905e769c1` (development snapshot, не stable release). Соответствие каждого семейства принимается отдельно; наличие типа или страницы каталога не означает полного переноса. Отрисованы 32 страницы light/dark и 68 preview images (включая вложенные tabs); выполнены численные spring/geometry/parallax smokes, реальные frame transitions, routed-input/state сценарии search, overlays, actions, selection, refresh/scroll-field. Date picker дополнительно содержит horizontal month paging в day view, boundary keyboard focus handoff, year reveal geometry и animated date-selection colors; ScrollField interpolates source typography/color transitions. Изолированный NuGet consumer запускается без project reference и Fluent/Simple, загружает встроенный Roboto и выполняет native button input. Физический touch/pen, screen reader и Android raster этой проверкой не покрыты.

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

Каталог оформлен как Expressive-студия без верхней шапки: слева сворачиваемый Navigation rail, в центре единая скруглённая поверхность с заголовком и прокручиваемыми примерами, справа панель параметров шириной 380 dp без отдельных карточек вокруг групп. Каталог содержит только `MD3 Expressive`; раздел `MD3 Classic` и переключатель редакций удалены. Сверху rail остаётся кнопка сворачивания, доступная также в компактном режиме. Библиотечные Material-токены и стили MD3 не изменены.

Общие параметры открываются кнопкой «Настройки» внизу левого меню: светлая/тёмная тема, reduced motion, ширина и минимальная высота области, доступность примеров и RTL. Они сохраняются в текущем окне и применяются при переключении страниц. Справа остаются настройки конкретного компонента: размер кнопок, ширина и высота карточек, количество действий FAB от 2 до 6, иконки и раскрытие меню. На странице Sliders по центру расположен один интерактивный слайдер; справа выбираются обычный, центрированный или диапазонный вид, один из пяти размеров, ориентация, длина, значения, шаг, метки, иконка и состояния. Диапазонный слайдер поддерживает только горизонтальную ориентацию. Настройки справа остаются доступны при отключённых примерах.
Параметры слайдера «Вид», «Размер» и «Ориентация» выбираются через заполненные Expressive split-кнопки: слева иконка и текущее значение, справа отдельная часть с шевроном. Обе части открывают скруглённое меню; выбранный пункт выделен и сразу обновляет пример. Используются `MaterialSplitButton` и `MenuFlyout`. Числовые поля оформлены как Outlined Text fields с плавающей подписью в вырезе рамки, без дублирующего заголовка сверху.
Длина задаётся числом от 120 до 1440 dp; для вертикального слайдера это высота. Значение и обе границы диапазона редактируются в числовых полях и синхронизируются при перетаскивании. В дискретном режиме вместо ручного шага задаётся количество точек от 2 до 51, включая края шкалы; шаг равен длине числового диапазона, делённой на количество интервалов. Точки под ручками и в зазорах скрываются. Под примером постоянно показано текущее значение или диапазон; всплывающая подпись использует до двух десятичных знаков и расширяется под текст.

Страница `Text fields` показывает одно редактируемое поле по центру и параметры справа: Filled/Outlined, ширину, подпись, placeholder, supporting text, префикс/суффикс, иконки, очистку, пароль с показом, многострочный ввод, ошибку, доступность, read-only и RTL. Переключение параметров не сбрасывает введённый текст; для сброса есть отдельная кнопка. Наведение и фокус проверяются непосредственно на поле. Используется существующий шаблон TextBox.
Вид поля выбирается связанной группой кнопок `Filled / Outlined` с галочкой у выбранного варианта. Ширина регулируется слайдером от 240 до 560 dp с шагом 1 dp; текущее значение показано в подписи.

Правая панель имеет собственную вертикальную прокрутку. Конфигурации слайдера следуют [Material 3 guidelines](https://m3.material.io/components/sliders/guidelines): непрерывный и дискретный режимы разделены; метки используют `TickFrequency` и совпадают с привязкой; inset-иконка доступна только обычному слайдеру M/L/XL и переходит на неактивный трек, если активный слишком короткий. Для центрированного и диапазонного вариантов, а также XS/S inset-иконка не рисуется.
Промежуточные дискретные метки рисуются на активном и неактивном участках трека с одинаковым шагом. Зазор вокруг ручки свободен от меток. Кнопка выбора и пункты меню принимают нажатия по всей площади, включая отступы.
Геометрия и состояния сверены с [Material 3 Slider specs](https://m3.material.io/components/sliders/specs): трек XS/S/M/L/XL — 16/24/40/56/96 dp, ручка — 44/44/52/68/108 dp, внешние радиусы — 8/8/12/16/28 dp, внутренние углы — 2 dp. Внешняя форма не зависит от длины участка. От края ручки до трека — 6 dp; ширина ручки — 4 dp, при нажатии и клавиатурном фокусе — 2 dp. В центрированном варианте центральный разрыв не пересекается с зазором ручки около нуля.
Значение по умолчанию не всплывает (`MaterialSlider.ShowValueIndicator="False"`); в каталоге «Подсказка значения» включена явно. При захвате или клавиатурном фокусе подпись высотой 44 dp и минимальной шириной 48 dp располагается над ручкой (слева для вертикальной). Текст — 14/20 dp, отступы по горизонтали — 16 dp. Контур клавиатурного фокуса повторяет форму ручки; восстановление фокуса окна его не включает. Метки на активном треке используют OnPrimary, на неактивном — OnSecondaryContainer. Отключённые трек/ручка используют OnSurface с непрозрачностью 38% для активного участка и ручки, 12% для неактивного.
Подсказка центрируется по текущей экранной позиции ручки и следует за ней при перетаскивании; область привязки остаётся внутри трека, наружный отступ задаётся смещением Popup. Нативное окно подсказки имеет прозрачный фон и прозрачный режим композиции, без прямоугольной подложки на тёмной теме. В центрированном варианте разрыв между активным и неактивным блоками составляет суммарно 4 dp для всех размеров XS–XL; фокусный контур не увеличивает отступ трека от ручки.
Каждый участок трека скругляется отдельно: большой внешний радиус применяется только к настоящему концу шкалы, а не к срезу возле ручки. Короткие участки — обрезанная часть той же круговой дуги с неизменным радиусом, без сжатия в эллипс. Конечная метка скрывается, если для неё нет места.

Страницы MD3 Expressive создаются при открытии, а их деревья контролов освобождаются при уходе; посещённые страницы не кэшируются. Вложенные вкладки Assets, Shapes и Examples также создают содержимое только при открытии. В текущем каталоге используется DeferredGallery на уровне страниц; ViewportGallery не подключён к примерам и не обеспечивает их выгрузку по viewport. При возвращении локальное состояние страницы пересоздаётся; общие ресурсы темы сохраняются. Полный экспорт `--preview` раскрывает содержимое для снимков. Фактическая экономия ОЗУ не измерялась.

```sh
dotnet run --project samples/ExpressiveAvalonia.Catalog -- --smoke
```

Полные превью страниц каталога (light/dark, вложенные вкладки, содержимое ниже первого экрана):

```sh
dotnet run --project samples/ExpressiveAvalonia.Catalog -- --preview
```

Откройте `artifacts/previews/index.html` напрямую в браузере: сервер не нужен. PNG-снимки реальных контролов находятся рядом; длинные страницы разбиты на плитки до 1600 пикселей. Поиск фильтрует страницы по семейству и теме. Это обзор элементов каталога, не перебор всех 5599 комбинаций состояний; popup и другие интерактивные состояния доступны в живом каталоге.

Desktop smoke проверяет смену темы/seed/контраста, затем отрисовывает 32 страницы `MD3 Expressive` в light/dark с seed `#6750A4` и `Contrast=0`; открывает/закрывает rich tooltip через Escape. Снимки — `artifacts/catalog-design-{light|dark}-{0..31}.png`. Навигация и панель параметров прокручиваются независимо.
Панель параметров на всех страницах MD3 Expressive начинается с настроек без общей шапки, пояснения об области применения и повторного названия компонента; подписи параметров и групп сохранены.

Проверено на Windows: desktop smoke (64 отрисовки: 32 страницы × две темы), смена темы/seed/контраста и rich tooltip с routed Escape. Это не попиксельная сверка всех 5599 вариантов. Выполненные input/motion сценарии перечислены в workbook; все сочетания hover/press/focus, физические устройства ввода, скринридеры, mobile и Linux/macOS не проверены. Это не сертификат доступности или полного соответствия MD3.

Дополнительно Skia/headless отрисовал 512 кадров: все 32 страницы × 100/125/150/200% simulated DPI × light/dark × LTR/RTL. Проверены размер framebuffer и текстовые поля на 125% LTR/200% RTL; физические дисплеи и native DPI switching этим не проверяются.

Заключительная интеграция 2026-10-27: 173/173 behavioral regressions; 330 button variant/size/shape/theme/RTL и icon-width конфигураций с native input; hover/focus/disabled FAB/chip shadows, general-item bounded ripple, toggle border и retained chip/segment exits; navigation/tab/menu feedback и modal rail drag/cancel/RTL/focus. Date picker поддерживает animated day-view month paging, year reveal, selection colors и виртуализированный full-screen range browser. Native time input проверен на third-digit replacement, reject paste, live Arabic digits и Scroll culture; refresh — на custom-template bindings, независимый MaxDistance и rendered shape clipping; carousel — на запрет user scroll без отключения child actions и empty collection lifecycle. Каталог прошёл 64 page renders light/dark и 68 previews. Изолированный NuGet consumer без ProjectReference/Fluent/Simple проверяет native templates/input и Roboto; применимые лицензии и `NOTICE` включены в пакет. Подробности и границы платформенной приёмки — [workbook](docs/compose-port/README.md).

## Подключение

Добавьте ProjectReference или локально собранный NuGet-пакет. Публикация в NuGet не выполнена.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="clr-namespace:ExpressiveAvalonia.Theming;assembly=ExpressiveAvalonia"
             x:Class="Example.App" RequestedThemeVariant="Default">
  <Application.Styles>
    <md:MaterialTheme SeedColor="#6750A4" Contrast="0" MotionScheme="Expressive" />
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

Страница `Buttons` показывает одну живую кнопку с настройками справа: Button/ToggleButton, текст/иконка, Filled/Tonal/Outlined/Elevated/Text, пять размеров, Round/Square, ширина icon-контейнера, подпись, ведущая иконка, выбранность, доступность и RTL. Счётчик отражает нажатия; выбранность ToggleButton синхронизируется с инспектором.

Страница `Selection` переключается между CheckBox, группой из трёх RadioButton и ToggleSwitch. Справа — подпись, состояние, трёхпозиционный режим и ошибка флажка, выбранная радиокнопка, встроенные иконки switch, доступность и RTL. Нативные изменения отражаются в настройках; изменение подписи и оформления не сбрасывает выбор.

Страница `Navigation rail` показывает один живой пример с настройками справа: Compact/Expanded/Modal, Docked/Floating, Top/Middle, количество пунктов и высота, подписи, badges, меню, FAB и его текст, доступность и RTL. Выбор сохраняется при смене оформления; modal rail открывается кнопкой, закрывается по Escape, scrim или выбору пункта (последние два поведения настраиваются).
`MaterialNavigationRailHost.HideOnCollapse` по умолчанию `false`: свёрнутый rail остаётся в layout под модальным expanded rail. Для data `ItemsSource` host создаёт общую по данным/выбору свёрнутую презентацию; для готовых control items задайте отдельный `CollapsedRail` (один Control нельзя разместить в двух visual trees). `CollapsedPresentation` возвращает реально размещённый rail. `HideOnCollapse=true` убирает свёрнутую презентацию и её inset.

Все 32 страницы MD3 Expressive используют живые примеры и профильные настройки справа. Семейства и варианты переключаются там же: Snackbar, FAB/XR, app bars/toolbars/headers, button groups, chips, dialogs/sheets, navigation, date/time pickers, lists, menus/tooltips, shapes/avatars, cards, carousel, pull-to-refresh, scroll field и scrollbars. Assets показывает глиф/палитру; Examples — экран/layout/keyboard/building block/device chrome/color schematic и native rich text/autosize/links. Modal и popup — настоящие controls/hosts. Общие тема/reduced/размер/disabled/RTL остаются в «Настройках».
`MaterialButtonGroupItem` сохраняет один визуальный экземпляр `Icon` и `Content` при переключении Standard/Connected/Segmented; пользовательские controls не копируются и не получают второго visual parent.
Нативный `PathIcon` имеет собственный инфраструктурный шаблон: геометрия масштабируется через Viewbox и окрашивается Foreground. Это обеспечивает отрисовку иконок действий и FAB без подключения сторонней темы.

Подписи CheckBox/RadioButton/ToggleSwitch центрируются и переносятся в узком контейнере. Native selection/keyboard/automation сохранены; Checkbox check path и indeterminate gravitation следуют pinned source: enter spring, выход сохраняет силуэт 100 ms и затем snap. Произвольного press scale нет.
Ripple использует pinned 75 ms alpha-in, 225 ms radius/center и 150 ms fade-out; быстрый release завершает раскрытие перед fade. Клавиатура задаёт центральный origin, pointer — точку нажатия; reduced motion показывает статический отклик.
У флажка волна круглая: фигурная векторная маска с дуговым срезом удалена; остаётся только внешняя круглая граница области отклика.

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
Активная полоса диапазона отделена зазором 8 dp от центра каждой ручки. При совпадении значений две ручки остаются раздельными, без активной заливки и дополнительной точки между ними.
`ValueChangeCompleted` вызывается также после отпускания Up/Down. Дискретная шкала использует одинаковую геометрию для отображения и указателя.

### Lists

На нативном `ListBoxItem` доступны `MaterialList.Headline`, `SupportingText`, `LeadingContent`, `TrailingContent`, `LineCount` (1/2/3). Минимальная высота — 56/72/88 dp соответственно. Без `Headline` сохраняются обычные `Content`/`ContentTemplate`. Leading/trailing content принимает пользовательские иконки, аватары или контролы.

`MaterialSwipeListItem` раскрывает действия при переходе к ним клавиатурным фокусом; их кнопки доступны через Tab. Смена разрешённого направления жеста обновляет сторону панели действий.

### Tabs, rich tooltip и motion

- `MaterialTabs.Variant="Primary|Secondary"`, `MaterialTabs.IsScrollable="True"` на TabControl.
- Страница `Tabs` содержит один живой пример. Справа — связанные кнопки Primary/Secondary и текст/иконка/оба, слайдеры ширины 240–800 dp и количества вкладок 2–12, прокрутка, badge, недоступная последняя вкладка, доступность и RTL. Изменение оформления сохраняет содержимое оставшихся страниц.
- Tabs используют pinned `Tab.kt`/`TabRow.kt`: заголовки 48 DIP, Primary с иконкой и текстом — 72 DIP; иконки 24 DIP, разделитель 1 DIP. Primary-индикатор высотой 3 DIP имеет минимум 24 DIP и верхние скругления; Secondary — 3 DIP на всю ширину вкладки (`TabRowDefaults.SecondaryIndicator` использует `PrimaryNavigationTabTokens.ActiveIndicatorHeight`).
- Страница `Search` показывает один живой пример Contained (Expressive), согласно [спецификации Search](https://m3.material.io/components/search/specs). Справа выбираются Search bar/Search view и Docked/Full-screen, ширина 360–720 dp, placeholder, иконки строки, аватар, подсказки, доступность и RTL. Full-screen ограничен поверхностью примера. Запрос сохраняется при переключениях; подсказки фильтруются по названиям компонентов без учёта регистра. Enter запускает поиск, Down переводит выбор к подсказкам; закрытый view можно открыть снова. Голосовой action только сообщает о запросе: распознавание речи не подключено.
- Страница `Progress` показывает Expressive-индикаторы по [спецификации progress indicators](https://m3.material.io/components/progress-indicators/specs): Linear/Circular, Flat/Wave, толщину 4–8 dp и Loading с семью начальными формами и необязательным контейнером. Справа регулируются значение, длина/диаметр, длина волны/число волн, дорожка Circular и RTL. Анимации включены по умолчанию: determinate демонстрирует циклическое заполнение, indeterminate и Loading используют собственные циклы. Loading повторяет Motion из androidx `LoadingIndicator`: одна заливка на кадр без кроссфейда, морф формы пружиной 0.6/200 каждые 650 мс, +90° за морф и один медленный оборот за 4666 мс. Переключатель «Анимация» останавливает движение и разрешает ручной процент; reduced motion останавливает анимации независимо от переключателя. Таймер демонстрационного заполнения отключается при скрытии и detach и возобновляется при возвращении.
- Progress использует геометрию и timelines закреплённого AndroidX: движущаяся волна Linear/Circular, amplitude flattening у краёв диапазона, остаточная circular track с gaps. Loading использует RoundedPolygon, измерение контура и feature-matched Morph из graphics-shapes; прежняя интерполяция по 256 точкам удалена. Демонстрационное заполнение каталога — отдельный сценарий, не контракт индикатора.
- Цвет активного Progress и конечной точки — `MaterialPrimaryBrush`, оставшейся дорожки — `MaterialSecondaryContainerBrush`. Дорожка должна визуально отличаться от заполнения; Linear хранит отдельные перья для этих двух ролей.
- `RichTooltip : Flyout`: `Title`, `Content`, `Actions`, `ActionsTemplate`. Назначьте Button.Flyout или вызовите ShowAt(owner); Hide закрывает. Открывается явно, а не hover-таймером, чтобы не красть фокус. Escape/light dismiss используют native flyout lifecycle.
- `MaterialMotion.IsReduced="True"` наследуется поддеревом. Системный Windows reduced mode имеет приоритет.
- `MaterialMotion.Animate(owner, from, to, apply, spring)` выбирает spec из наследуемой схемы; численная перегрузка требует явные stiffness и dampingRatio. `Retarget(target)` сохраняет положение и скорость; `Dispose` отменяет без snap, detach/reduced завершают к цели. `Tween`, `Decay` и `ExponentialDecay` используют тот же Avalonia frame clock.

## Ресурсы

- Цвет: `Material{Role}Color` / `Material{Role}Brush`; полный перечень в `Tokens/MaterialColorScheme.cs`.
- Типографика: `Material{Display|Headline|Title|Body|Label}{Large|Medium|Small}{FontSize|LineHeight|FontWeight|LetterSpacing}`. Emphasis вставляет `Emphasized` перед последним суффиксом.
- `MaterialFontFamily`; Roboto вложен в пакет (SIL OFL 1.1), сетевой загрузки нет.
- `MaterialShape{None|ExtraSmall|Small|Medium|Large|ExtraLarge|Full}CornerRadius`.
- `Material{Hover|Focus|Pressed|Dragged}StateLayerOpacity`.
- `MaterialElevationLevel0` … `5` и `MaterialElevationLevel0Shadow` … `5Shadow`.
- `MaterialDuration{Short|Medium|Long|ExtraLong}{1|2|3|4}` типа TimeSpan.
- `MaterialEasing{Standard|StandardAccelerate|StandardDecelerate|Emphasized|EmphasizedAccelerate|EmphasizedDecelerate|Legacy|LegacyAccelerate|LegacyDecelerate|Linear}` — `IEasing` для `Transitions` и кода.
- `MaterialMotionScheme`; `MaterialSpring{Spatial|Effects}{Default|Fast|Slow}{Stiffness|Damping}` — параметры выбранной схемы AndroidX StandardMotionTokens/ExpressiveMotionTokens.
- Строки шаблонов: `MaterialTextInput{Cut|Copy|Paste|SelectAll}Text`, `MaterialPassword{Show|Hide}Text`, `MaterialRange{Lower|Upper}ValueText`, `MaterialScroll{Left|Right|Up|Down}Text`, `MaterialPage{Left|Right|Up|Down}Text`, `MaterialScrollPositionText`. Переопределите эти ключи в ресурсах приложения, чтобы локализовать встроенные automation names, tooltips и контекстное меню.

Используйте DynamicResource. В прямом FindResource укажите ActualThemeVariant. Визуальная вертикальная линия: Separator Classes="vertical".

## Ограничения

Генератор использует `Shirasagi0012.MaterialColorUtilities` 0.2.0 (Apache-2.0); автор помечает порт WIP. Независимые reference tests проверяют конкретные эталоны, а не весь алгоритм. Scheme — классический Tonal Spot Spec2021/phone, не Spec2025. Seed alpha игнорируется.

Семейства FAB/chips/cards, dialogs/sheets/snackbar, navigation, carousel и date/time pickers реализованы; поэлементные source mappings, desktop-адаптации и выполненные проверки перечислены в [плане переноса](docs/compose-port/README.md) и [матрице покрытия](docs/architecture.md). Инфраструктурный Calendar не подменяет MaterialDatePicker. Progress поддерживает rounded/gapped tracks, stop marker и движущиеся Expressive wave-индикаторы.

Источник layout/state/motion — AndroidX Material3 snapshot [`77b2199d6f056c2fe829ce25523c34a905e769c1`](https://github.com/androidx/androidx/tree/77b2199d6f056c2fe829ce25523c34a905e769c1/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3). Меню имеют отдельные Baseline и Expressive Standard/Vibrant варианты; public appearance defaults и desktop extensions описаны в workbook. Независимое Android raster comparison, физические touch/pen/IME, screen reader и другие ОС не проверены; успешная отрисовка каталога не означает проверки каждого интерактивного состояния.

`MaterialMotion.Animate` выбирает `SpatialFast` из наследуемого `MaterialMotion.Scheme`: Expressive — 800/0.6, Standard — 1400/0.9 (stiffness/damping). Численная перегрузка требует оба параметра явно. Траектория аналитическая, duration берётся из AndroidX SpringEstimation, retarget сохраняет положение и скорость; кадры запрашиваются у Avalonia TopLevel. Порог задаётся в единицах значения: Float — 0.01, Dp — 0.4 по закреплённому source. `IsReducedMotion` независимо от схемы завершает springs и обнуляет duration-токены; Windows policy учитывается также ресурсами темы.

Численный smoke основы: 324 samples против независимого RK4 для stiffness 200/800/3800 и damping 0.6/1/1.5, максимальная абсолютная ошибка положения/скорости `1.50e-11`; 81 сценарий duration/retarget и 64 Oklab round-trip. Это не проверка визуального соответствия компонентов и не замена Android reference runner.

## Упаковка и лицензии

```sh
dotnet pack src/ExpressiveAvalonia/ExpressiveAvalonia.csproj -o artifacts/packages
```

Исходный код проекта — MIT (`LICENSE`); адаптированные части AndroidX — Apache-2.0, встроенный Roboto — OFL-1.1. SVG/boards и avatar/placeholder assets из Material 3 Design Kit (Community) — CC-BY-4.0: заявление лицензии найдено в исходном overview node `58956:9362`; атрибуция, версия экспорта и преобразования сохранены в `NOTICE`. Выражение лицензии пакета: `MIT AND Apache-2.0 AND OFL-1.1 AND CC-BY-4.0`. Применимые тексты включены в NuGet (`licenses/`). Лицензии зависимостей самостоятельны; CC-BY не предоставляет права на товарные знаки, частную жизнь и изображение личности.
