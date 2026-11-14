using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The two source content-block modes.</summary>
public enum MaterialContentBlockType
{
    /// <summary>Overline, label and supporting text.</summary>
    Text,
    /// <summary>A hosted slot component.</summary>
    Slot
}

/// <summary>
/// The content building block used inside list items, app bars and cards.
/// </summary>
/// <remarks>
/// This is a design-source building block, not an M3 composable; its type roles use the ported
/// Material typography.
/// <para>
/// Covers both source packages: Building blocks/Content (overline + label + supporting text,
/// overline visible by default) and .Building Blocks/Content (label + supporting text, no
/// overline). Set <see cref="ShowOverline"/> false and <see cref="ShowSupportingText"/> false for
/// the second package's two-line form. In <see cref="MaterialContentBlockType.Slot"/> mode the
/// inherited <see cref="ContentControl.Content"/> fills a <see cref="MaterialSlotHost"/>, which
/// shows its empty-slot prompt until content is assigned.
/// </para>
/// </remarks>
public sealed class MaterialContentBlock : ContentControl
{
    /// <summary>Text or slot mode.</summary>
    public static readonly StyledProperty<MaterialContentBlockType> TypeProperty =
        AvaloniaProperty.Register<MaterialContentBlock, MaterialContentBlockType>(nameof(Type));

    /// <summary>Overline text above the label.</summary>
    public static readonly StyledProperty<string?> OverlineProperty =
        AvaloniaProperty.Register<MaterialContentBlock, string?>(nameof(Overline));

    /// <summary>The primary line.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialContentBlock, string?>(nameof(Label));

    /// <summary>Supporting line below the label.</summary>
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialContentBlock, string?>(nameof(SupportingText));

    /// <summary>Shows the overline line.</summary>
    public static readonly StyledProperty<bool> ShowOverlineProperty =
        AvaloniaProperty.Register<MaterialContentBlock, bool>(nameof(ShowOverline), true);

    /// <summary>Shows the supporting line.</summary>
    public static readonly StyledProperty<bool> ShowSupportingTextProperty =
        AvaloniaProperty.Register<MaterialContentBlock, bool>(nameof(ShowSupportingText), true);

    private TextBlock? _overline;
    private TextBlock? _label;
    private TextBlock? _supporting;
    private StackPanel? _textStack;
    private MaterialSlotHost? _slot;

    static MaterialContentBlock()
    {
        TypeProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateMode());
        OverlineProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateLines());
        LabelProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateLines());
        SupportingTextProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateLines());
        ShowOverlineProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateLines());
        ShowSupportingTextProperty.Changed.AddClassHandler<MaterialContentBlock>(static (block, _) => block.UpdateLines());
    }

    public MaterialContentBlock()
    {
        Template = new FuncControlTemplate<MaterialContentBlock>((block, _) => block.Build());
    }

    public MaterialContentBlockType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public string? Overline { get => GetValue(OverlineProperty); set => SetValue(OverlineProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
    public bool ShowOverline { get => GetValue(ShowOverlineProperty); set => SetValue(ShowOverlineProperty, value); }
    public bool ShowSupportingText { get => GetValue(ShowSupportingTextProperty); set => SetValue(ShowSupportingTextProperty, value); }

    private Control Build()
    {
        _overline = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        MaterialTypography.SetStyle(_overline, MaterialTypeStyle.LabelSmall);
        _overline.Bind(TextBlock.ForegroundProperty, _overline.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        _label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        MaterialTypography.SetStyle(_label, MaterialTypeStyle.TitleMedium);
        _label.Bind(TextBlock.ForegroundProperty, _label.GetResourceObservable("MaterialOnSurfaceBrush"));
        AutomationProperties.SetHeadingLevel(_label, 3);

        _supporting = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        MaterialTypography.SetStyle(_supporting, MaterialTypeStyle.BodyMedium);
        _supporting.Bind(TextBlock.ForegroundProperty, _supporting.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        _textStack = new StackPanel();
        _textStack.Children.Add(_overline);
        _textStack.Children.Add(_label);
        _textStack.Children.Add(_supporting);

        _slot = new MaterialSlotHost();
        _slot[!ContentProperty] = this[!ContentProperty];
        _slot[!ContentTemplateProperty] = this[!ContentTemplateProperty];

        var root = new Grid();
        root.Children.Add(_textStack);
        root.Children.Add(_slot);
        UpdateLines();
        UpdateMode();
        return root;
    }

    private void UpdateMode()
    {
        var slot = Type == MaterialContentBlockType.Slot;
        if (_textStack is not null) _textStack.IsVisible = !slot;
        if (_slot is not null) _slot.IsVisible = slot;
    }

    private void UpdateLines()
    {
        if (_overline is null || _label is null || _supporting is null) return;
        _overline.Text = Overline;
        _overline.IsVisible = ShowOverline && !string.IsNullOrEmpty(Overline);
        _label.Text = Label;
        _label.IsVisible = !string.IsNullOrEmpty(Label);
        _supporting.Text = SupportingText;
        _supporting.IsVisible = ShowSupportingText && !string.IsNullOrEmpty(SupportingText);
        AutomationProperties.SetName(this, Label ?? Overline ?? "Content");
    }
}
