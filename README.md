# ExpressiveAvalonia

Библиотека Material Design 3 + Material 3 Expressive для Avalonia. **В разработке; полного покрытия компонентов пока нет.**

## Текущее состояние

- Avalonia 12.1.4; библиотека `net8.0`, desktop-каталог `net10.0`.
- `MaterialTheme`: `SeedColor`, `Contrast` от -1 до 1, light/dark и локальный `ThemeVariantScope`.
- HCT Tonal Spot / Spec2021: 49 цветовых ролей и соответствующие brushes. Это классическая схема MD3, не Expressive Spec2025.
- Базовые токены: 15 типографических стилей, shape, state-layer opacity, elevation distances и durations.
- Начальный filled Button: state layers, focus ring, disabled. Остальные варианты и Expressive shape/motion ещё не реализованы.
- Каталог с переключением темы, seed и контраста. Нет зависимости от FluentTheme/SimpleTheme.

## Запуск из репозитория

Нужен .NET SDK 10.

```sh
dotnet build ExpressiveAvalonia.slnx
dotnet run --project samples/ExpressiveAvalonia.Catalog
```

Runtime smoke открывает настоящее desktop-окно, проверяет вызов обработчика действия, disabled state, light/dark, локальную тёмную тему, смену seed/контраста и сохранение счётчика. Выводит результат в консоль и сохраняет `artifacts/catalog-smoke.png`:

```sh
dotnet run --project samples/ExpressiveAvalonia.Catalog -- --smoke
```

Это не проверка физических нажатий мыши/клавиатуры или скринридера. Выполнена на Windows; другие платформы не проверены.

## Подключение темы

Добавьте ссылку на проект библиотеки или локально собранный пакет. Публикация пакета в NuGet не выполнена.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="clr-namespace:ExpressiveAvalonia.Theming;assembly=ExpressiveAvalonia"
             x:Class="Example.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <md:MaterialTheme SeedColor="#6750A4" Contrast="0" />
  </Application.Styles>
</Application>
```

Не подключайте FluentTheme как скрытую подмену отсутствующих Material-шаблонов. На текущем этапе полноценно оформлен только начальный Button; приложение с другими контролами не получит полную тему.

## Ресурсы

- Все цветовые ключи перечислены в `Tokens/MaterialColorScheme.cs`: `Material{Role}Color`. Тема добавляет каждому `Material{Role}Brush`; пример — `MaterialPrimaryBrush`, `MaterialOnPrimaryBrush`, `MaterialSurfaceContainerBrush`.
- Типографика: `Material{Display|Headline|Title|Body|Label}{Large|Medium|Small}{FontSize|LineHeight|FontWeight}`.
- Шрифт: `MaterialFontFamily`, по умолчанию системный; Roboto не вложен.
- Формы: `MaterialShape{None|ExtraSmall|Small|Medium|Large|ExtraLarge|Full}CornerRadius`.
- State layers: `Material{Hover|Focus|Pressed|Dragged}StateLayerOpacity`.
- Elevation: `MaterialElevationLevel0` … `MaterialElevationLevel5`, расстояния, не готовые тени.
- Durations: `MaterialDuration{Short|Medium|Long|ExtraLong}{1|2|3|4}`, значения `TimeSpan`, не физическая модель движения.

Динамические ресурсы применяются через `{DynamicResource MaterialPrimaryBrush}`. Для прямого поиска ресурсов Avalonia передавайте `ActualThemeVariant` в `FindResource`.

## Ограничения зависимостей

Генератор цветов использует `Shirasagi0012.MaterialColorUtilities` 0.2.0 (Apache-2.0). Автор порта помечает его work-in-progress / not production ready. Наличие полного набора ролей не доказывает численное соответствие всем эталонным векторам Google. До production-релиза требуется независимая проверка алгоритма и зависимостей.

Seed alpha игнорируется; цветовые роли непрозрачны. Motion API, Expressive spring animations, enhanced typography, автоматическое reduced motion, MaterialAssist и дополнительные компоненты пока отсутствуют.

Архитектура, полная матрица и критерии готовности: [docs/architecture.md](docs/architecture.md).

## Упаковка

```sh
dotnet pack src/ExpressiveAvalonia/ExpressiveAvalonia.csproj -o artifacts/packages
```

Лицензия библиотеки — MIT, см. `LICENSE`. Лицензии зависимостей сохраняют самостоятельное действие.
