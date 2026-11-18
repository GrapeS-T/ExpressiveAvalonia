using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live Material shape with its geometry, size, stretch and paint exposed.</summary>
public static class ShapesGallery
{
    public static Control Create() => new ShapesPlayground();
}

internal sealed class ShapesPlayground : Grid, IComponentPlayground
{
    private static readonly MaterialShapeKind[] Named = Array.FindAll(
        Enum.GetValues<MaterialShapeKind>(),
        kind => !MaterialShapeGeometries.IsExpressive(kind) && kind != MaterialShapeKind.Hexagon);

    private static readonly MaterialShapeKind[] Extensions = Array.FindAll(
        Enum.GetValues<MaterialShapeKind>(),
        kind => MaterialShapeGeometries.IsExpressive(kind) || kind == MaterialShapeKind.Hexagon);

    private static readonly string[] FillKeys =
    {
        "MaterialPrimaryBrush",
        "MaterialPrimaryContainerBrush",
        "MaterialSecondaryBrush",
        "MaterialTertiaryBrush",
        "MaterialErrorBrush",
        "MaterialOutlineBrush",
    };

    private readonly MaterialShape _shape = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Border _shapeFrame;
    private readonly Border _dividerStage;
    private readonly MaterialDivider _liveDivider = new();
    private readonly Border _liveDividerHost = new() { Width = 320, Height = 88 };
    private readonly TextBlock _caption = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };
    private readonly MaterialButtonGroup _mode;
    private readonly StackPanel _shapeSettings;
    private readonly StackPanel _dividerSettings;
    private readonly MaterialButtonGroup _family;
    private readonly ComboBox _kind;
    private readonly Slider _size;
    private readonly Slider _height;
    private readonly ComboBox _stretch;
    private readonly ComboBox _fill;
    private readonly ToggleSwitch _outline;
    private readonly Slider _thickness;
    private readonly ComboBox _dividerOrientation;
    private readonly ComboBox _dividerVariant;
    private readonly Slider _dividerThickness;
    private readonly TextBox _dividerSubheader;
    private readonly ToggleSwitch _dividerRtl;
    private bool _syncing;
    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal ShapesPlayground()
    {
        _shapeFrame = new Border
        {
            Width = 360,
            Height = 360,
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _shape,
        };
        _shapeFrame.Bind(Border.BackgroundProperty, _shapeFrame.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        _dividerStage = BuildDividerStage();
        _dividerStage.IsVisible = false;

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(_shapeFrame);
        stage.Children.Add(_dividerStage);
        stage.Children.Add(_caption);
        Children.Add(stage);

        var modeSection = InspectorLayout.Section(Settings, divider: false);
        _mode = InspectorLayout.Choice(modeSection, "Режим", new[] { "Фигуры", "Разделители" }, 0);
        AutomationProperties.SetName(_mode, "Режим страницы фигур и разделителей");

        _shapeSettings = InspectorLayout.Section(Settings, "Фигура (MaterialShape)");
        _family = InspectorLayout.Choice(
            _shapeSettings,
            "Семейство",
            new[] { $"MaterialShapes ({Named.Length})", $"Расширения ({Extensions.Length})" },
            0);
        _kind = InspectorLayout.Combo(_shapeSettings, "Геометрия");
        AutomationProperties.SetName(_kind, "Геометрия фигуры");
        _size = InspectorLayout.Slider(_shapeSettings, "Размер", 32, 192, 96, 8, "dp", "0");
        AutomationProperties.SetName(_size, "Размер фигуры");
        _height = InspectorLayout.CustomSlider(_shapeSettings, "Высота", 0.5, 1.5, 1.0, 0.1, v => $"×{v:0.0}");
        AutomationProperties.SetName(_height, "Высота фигуры");
        _stretch = InspectorLayout.Combo(_shapeSettings, "Растяжение (Stretch)", new[] { "Uniform", "Fill", "None" }, 0);
        AutomationProperties.SetName(_stretch, "Растяжение фигуры");
        _fill = InspectorLayout.Combo(
            _shapeSettings,
            "Заливка",
            new[] { "Как в источнике", "Primary", "Primary container", "Secondary", "Tertiary", "Error", "Outline" },
            0);
        AutomationProperties.SetName(_fill, "Заливка фигуры");
        var shapeSwitches = InspectorLayout.Switches(_shapeSettings);
        _outline = InspectorLayout.Switch(shapeSwitches, "Только обводка", false);
        _thickness = InspectorLayout.Slider(_shapeSettings, "Толщина обводки", 1, 8, 3, 1, "dp", "0");
        InspectorLayout.Note(
            _shapeSettings,
            "MaterialShape — настоящая Avalonia Shape: Kind выбирает исходную геометрию, Stretch и " +
            "локальная заливка переопределяют тему. Без явной заливки Shape Set рисуется ролью primary, " +
            "а .Shape — primary-container, как в исходнике. Обводка снимает заливку и рисует контур secondary.");

        _dividerSettings = InspectorLayout.Section(Settings, "Разделитель (MaterialDivider)");
        _dividerSettings.IsVisible = false;
        _dividerOrientation = InspectorLayout.Combo(_dividerSettings, "Ориентация", new[] { "Horizontal", "Vertical" }, 0);
        AutomationProperties.SetName(_dividerOrientation, "Ориентация разделителя");
        _dividerVariant = InspectorLayout.Combo(
            _dividerSettings,
            "Вариант отступа",
            new[] { "FullWidth", "Inset (16 dp)", "MiddleInset (16 dp × 2)" },
            2);
        AutomationProperties.SetName(_dividerVariant, "Вариант отступа разделителя");
        _dividerThickness = InspectorLayout.Slider(_dividerSettings, "Толщина линии", 1, 4, 1, 1, "dp", "0");
        AutomationProperties.SetName(_dividerThickness, "Толщина разделителя");
        _dividerSubheader = InspectorLayout.TextField(_dividerSettings, "Подзаголовок (Subheader)", "Раздел");
        AutomationProperties.SetName(_dividerSubheader, "Подзаголовок разделителя");
        var dividerSwitches = InspectorLayout.Switches(_dividerSettings);
        _dividerRtl = InspectorLayout.Switch(dividerSwitches, "Right-to-left", false);
        InspectorLayout.Note(
            _dividerSettings,
            "MaterialDivider рисует линию 1 dp в роли outline-variant: FullWidth без отступов, " +
            "Inset — 16 dp с ведущего края, MiddleInset — по 16 dp с обоих краёв, Subheader — " +
            "подпись title-small через зазор 4 dp. Декоративные разделители исключены из дерева UIA.");

        _mode.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _family.SelectionChanged += (_, _) => { if (!_syncing) PopulateKinds(); };
        _kind.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _size.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _height.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _stretch.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _fill.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _outline.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        _thickness.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _dividerOrientation.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _dividerVariant.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _dividerThickness.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _dividerSubheader.TextChanged += (_, _) => { if (!_syncing) Apply(); };
        _dividerRtl.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };

        PopulateKinds();
    }

    private MaterialShapeKind[] CurrentKinds => _family.SelectedIndex == 1 ? Extensions : Named;

    private void PopulateKinds()
    {
        _syncing = true;
        var kinds = CurrentKinds;
        _kind.ItemsSource = Array.ConvertAll(kinds, kind => kind.ToString());
        _kind.SelectedIndex = 0;
        _syncing = false;
        Apply();
    }

    private void Apply()
    {
        var dividerMode = _mode.SelectedIndex == 1;
        _shapeFrame.IsVisible = !dividerMode;
        _shapeSettings.IsVisible = !dividerMode;
        _dividerStage.IsVisible = dividerMode;
        _dividerSettings.IsVisible = dividerMode;

        if (dividerMode)
        {
            var vertical = _dividerOrientation.SelectedIndex == 1;
            var variant = (MaterialDividerVariant)Math.Clamp(_dividerVariant.SelectedIndex, 0, 2);
            _liveDivider.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
            _liveDivider.Variant = variant;
            _liveDivider.LineThickness = _dividerThickness.Value;
            _liveDivider.Subheader = vertical || string.IsNullOrWhiteSpace(_dividerSubheader.Text)
                ? null
                : _dividerSubheader.Text;
            InspectorLayout.ApplyFlowDirection(_dividerStage, _dividerRtl);
            _dividerSubheader.IsEnabled = !vertical;
            _caption.Text = $"MaterialDivider · {_liveDivider.Orientation} · {variant} · {_dividerThickness.Value:0} dp";
            return;
        }

        var kinds = CurrentKinds;
        var index = Math.Clamp(_kind.SelectedIndex, 0, kinds.Length - 1);
        var kind = kinds[index];

        _shape.Kind = kind;
        _shape.Width = _size.Value;
        _shape.Height = _size.Value * _height.Value;
        _shape.Stretch = _stretch.SelectedIndex switch
        {
            1 => Stretch.Fill,
            2 => Stretch.None,
            _ => Stretch.Uniform,
        };

        var outline = _outline.IsChecked == true;
        _thickness.IsVisible = outline;
        if (outline)
        {
            _shape.ClearValue(Shape.FillProperty);
            _shape.Fill = null;
            _shape.ClearValue(Shape.StrokeProperty);
            _shape.Bind(Shape.StrokeProperty, _shape.GetResourceObservable("MaterialSecondaryBrush"));
            _shape.StrokeThickness = _thickness.Value;
        }
        else
        {
            _shape.Stroke = null;
            _shape.StrokeThickness = 0;
            _shape.ClearValue(Shape.FillProperty);
            if (_fill.SelectedIndex > 0)
                _shape.Bind(Shape.FillProperty, _shape.GetResourceObservable(
                    FillKeys[Math.Clamp(_fill.SelectedIndex - 1, 0, FillKeys.Length - 1)]));
        }

        var origin = kind == MaterialShapeKind.Hexagon || MaterialShapeGeometries.IsExpressive(kind)
            ? "расширение design source"
            : "AndroidX MaterialShapes";
        _caption.Text = $"{kind} · {origin}\n{_size.Value:0}×{_shape.Height:0} dp · {_shape.Stretch}";
    }

    private Border BuildDividerStage()
    {
        _liveDividerHost.Child = _liveDivider;
        _liveDividerHost.CornerRadius = new CornerRadius(12);
        _liveDividerHost.BorderThickness = new Thickness(1);
        _liveDividerHost.Bind(Border.BackgroundProperty, _liveDividerHost.GetResourceObservable("MaterialSurfaceBrush"));
        _liveDividerHost.Bind(Border.BorderBrushProperty, _liveDividerHost.GetResourceObservable("MaterialOutlineVariantBrush"));

        var verticalRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Full", VerticalAlignment = VerticalAlignment.Center },
                new MaterialDivider { Orientation = Orientation.Vertical, Variant = MaterialDividerVariant.FullWidth },
                new TextBlock { Text = "Inset", VerticalAlignment = VerticalAlignment.Center },
                new MaterialDivider { Orientation = Orientation.Vertical, Variant = MaterialDividerVariant.Inset },
                new TextBlock { Text = "Middle", VerticalAlignment = VerticalAlignment.Center },
                new MaterialDivider { Orientation = Orientation.Vertical, Variant = MaterialDividerVariant.MiddleInset },
                new TextBlock { Text = "End", VerticalAlignment = VerticalAlignment.Center },
            },
        };

        var stack = new StackPanel
        {
            Width = 340,
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Интерактивный экземпляр (слот 88 dp)", FontWeight = FontWeight.Medium },
                _liveDividerHost,
                new TextBlock { Text = "Варианты Horizontal и Vertical", FontWeight = FontWeight.Medium },
                new MaterialDivider { Variant = MaterialDividerVariant.FullWidth },
                new MaterialDivider { Variant = MaterialDividerVariant.Inset },
                new MaterialDivider { Variant = MaterialDividerVariant.MiddleInset, Subheader = "Subheader · 16 dp inset" },
                verticalRow,
            },
        };

        var card = new Border
        {
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));
        return card;
    }
}
