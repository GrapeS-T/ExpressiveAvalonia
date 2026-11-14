using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// A persistent Material bottom sheet scaffold: top bar, body, peek-height bottom sheet and
/// snackbar host, composed with the source's placement rules.
/// </summary>
/// <remarks>
/// <para>
/// Source mapping: <c>BottomSheetScaffold</c>, <c>BottomSheetScaffoldState</c> and
/// <c>StandardBottomSheet</c>. <see cref="TopBar"/> occupies the top row; <see cref="Content"/>
/// is measured below it and receives <see cref="ContentPadding"/>, whose bottom inset is
/// <see cref="SheetPeekHeight"/> exactly like the source's
/// <c>content(PaddingValues(bottom = sheetPeekHeight))</c>. <see cref="Sheet"/> is a standard
/// (non-modal) <see cref="MaterialBottomSheet"/> placed in the body area, so its partial anchor
/// shows the requested peek height. <see cref="SnackbarHost"/> sits above the partial sheet and
/// returns to the scaffold bottom while the sheet is expanded or hidden, matching the source's
/// <c>snackbarOffsetY</c>.
/// </para>
/// <para>
/// The sheet opens at the partial anchor when the scaffold joins a visual tree and keeps its
/// state across template re-application. It is not modal: the page stays interactive and no
/// scrim or focus trap is installed. The hidden anchor is skipped by default
/// (<see cref="SkipHiddenState"/>), as in
/// <c>rememberStandardBottomSheetState(skipHiddenState = true)</c>; a close request then
/// collapses the sheet to its partial anchor instead of removing it from the screen.
/// </para>
/// <para>
/// Desktop adaptation: Android window insets, predictive back and the coroutine state holder are
/// not simulated. State is the exposed <see cref="Sheet"/> instance and
/// <see cref="SnackbarHost"/>; callers control it directly instead of through a
/// <c>BottomSheetScaffoldState</c> wrapper.
/// </para>
/// </remarks>
public sealed class MaterialBottomSheetScaffold : ContentControl
{
    /// <summary>Identifies the <see cref="TopBar"/> property.</summary>
    public static readonly StyledProperty<object?> TopBarProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, object?>(nameof(TopBar));

    /// <summary>Identifies the <see cref="TopBarTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> TopBarTemplateProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, IDataTemplate?>(nameof(TopBarTemplate));

    /// <summary>Identifies the <see cref="SheetContent"/> property.</summary>
    public static readonly StyledProperty<object?> SheetContentProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, object?>(nameof(SheetContent));

    /// <summary>Identifies the <see cref="SheetContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> SheetContentTemplateProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, IDataTemplate?>(
            nameof(SheetContentTemplate));

    /// <summary>Identifies the <see cref="SheetPeekHeight"/> property.</summary>
    public static readonly StyledProperty<double> SheetPeekHeightProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, double>(
            nameof(SheetPeekHeight), 56d,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Identifies the <see cref="SkipHiddenState"/> property.</summary>
    public static readonly StyledProperty<bool> SkipHiddenStateProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, bool>(
            nameof(SkipHiddenState), true);

    /// <summary>Identifies the <see cref="SkipPartiallyExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> SkipPartiallyExpandedProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, bool>(
            nameof(SkipPartiallyExpanded));

    /// <summary>Identifies the <see cref="ShowDragHandle"/> property.</summary>
    public static readonly StyledProperty<bool> ShowDragHandleProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, bool>(nameof(ShowDragHandle), true);

    /// <summary>Identifies the <see cref="SheetMaxWidth"/> property.</summary>
    public static readonly StyledProperty<double> SheetMaxWidthProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, double>(
            nameof(SheetMaxWidth), 640d,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Identifies the <see cref="SheetSwipeEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> SheetSwipeEnabledProperty =
        AvaloniaProperty.Register<MaterialBottomSheetScaffold, bool>(
            nameof(SheetSwipeEnabled), true);

    /// <summary>Identifies the <see cref="ContentPadding"/> property.</summary>
    public static readonly DirectProperty<MaterialBottomSheetScaffold, Thickness>
        ContentPaddingProperty =
            AvaloniaProperty.RegisterDirect<MaterialBottomSheetScaffold, Thickness>(
                nameof(ContentPadding), static scaffold => scaffold._contentPadding);

    /// <summary>Identifies the <see cref="SnackbarBottomInset"/> property.</summary>
    public static readonly DirectProperty<MaterialBottomSheetScaffold, double>
        SnackbarBottomInsetProperty =
            AvaloniaProperty.RegisterDirect<MaterialBottomSheetScaffold, double>(
                nameof(SnackbarBottomInset), static scaffold => scaffold._snackbarBottomInset);

    private ContentControl? _topBar;
    private ContentPresenter? _sheetHost;
    private ContentPresenter? _snackbarHost;
    private Thickness _contentPadding;
    private double _snackbarBottomInset;
    private bool _openRequested;

    public MaterialBottomSheetScaffold()
    {
        Sheet = new MaterialBottomSheet();
        SnackbarHost = new MaterialSnackbarHost();
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Pane);

        _contentPadding = new Thickness(0, 0, 0, SheetPeekHeight);
        ApplySheetProperties();

        Sheet.PropertyChanged += OnSheetPropertyChanged;
    }

    /// <summary>
    /// The persistent bottom sheet. It is a standard <see cref="MaterialBottomSheet"/> whose
    /// <see cref="MaterialBottomSheet.SheetPeekHeight"/> follows the scaffold.
    /// </summary>
    public MaterialBottomSheet Sheet { get; }

    /// <summary>The scaffold snackbar host, already placed above the partial sheet.</summary>
    public MaterialSnackbarHost SnackbarHost { get; }

    /// <summary>The top app bar slot. Null removes the top row.</summary>
    public object? TopBar
    {
        get => GetValue(TopBarProperty);
        set => SetValue(TopBarProperty, value);
    }

    /// <summary>The template used to render <see cref="TopBar"/>.</summary>
    public IDataTemplate? TopBarTemplate
    {
        get => GetValue(TopBarTemplateProperty);
        set => SetValue(TopBarTemplateProperty, value);
    }

    /// <summary>The sheet content slot, forwarded to <see cref="Sheet"/>.</summary>
    public object? SheetContent
    {
        get => GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    /// <summary>The template used to render <see cref="SheetContent"/>.</summary>
    public IDataTemplate? SheetContentTemplate
    {
        get => GetValue(SheetContentTemplateProperty);
        set => SetValue(SheetContentTemplateProperty, value);
    }

    /// <summary>
    /// The visible sheet height at its partial anchor, in DIP. The source default is 56.
    /// </summary>
    public double SheetPeekHeight
    {
        get => GetValue(SheetPeekHeightProperty);
        set => SetValue(SheetPeekHeightProperty, value);
    }

    /// <summary>
    /// Omits the hidden anchor and makes close requests collapse to the partial anchor. The source
    /// persistent scaffold defaults to true.
    /// </summary>
    public bool SkipHiddenState
    {
        get => GetValue(SkipHiddenStateProperty);
        set => SetValue(SkipHiddenStateProperty, value);
    }

    /// <summary>Omits the partial anchor from the sheet; the source flag is off by default.</summary>
    public bool SkipPartiallyExpanded
    {
        get => GetValue(SkipPartiallyExpandedProperty);
        set => SetValue(SkipPartiallyExpandedProperty, value);
    }

    /// <summary>Shows the 32x4 drag handle in the sheet anatomy.</summary>
    public bool ShowDragHandle
    {
        get => GetValue(ShowDragHandleProperty);
        set => SetValue(ShowDragHandleProperty, value);
    }

    /// <summary>The maximum sheet width in DIP. The source default is 640.</summary>
    public double SheetMaxWidth
    {
        get => GetValue(SheetMaxWidthProperty);
        set => SetValue(SheetMaxWidthProperty, value);
    }

    /// <summary>
    /// Whether the sheet responds to drag and nested-scroll gestures. The drag handle stays
    /// keyboard-operable, as in the source where its clickable is outside the gesture gate.
    /// </summary>
    public bool SheetSwipeEnabled
    {
        get => GetValue(SheetSwipeEnabledProperty);
        set => SetValue(SheetSwipeEnabledProperty, value);
    }

    /// <summary>
    /// Body padding supplied by the scaffold. The bottom inset is <see cref="SheetPeekHeight"/>,
    /// matching the source's content padding for the collapsed sheet. The scaffold applies this
    /// value to the body presenter, so body content is not hidden behind the sheet.
    /// </summary>
    public Thickness ContentPadding => _contentPadding;

    /// <summary>
    /// Bottom inset applied to <see cref="SnackbarHost"/>: the peek height while the sheet is
    /// partially expanded, zero while it is expanded or hidden.
    /// </summary>
    public double SnackbarBottomInset => _snackbarBottomInset;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _topBar = e.NameScope.Find<ContentControl>("PART_TopBar");
        _sheetHost = e.NameScope.Find<ContentPresenter>("PART_SheetHost");
        _snackbarHost = e.NameScope.Find<ContentPresenter>("PART_SnackbarHost");

        if (_sheetHost is not null)
            _sheetHost.Content = Sheet;
        if (_snackbarHost is not null)
        {
            _snackbarHost.Content = SnackbarHost;
            _snackbarHost.Margin = new Thickness(0, 0, 0, _snackbarBottomInset);
        }

        UpdateTopBarVisibility();
        UpdateSnackbarInset();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The source standard scaffold starts at its partial anchor. Staging IsOpen before the
        // sheet template is applied lets its normal entrance handle layout and focus.
        if (_openRequested)
            return;

        _openRequested = true;
        Sheet.IsOpen = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TopBarProperty || change.Property == TopBarTemplateProperty)
            UpdateTopBarVisibility();
        else if (change.Property == SheetPeekHeightProperty)
        {
            UpdateContentPadding();
            ApplySheetProperties();
            UpdateSnackbarInset();
        }
        else if (change.Property == SkipHiddenStateProperty ||
                 change.Property == SkipPartiallyExpandedProperty ||
                 change.Property == ShowDragHandleProperty ||
                 change.Property == SheetMaxWidthProperty ||
                 change.Property == SheetSwipeEnabledProperty ||
                 change.Property == SheetContentProperty ||
                 change.Property == SheetContentTemplateProperty)
        {
            ApplySheetProperties();
        }
    }

    private void ApplySheetProperties()
    {
        Sheet.SheetPeekHeight = SheetPeekHeight;
        Sheet.SkipHiddenState = SkipHiddenState;
        Sheet.SkipPartiallyExpanded = SkipPartiallyExpanded;
        Sheet.ShowDragHandle = ShowDragHandle;
        Sheet.SheetMaxWidth = SheetMaxWidth;
        Sheet.AreGesturesEnabled = SheetSwipeEnabled;
        Sheet.Content = SheetContent;
        Sheet.ContentTemplate = SheetContentTemplate;
    }

    private void UpdateTopBarVisibility()
    {
        if (_topBar is not null)
            _topBar.IsVisible = TopBar is not null;
    }

    private void UpdateContentPadding()
    {
        var padding = new Thickness(0, 0, 0, SheetPeekHeight);
        if (padding == _contentPadding)
            return;
        var old = _contentPadding;
        _contentPadding = padding;
        RaisePropertyChanged(ContentPaddingProperty, old, padding);
    }

    private void OnSheetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MaterialSheet.CurrentValueProperty ||
            e.Property == MaterialSheet.IsOpenProperty ||
            e.Property == MaterialBottomSheet.SafeAreaPaddingProperty)
            UpdateSnackbarInset();
    }

    private void UpdateSnackbarInset()
    {
        var inset = Sheet.CurrentValue == MaterialSheetValue.PartiallyExpanded
            ? SheetPeekHeight + Math.Max(0, Sheet.SafeAreaPadding.Bottom)
            : 0d;

        if (Math.Abs(inset - _snackbarBottomInset) > 0.01)
        {
            var old = _snackbarBottomInset;
            _snackbarBottomInset = inset;
            RaisePropertyChanged(SnackbarBottomInsetProperty, old, inset);
        }

        if (_snackbarHost is not null)
            _snackbarHost.Margin = new Thickness(0, 0, 0, _snackbarBottomInset);
    }
}
