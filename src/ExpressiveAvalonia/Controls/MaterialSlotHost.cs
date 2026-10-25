using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The shared slot component: hosts arbitrary content, and while empty shows the source
/// replacement prompt inside its safety zone.
/// </summary>
/// <remarks>
/// Source anatomy (Shared Building Blocks/Slot-component): a 360x379 primary-container surface,
/// a dashed safety zone, a swap glyph, the "slot" caption and the replacement instructions.
/// The prompt is the component's real empty state, not a stand-in: assign <see cref="Content"/>
/// (optionally with a template) to fill the slot. <see cref="PlaceholderContent"/> replaces the
/// default prompt when an application wants different empty-slot guidance.
/// </remarks>
public sealed class MaterialSlotHost : ContentControl
{
    /// <summary>Caption shown while the slot is empty.</summary>
    public static readonly StyledProperty<string> SlotTitleProperty =
        AvaloniaProperty.Register<MaterialSlotHost, string>(nameof(SlotTitle), "slot");

    /// <summary>Instructions shown while the slot is empty.</summary>
    public static readonly StyledProperty<string> SlotDescriptionProperty =
        AvaloniaProperty.Register<MaterialSlotHost, string>(nameof(SlotDescription),
            "Replace this subcomponent in the variant properties with one you've built locally.");

    /// <summary>Replaces the built-in prompt while the slot is empty.</summary>
    public static readonly StyledProperty<object?> PlaceholderContentProperty =
        AvaloniaProperty.Register<MaterialSlotHost, object?>(nameof(PlaceholderContent));

    /// <summary>Corner radius of the slot surface.</summary>
    public static readonly StyledProperty<CornerRadius> SlotCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialSlotHost, CornerRadius>(nameof(SlotCornerRadius), new CornerRadius(16));

    private ContentPresenter? _contentHost;
    private Control? _placeholder;
    private ContentPresenter? _customPlaceholder;

    static MaterialSlotHost()
    {
        ContentProperty.Changed.AddClassHandler<MaterialSlotHost>(static (host, _) => host.UpdateFilled());
        PlaceholderContentProperty.Changed.AddClassHandler<MaterialSlotHost>(static (host, _) => host.UpdateFilled());
        SlotTitleProperty.Changed.AddClassHandler<MaterialSlotHost>(static (host, _) => host.UpdatePlaceholderText());
        SlotDescriptionProperty.Changed.AddClassHandler<MaterialSlotHost>(static (host, _) => host.UpdatePlaceholderText());
    }

    public MaterialSlotHost()
    {
        Template = new FuncControlTemplate<MaterialSlotHost>((host, _) => host.Build());
        AutomationProperties.SetName(this, "Content slot");
    }

    /// <summary>True when real content has been assigned.</summary>
    public bool IsFilled => Content is not null;

    public string SlotTitle { get => GetValue(SlotTitleProperty); set => SetValue(SlotTitleProperty, value); }
    public string SlotDescription { get => GetValue(SlotDescriptionProperty); set => SetValue(SlotDescriptionProperty, value); }
    public object? PlaceholderContent { get => GetValue(PlaceholderContentProperty); set => SetValue(PlaceholderContentProperty, value); }
    public CornerRadius SlotCornerRadius { get => GetValue(SlotCornerRadiusProperty); set => SetValue(SlotCornerRadiusProperty, value); }

    private Control Build()
    {
        _contentHost = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _contentHost[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        _contentHost[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];

        var icon = MaterialSourceIcons.Create(MaterialSourceIcons.Slot, 33, Brushes.Transparent);
        icon.Bind(Shape.FillProperty, this.GetResourceObservable("MaterialOnPrimaryContainerBrush"));

        var title = new TextBlock { Text = SlotTitle };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.LabelMedium);
        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable("MaterialOnPrimaryContainerBrush"));

        var description = new TextBlock
        {
            Text = SlotDescription,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 240
        };
        MaterialTypography.SetStyle(description, MaterialTypeStyle.BodySmall);
        description.Bind(TextBlock.ForegroundProperty, description.GetResourceObservable("MaterialOnPrimaryContainerBrush"));

        var prompt = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16)
        };
        prompt.Children.Add(icon);
        prompt.Children.Add(title);
        prompt.Children.Add(description);

        _customPlaceholder = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _customPlaceholder[!ContentPresenter.ContentProperty] = this[!PlaceholderContentProperty];

        var safetyZone = new Rectangle
        {
            StrokeThickness = 1,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 },
            RadiusX = 12,
            RadiusY = 12,
            Margin = new Thickness(8),
            IsHitTestVisible = false
        };
        safetyZone.Bind(Shape.StrokeProperty, safetyZone.GetResourceObservable("MaterialOnPrimaryContainerBrush"));

        var layer = new Grid();
        layer.Children.Add(_contentHost);
        layer.Children.Add(safetyZone);
        layer.Children.Add(prompt);
        layer.Children.Add(_customPlaceholder);
        _placeholder = prompt;

        var surface = new Border
        {
            CornerRadius = SlotCornerRadius,
            Padding = new Thickness(8),
            Child = layer
        };
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("MaterialPrimaryContainerBrush"));

        UpdateFilled();
        UpdatePlaceholderText();
        return surface;
    }

    private void UpdateFilled()
    {
        var filled = IsFilled;
        if (_contentHost is not null) _contentHost.IsVisible = filled;
        var showDefault = !filled && PlaceholderContent is null;
        if (_placeholder is not null) _placeholder.IsVisible = showDefault;
        if (_customPlaceholder is not null) _customPlaceholder.IsVisible = !filled && PlaceholderContent is not null;
        AutomationProperties.SetHelpText(this, filled ? string.Empty : SlotDescription);
    }

    private void UpdatePlaceholderText()
    {
        if (_placeholder is not StackPanel panel) return;
        if (panel.Children.Count >= 3 &&
            panel.Children[1] is TextBlock title && panel.Children[2] is TextBlock description)
        {
            title.Text = SlotTitle;
            description.Text = SlotDescription;
        }
    }
}
