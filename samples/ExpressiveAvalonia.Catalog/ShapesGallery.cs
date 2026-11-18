using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

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
    private readonly ComboBox _mode;
    private readonly StackPanel _shapeSettings = new() { Spacing = 16 };
    private readonly StackPanel _dividerSettings = new() { Spacing = 16, IsVisible = false };
    private readonly MaterialButtonGroup _family;
    private readonly ComboBox _kind;
    private readonly Slider _size;
    private readonly Slider _height;
    private readonly ComboBox _stretch;
    private readonly ComboBox _fill;
    private readonly ToggleSwitch _outline;
    private readonly Slider _thickness;
    private readonly StackPanel _thicknessPanel = new() { Spacing = 8 };
    private readonly TextBlock _sizeCaption = new();
    private readonly TextBlock _heightCaption = new();
    private readonly TextBlock _thicknessCaption = new();
    private readonly ComboBox _dividerOrientation;
    private readonly ComboBox _dividerVariant;
    private readonly Slider _dividerThickness;
    private readonly TextBlock _dividerThicknessCaption = new();
    private readonly TextBox _dividerSubheader;
    private readonly ToggleSwitch _dividerRtl;
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

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

        Settings.Children.Add(new TextBlock { Text = "Режим" });
        _mode = new ComboBox
        {
            ItemsSource = new[] { "Фигуры (MaterialShape)", "Разделители (MaterialDivider)" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_mode, "Режим страницы фигур и разделителей");
        Settings.Children.Add(_mode);
        Settings.Children.Add(_shapeSettings);
        Settings.Children.Add(_dividerSettings);

        _family = Choice(_shapeSettings, "Семейство", new[] { $"MaterialShapes ({Named.Length})", $"Расширения ({Extensions.Length})" }, 0);

        _shapeSettings.Children.Add(new TextBlock { Text = "Геометрия" });
        _kind = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_kind, "Геометрия фигуры");
        _shapeSettings.Children.Add(_kind);

        _sizeCaption.Text = "Размер: 96 dp";
        _shapeSettings.Children.Add(_sizeCaption);
        _size = new Slider { Minimum = 32, Maximum = 192, Value = 96, TickFrequency = 8, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_size, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_size, false);
        AutomationProperties.SetName(_size, "Размер фигуры");
        _shapeSettings.Children.Add(_size);

        _heightCaption.Text = "Высота: ×1.0";
        _shapeSettings.Children.Add(_heightCaption);
        _height = new Slider { Minimum = 0.5, Maximum = 1.5, Value = 1, TickFrequency = 0.1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_height, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_height, false);
        AutomationProperties.SetName(_height, "Высота фигуры");
        _shapeSettings.Children.Add(_height);

        _shapeSettings.Children.Add(new TextBlock { Text = "Растяжение (Stretch)" });
        _stretch = new ComboBox
        {
            ItemsSource = new[] { "Uniform", "Fill", "None" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_stretch, "Растяжение фигуры");
        _shapeSettings.Children.Add(_stretch);

        _shapeSettings.Children.Add(new TextBlock { Text = "Заливка" });
        _fill = new ComboBox
        {
            ItemsSource = new[] { "Как в источнике", "Primary", "Primary container", "Secondary", "Tertiary", "Error", "Outline" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_fill, "Заливка фигуры");
        _shapeSettings.Children.Add(_fill);

        _outline = Toggle(_shapeSettings, "Только обводка", false);
        _thicknessPanel.Children.Add(_thicknessCaption);
        _thicknessCaption.Text = "Толщина обводки: 3 dp";
        _thickness = new Slider { Minimum = 1, Maximum = 8, Value = 3, TickFrequency = 1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_thickness, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_thickness, false);
        AutomationProperties.SetName(_thickness, "Толщина обводки");
        _thicknessPanel.Children.Add(_thickness);
        _shapeSettings.Children.Add(_thicknessPanel);

        _shapeSettings.Children.Add(new TextBlock
        {
            Text = "MaterialShape — настоящая Avalonia Shape: Kind выбирает исходную геометрию, Stretch и " +
                   "локальная заливка переопределяют тему. Без явной заливки Shape Set рисуется ролью primary, " +
                   "а .Shape — primary-container, как в исходнике. Обводка снимает заливку и рисует контур secondary.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _dividerSettings.Children.Add(new TextBlock { Text = "Ориентация" });
        _dividerOrientation = new ComboBox
        {
            ItemsSource = new[] { "Horizontal", "Vertical" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_dividerOrientation, "Ориентация разделителя");
        _dividerSettings.Children.Add(_dividerOrientation);

        _dividerSettings.Children.Add(new TextBlock { Text = "Вариант отступа" });
        _dividerVariant = new ComboBox
        {
            ItemsSource = new[] { "FullWidth", "Inset (16 dp)", "MiddleInset (16 dp × 2)" },
            SelectedIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_dividerVariant, "Вариант отступа разделителя");
        _dividerSettings.Children.Add(_dividerVariant);

        _dividerThicknessCaption.Text = "Толщина линии: 1 dp";
        _dividerSettings.Children.Add(_dividerThicknessCaption);
        _dividerThickness = new Slider { Minimum = 1, Maximum = 4, Value = 1, TickFrequency = 1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_dividerThickness, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_dividerThickness, false);
        AutomationProperties.SetName(_dividerThickness, "Толщина разделителя");
        _dividerSettings.Children.Add(_dividerThickness);

        _dividerSettings.Children.Add(new TextBlock { Text = "Подзаголовок (Subheader)" });
        _dividerSubheader = new TextBox { Text = "Раздел", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_dividerSubheader, MaterialTextFieldVariant.Outlined);
        MaterialTextField.SetLabel(_dividerSubheader, "Subheader");
        AutomationProperties.SetName(_dividerSubheader, "Подзаголовок разделителя");
        _dividerSettings.Children.Add(_dividerSubheader);

        _dividerRtl = Toggle(_dividerSettings, "Right-to-left", false);
        _dividerSettings.Children.Add(new TextBlock
        {
            Text = "MaterialDivider рисует линию 1 dp в роли outline-variant: FullWidth без отступов, " +
                   "Inset — 16 dp с ведущего края, MiddleInset — по 16 dp с обоих краёв, Subheader — " +
                   "подпись title-small через зазор 4 dp. Декоративные разделители исключены из дерева UIA.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

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
            _dividerStage.FlowDirection = _dividerRtl.IsChecked == true
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
            _dividerSubheader.IsEnabled = !vertical;
            _dividerThicknessCaption.Text = $"Толщина линии: {_dividerThickness.Value:0} dp";
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
        _thicknessPanel.IsVisible = outline;
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

        _sizeCaption.Text = $"Размер: {_size.Value:0} dp";
        _heightCaption.Text = $"Высота: ×{_height.Value:0.0}";
        _thicknessCaption.Text = $"Толщина обводки: {_thickness.Value:0} dp";
        var origin = kind == MaterialShapeKind.Hexagon || MaterialShapeGeometries.IsExpressive(kind)
            ? "расширение design source" : "AndroidX MaterialShapes";
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

    private static MaterialButtonGroup Choice(StackPanel host, string label, string[] options, int selected)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        group.SelectedIndex = selected;
        panel.Children.Add(group);
        host.Children.Add(panel);
        return group;
    }

    private static ToggleSwitch Toggle(StackPanel host, string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        host.Children.Add(toggle);
        return toggle;
    }
}
