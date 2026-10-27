using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>Event data for <see cref="MaterialSideSheet.BackRequested"/>.</summary>
public sealed class SheetBackRequestedEventArgs : System.EventArgs
{
    /// <summary>Set to true when the request was handled without closing the sheet.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// A Material-styled side sheet docked to the leading or trailing edge.
/// </summary>
/// <remarks>
/// <para>
/// This is an ExpressiveAvalonia extension rather than a port: the pinned Material 3 snapshot has
/// no side-sheet composable. It uses the shared <see cref="MaterialSheet"/> state machine (open,
/// hidden, dismiss veto, focus restore and spring motion) with the design-source anatomy: a
/// 320-wide panel with a 400 maximum, a 76-tall header with optional back affordance, a Title
/// Large headline, a close affordance, the scrolling content slot and an actions area above a
/// 1-pixel divider.
/// </para>
/// <para>
/// <see cref="MaterialSheet.IsModal"/> selects the modal presentation (scrim, 16 rounded
/// container, focus trap) or the standard presentation (no scrim, square corners, page-level
/// focus). The close affordance and the close command always close the sheet;
/// <see cref="MaterialSheet.IsDismissEnabled"/> only governs Escape and scrim taps.
/// </para>
/// </remarks>
public sealed class MaterialSideSheet : MaterialSheet
{
    /// <summary>Identifies the <see cref="Headline"/> property.</summary>
    public static readonly StyledProperty<string?> HeadlineProperty =
        AvaloniaProperty.Register<MaterialSideSheet, string?>(nameof(Headline), "Title");

    /// <summary>Identifies the <see cref="ShowBackButton"/> property.</summary>
    public static readonly StyledProperty<bool> ShowBackButtonProperty =
        AvaloniaProperty.Register<MaterialSideSheet, bool>(nameof(ShowBackButton));

    /// <summary>Identifies the <see cref="ShowCloseButton"/> property.</summary>
    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<MaterialSideSheet, bool>(nameof(ShowCloseButton), true);

    /// <summary>Identifies the <see cref="BackCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<MaterialSideSheet, ICommand?>(nameof(BackCommand));

    /// <summary>Identifies the <see cref="CloseCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<MaterialSideSheet, ICommand?>(nameof(CloseCommand));

    /// <summary>Identifies the <see cref="Side"/> property.</summary>
    public static readonly StyledProperty<MaterialSheetSide> SideProperty =
        AvaloniaProperty.Register<MaterialSideSheet, MaterialSheetSide>(nameof(Side), MaterialSheetSide.End);

    /// <summary>Identifies the <see cref="SheetWidth"/> property.</summary>
    public static readonly StyledProperty<double> SheetWidthProperty =
        AvaloniaProperty.Register<MaterialSideSheet, double>(nameof(SheetWidth), 320d,
            validate: static value => double.IsFinite(value) && value > 0);

    private Grid? _header;
    private Button? _backButton;
    private Button? _closeButton;
    private TextBlock? _headline;

    static MaterialSideSheet()
    {
        ShowActionsProperty.OverrideDefaultValue<MaterialSideSheet>(true);
    }

    /// <summary>Raised when the back affordance is used.</summary>
    public event System.EventHandler<SheetBackRequestedEventArgs>? BackRequested;

    /// <summary>The localized headline. The default "Title" mirrors the design-source default.</summary>
    public string? Headline
    {
        get => GetValue(HeadlineProperty);
        set => SetValue(HeadlineProperty, value);
    }

    /// <summary>Shows the leading back affordance, which shifts the headline by 20 units.</summary>
    public bool ShowBackButton
    {
        get => GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    /// <summary>Shows the trailing close affordance.</summary>
    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>Executed by the back affordance before <see cref="BackRequested"/> is considered handled.</summary>
    public ICommand? BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    /// <summary>Executed by the close affordance before the sheet closes.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>The logical edge the sheet docks to. Respects right-to-left flow direction.</summary>
    public MaterialSheetSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>The surface width. Material uses 320 with a 400 maximum.</summary>
    public double SheetWidth
    {
        get => GetValue(SheetWidthProperty);
        set => SetValue(SheetWidthProperty, value);
    }

    private bool IsSurfaceOnRight =>
        Side == MaterialSheetSide.End
            ? FlowDirection != FlowDirection.RightToLeft
            : FlowDirection == FlowDirection.RightToLeft;

    protected override bool CanUseValue(MaterialSheetValue value) =>
        value != MaterialSheetValue.PartiallyExpanded;

    protected override string? GetSheetAccessibleName() => Headline;

    protected override double GetPosition(MaterialSheetValue value)
    {
        if (value == MaterialSheetValue.Hidden)
        {
            var width = SheetSurface?.Bounds.Width ?? 0;
            return width > 0 ? width : SheetWidth;
        }

        return 0;
    }

    protected override void ApplyPosition(double position)
    {
        if (SheetTransform is null)
            return;

        // The offset is always a positive travel distance; only the sign depends on the edge.
        SheetTransform.X = IsSurfaceOnRight ? position : -position;
        SheetTransform.Y = 0;
    }

    protected override void OnSheetChromeChanged() => UpdateSideLayout();

    protected override void OnSheetTemplateApplied(TemplateAppliedEventArgs e)
    {
        if (_backButton is not null)
            _backButton.Click -= OnBackClick;
        if (_closeButton is not null)
            _closeButton.Click -= OnCloseClick;

        _header = e.NameScope.Find<Grid>("PART_Header");
        _backButton = e.NameScope.Find<Button>("PART_BackButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        _headline = e.NameScope.Find<TextBlock>("PART_Headline");

        if (SheetSurface is not null)
        {
            var slide = new TranslateTransform();
            SheetTransform = slide;
            SheetSurface.RenderTransform = slide;
        }

        if (_backButton is not null)
            _backButton.Click += OnBackClick;
        if (_closeButton is not null)
            _closeButton.Click += OnCloseClick;
        if (_headline is not null)
            AutomationProperties.SetHeadingLevel(_headline, 2);

        UpdateHeaderLayout();
        UpdateSideLayout();
        UpdateAccessibleName();
        ApplyPosition(0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowBackButtonProperty)
            UpdateHeaderLayout();
        else if (change.Property == SideProperty || change.Property == FlowDirectionProperty)
            UpdateSideLayout();
        else if (change.Property == HeadlineProperty)
            UpdateAccessibleName();
    }

    private void UpdateHeaderLayout()
    {
        if (_header is null)
            return;

        // Header: 12 top, 12 trailing, 16 bottom and 24 leading; the back affordance replaces
        // the leading 24 with 4 (48-wide target plus the headline's 12 inset).
        _header.Margin = new Thickness(ShowBackButton ? 4 : 24, 12, 12, 16);
    }

    private void UpdateSideLayout()
    {
        if (SheetSurface is null)
            return;

        var right = IsSurfaceOnRight;
        SheetSurface.HorizontalAlignment = right
            ? Avalonia.Layout.HorizontalAlignment.Right
            : Avalonia.Layout.HorizontalAlignment.Left;
        SheetSurface.BorderThickness = right ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);

        // The design docks both presentations full height; only the modal container is rounded (16).
        SheetSurface.CornerRadius = new CornerRadius(IsModal ? 16 : 0);

        // The translation direction and closed distance depend on the edge and on the width the
        // surface actually received. The base class reconciles the current state after layout.
        ApplyPosition(SheetPosition);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (CloseCommand is { } command && command.CanExecute(null))
            command.Execute(null);

        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        var args = new SheetBackRequestedEventArgs();
        BackRequested?.Invoke(this, args);
        if (args.Handled)
            return;

        if (BackCommand is { } command)
        {
            if (command.CanExecute(null))
                command.Execute(null);
            return;
        }

        // A back affordance without navigation closes the sheet.
        SetCurrentValue(IsOpenProperty, false);
    }
}