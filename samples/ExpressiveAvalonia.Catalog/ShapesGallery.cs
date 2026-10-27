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
    private readonly TextBlock _caption = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };
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
    private bool _syncing;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

    internal ShapesPlayground()
    {
        var frame = new Border
        {
            Width = 360,
            Height = 360,
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _shape,
        };
        frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(frame);
        stage.Children.Add(_caption);
        Children.Add(stage);

        _family = Choice("Семейство", new[] { $"MaterialShapes ({Named.Length})", $"Расширения ({Extensions.Length})" }, 0);

        Settings.Children.Add(new TextBlock { Text = "Геометрия" });
        _kind = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_kind, "Геометрия фигуры");
        Settings.Children.Add(_kind);

        _sizeCaption.Text = "Размер: 96 dp";
        Settings.Children.Add(_sizeCaption);
        _size = new Slider { Minimum = 32, Maximum = 192, Value = 96, TickFrequency = 8, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_size, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_size, false);
        AutomationProperties.SetName(_size, "Размер фигуры");
        Settings.Children.Add(_size);

        _heightCaption.Text = "Высота: ×1.0";
        Settings.Children.Add(_heightCaption);
        _height = new Slider { Minimum = 0.5, Maximum = 1.5, Value = 1, TickFrequency = 0.1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_height, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_height, false);
        AutomationProperties.SetName(_height, "Высота фигуры");
        Settings.Children.Add(_height);

        Settings.Children.Add(new TextBlock { Text = "Растяжение (Stretch)" });
        _stretch = new ComboBox
        {
            ItemsSource = new[] { "Uniform", "Fill", "None" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_stretch, "Растяжение фигуры");
        Settings.Children.Add(_stretch);

        Settings.Children.Add(new TextBlock { Text = "Заливка" });
        _fill = new ComboBox
        {
            ItemsSource = new[] { "Как в источнике", "Primary", "Primary container", "Secondary", "Tertiary", "Error", "Outline" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_fill, "Заливка фигуры");
        Settings.Children.Add(_fill);

        _outline = Toggle("Только обводка", false);
        _thicknessPanel.Children.Add(_thicknessCaption);
        _thicknessCaption.Text = "Толщина обводки: 3 dp";
        _thickness = new Slider { Minimum = 1, Maximum = 8, Value = 3, TickFrequency = 1, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_thickness, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_thickness, false);
        AutomationProperties.SetName(_thickness, "Толщина обводки");
        _thicknessPanel.Children.Add(_thickness);
        Settings.Children.Add(_thicknessPanel);

        Settings.Children.Add(new TextBlock
        {
            Text = "MaterialShape — настоящая Avalonia Shape: Kind выбирает исходную геометрию, Stretch и " +
                   "локальная заливка переопределяют тему. Без явной заливки Shape Set рисуется ролью primary, " +
                   "а .Shape — primary-container, как в исходнике. Обводка снимает заливку и рисует контур secondary.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

        _family.SelectionChanged += (_, _) => { if (!_syncing) PopulateKinds(); };
        _kind.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _size.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _height.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };
        _stretch.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _fill.SelectionChanged += (_, _) => { if (!_syncing) Apply(); };
        _outline.IsCheckedChanged += (_, _) => { if (!_syncing) Apply(); };
        _thickness.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty && !_syncing) Apply(); };

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

    private MaterialButtonGroup Choice(string label, string[] options, int selected)
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
        Settings.Children.Add(panel);
        return group;
    }

    private ToggleSwitch Toggle(string label, bool value)
    {
        var toggle = new ToggleSwitch { Content = label, IsChecked = value };
        Settings.Children.Add(toggle);
        return toggle;
    }
}
