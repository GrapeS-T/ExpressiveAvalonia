using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>Where a search view is presented.</summary>
public enum MaterialSearchViewMode
{
    /// <summary>A floating card anchored in page content: field and suggestion card, 2 DIP apart.</summary>
    Docked,

    /// <summary>A full-bleed surface with an optional scrim behind it.</summary>
    FullScreen,
}

/// <summary>Which of the two source layouts the view reproduces.</summary>
public enum MaterialSearchViewVariant
{
    /// <summary>Separate field and suggestion cards (radius 28 and 12) on a transparent root.</summary>
    Expressive,

    /// <summary>One rounded container: flat header, 1 DIP divider and a flush suggestion list.</summary>
    Baseline,
}

/// <summary>Whether the header shows an active query or the resting hint.</summary>
public enum MaterialSearchFieldConfiguration
{
    /// <summary>Active editing: query text with a caret and the voice action.</summary>
    InputText,

    /// <summary>Resting hint: placeholder text only, no voice action.</summary>
    SupportingText,
}

/// <summary>How a search view was dismissed.</summary>
public enum MaterialSearchDismissReason
{
    /// <summary>Escape with an empty query.</summary>
    Escape,

    /// <summary>The leading back arrow.</summary>
    Back,

    /// <summary>The trailing close action.</summary>
    Close,

    /// <summary>A press on the full-screen scrim.</summary>
    Scrim,

    /// <summary>A press outside a docked view, mirroring the source popup's light dismiss.</summary>
    Outside,

    /// <summary>Dismissed from code.</summary>
    Programmatic,
}

/// <summary>Carries the reason reported by <see cref="MaterialSearchView.Dismissed"/>.</summary>
public sealed class MaterialSearchDismissedEventArgs : EventArgs
{
    public MaterialSearchDismissedEventArgs(MaterialSearchDismissReason reason) => Reason = reason;

    /// <summary>What dismissed the view.</summary>
    public MaterialSearchDismissReason Reason { get; }
}

/// <summary>Carries the activated suggestion reported by <see cref="MaterialSearchView.SuggestionActivated"/>.</summary>
public sealed class MaterialSearchSuggestionEventArgs : EventArgs
{
    public MaterialSearchSuggestionEventArgs(object? suggestion) => Suggestion = suggestion;

    /// <summary>The activated item, as supplied through <see cref="MaterialSearchView.Suggestions"/>.</summary>
    public object? Suggestion { get; }
}

/// <summary>
/// The Material 3 search view: a search field plus a suggestion list, docked or full screen,
/// in the expressive (two-card) or baseline (single-container) layout.
/// </summary>
/// <remarks>
/// <para>
/// The view owns the search session: it focuses the field when it opens, moves focus into the
/// suggestion list with Down, activates the highlighted suggestion with Enter, returns to the
/// field with Up/Escape, and dismisses on Escape (once the query is empty), on the leading back
/// arrow, on the trailing close action and on a scrim press. <see cref="Dismiss"/> hides the view
/// through the <c>:closed</c> pseudo class and restores the focus that was active before it opened.
/// </para>
/// <para>
/// Suggestions are whatever <see cref="Suggestions"/> contains; the view does not invent a
/// matching algorithm, so callers filter their own collection from <see cref="QueryChanged"/>.
/// Activating a suggestion sets <see cref="Text"/> when the item is a string and always raises
/// <see cref="SuggestionActivated"/> so object items can be handled by the caller.
/// </para>
/// <para>
/// Source mapping: <see cref="Mode"/> is the docked versus full-screen package,
/// <see cref="Variant"/> the expressive versus baseline package, and
/// <see cref="Configuration"/> the "Input text" versus "Supporting text" axis, which is what
/// shows the voice action in the header.
/// </para>
/// </remarks>
[TemplatePart("PART_Root", typeof(Border))]
[TemplatePart("PART_Header", typeof(Border))]
[TemplatePart("PART_SearchBar", typeof(MaterialSearchBar))]
[TemplatePart("PART_Suggestions", typeof(ListBox))]
[TemplatePart("PART_SuggestionsHost", typeof(Border))]
[TemplatePart("PART_Divider", typeof(Border))]
[TemplatePart("PART_Scrim", typeof(Border))]
[PseudoClasses(":docked", ":full-screen", ":baseline", ":supporting-text", ":closed", ":scrim")]
public class MaterialSearchView : TemplatedControl
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialSearchView, string?>(nameof(Text), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="PlaceholderText"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<MaterialSearchView, string?>(nameof(PlaceholderText));

    /// <summary>Defines the <see cref="FieldBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FieldBackgroundProperty =
        AvaloniaProperty.Register<MaterialSearchView, IBrush?>(nameof(FieldBackground));

    /// <summary>Defines the <see cref="FieldCornerRadius"/> property.</summary>
    public static readonly StyledProperty<CornerRadius> FieldCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialSearchView, CornerRadius>(nameof(FieldCornerRadius));

    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchViewMode> ModeProperty =
        AvaloniaProperty.Register<MaterialSearchView, MaterialSearchViewMode>(nameof(Mode));

    /// <summary>Defines the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchViewVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialSearchView, MaterialSearchViewVariant>(nameof(Variant));

    /// <summary>Defines the <see cref="Configuration"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchFieldConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialSearchView, MaterialSearchFieldConfiguration>(nameof(Configuration));

    /// <summary>Defines the <see cref="ShowSuggestions"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSuggestionsProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(ShowSuggestions), true);

    /// <summary>Defines the <see cref="ShowTrailingIcon"/> property.</summary>
    public static readonly StyledProperty<bool> ShowTrailingIconProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(ShowTrailingIcon), true);

    /// <summary>Defines the <see cref="ShowScrim"/> property.</summary>
    public static readonly StyledProperty<bool> ShowScrimProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(ShowScrim), true);

    /// <summary>Defines the <see cref="IsOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(IsOpen), true);

    /// <summary>Defines the <see cref="AutoFocus"/> property.</summary>
    public static readonly StyledProperty<bool> AutoFocusProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(AutoFocus), true);

    /// <summary>Defines the <see cref="HideAnchorWhenExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> HideAnchorWhenExpandedProperty =
        AvaloniaProperty.Register<MaterialSearchView, bool>(nameof(HideAnchorWhenExpanded), true);

    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchView, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchView, bool>(
            nameof(IsExpanded), view => view.IsExpanded);

    /// <summary>Defines the <see cref="IsExpansionTargetExpanded"/> property.</summary>
    public static readonly DirectProperty<MaterialSearchView, bool> IsExpansionTargetExpandedProperty =
        AvaloniaProperty.RegisterDirect<MaterialSearchView, bool>(
            nameof(IsExpansionTargetExpanded), view => view.IsExpansionTargetExpanded);

    /// <summary>
    /// Defines the control whose bounds seed the expansion geometry. When set, the open and close
    /// transitions scale and translate the surface from those bounds (source animated bounds);
    /// unset transitions fade and slide in place.
    /// </summary>
    public static readonly StyledProperty<Control?> ExpansionAnchorProperty =
        AvaloniaProperty.Register<MaterialSearchView, Control?>(nameof(ExpansionAnchor));

    /// <summary>Defines the <see cref="Suggestions"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> SuggestionsProperty =
        AvaloniaProperty.Register<MaterialSearchView, IEnumerable?>(nameof(Suggestions));

    /// <summary>Defines the <see cref="ItemTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<MaterialSearchView, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Defines the <see cref="SelectedSuggestion"/> property.</summary>
    public static readonly StyledProperty<object?> SelectedSuggestionProperty =
        AvaloniaProperty.Register<MaterialSearchView, object?>(nameof(SelectedSuggestion),
            defaultBindingMode: BindingMode.TwoWay);

    private MaterialSearchBar? _searchBar;
    private ListBox? _suggestions;
    private Border? _suggestionsHost;
    private Border? _scrim;
    private Border? _divider;
    private Border? _root;
    private MaterialMotion.SpringAnimation? _expansionSpring;
    private MaterialMotion.SpringAnimation? _slideSpring;
    private MaterialMotion.TweenAnimation? _contentTween;
    private double _expansionProgress;
    private double _contentProgress;
    private double _slideProgress;
    private double _slideTarget;
    private bool _closing;
    private CornerRadius _expandedCornerRadius;
    private ItemsSourceView? _itemsView;
    private IInputElement? _previousFocus;
    private MaterialSearchBar? _anchorBar;
    private TopLevel? _lightDismissRoot;
    private int _expansionGeneration;
    private bool _anchorWasVisible = true;
    private bool _hideAnchorWhenExpanded;
    private bool _anchorVisibilityChanged;
    private double _collapsedCornerRadius = 28;
    private string? _lastSyncedText;
    private bool _syncingInput;
    private bool _isExpanded = true;
    private bool _isExpansionTargetExpanded = true;

    /// <summary>Raised when the query is confirmed with Enter or the search action.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Raised after the query has been cleared.</summary>
    public event EventHandler? ClearRequested;

    /// <summary>Raised whenever <see cref="Text"/> changes.</summary>
    public event EventHandler? QueryChanged;

    /// <summary>Raised when the voice action is activated.</summary>
    public event EventHandler? VoiceInputRequested;

    /// <summary>Raised when the view is dismissed, with the reason.</summary>
    public event EventHandler<MaterialSearchDismissedEventArgs>? Dismissed;

    /// <summary>Raised when a suggestion is activated by Enter or a click.</summary>
    public event EventHandler<MaterialSearchSuggestionEventArgs>? SuggestionActivated;

    public MaterialSearchView() => UpdatePseudoClasses();

    /// <summary>The query. Bound two-way by default.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The hint shown while the query is empty.</summary>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>
    /// Background of the header field. The theme uses the Surface container high role; the
    /// baseline layout sets it transparent so the root container shows through, and the source's
    /// Flat and On-scroll building blocks override it with their own container colors.
    /// </summary>
    public IBrush? FieldBackground
    {
        get => GetValue(FieldBackgroundProperty);
        set => SetValue(FieldBackgroundProperty, value);
    }

    /// <summary>
    /// Corner radius of the header field. The theme uses the source pill radius; the baseline
    /// layout flattens it to zero because its field is part of the single container.
    /// </summary>
    public CornerRadius FieldCornerRadius
    {
        get => GetValue(FieldCornerRadiusProperty);
        set => SetValue(FieldCornerRadiusProperty, value);
    }

    /// <summary>Docked or full screen.</summary>
    public MaterialSearchViewMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Expressive (two cards) or baseline (one container) layout.</summary>
    public MaterialSearchViewVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Active query presentation or the resting hint presentation.</summary>
    public MaterialSearchFieldConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    /// <summary>Shows the suggestion card. Defaults to true.</summary>
    public bool ShowSuggestions
    {
        get => GetValue(ShowSuggestionsProperty);
        set => SetValue(ShowSuggestionsProperty, value);
    }

    /// <summary>
    /// Shows the trailing close action, which dismisses the view with
    /// <see cref="MaterialSearchDismissReason.Close"/>. Defaults to true. The voice action is
    /// independent: the source shows it only while <see cref="Configuration"/> is
    /// <see cref="MaterialSearchFieldConfiguration.InputText"/>.
    /// </summary>
    public bool ShowTrailingIcon
    {
        get => GetValue(ShowTrailingIconProperty);
        set => SetValue(ShowTrailingIconProperty, value);
    }

    /// <summary>Shows the scrim behind a full-screen view. Defaults to true.</summary>
    public bool ShowScrim
    {
        get => GetValue(ShowScrimProperty);
        set => SetValue(ShowScrimProperty, value);
    }

    /// <summary>Whether the view is presented. Setting it false collapses the view.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Focuses the field when the view opens or is attached. Defaults to true.</summary>
    public bool AutoFocus
    {
        get => GetValue(AutoFocusProperty);
        set => SetValue(AutoFocusProperty, value);
    }

    /// <summary>
    /// Whether the view is currently expanded, ported from <c>SearchBarState.currentValue</c>: the
    /// expansion counts as expanded while the container progress is above 0.02, so it stays
    /// expanded through the closing animation and only collapses once the surface has reached its
    /// collapsed geometry. The search app bar uses it to keep the collapsed field hidden while the
    /// full-screen surface collapses.
    /// </summary>
    public bool IsExpanded => _isExpanded;

    /// <summary>
    /// Whether the expansion target is Expanded, the source's <c>SearchBarState.targetValue</c>. It
    /// flips to false as soon as a collapse starts and to true as soon as an expansion starts.
    /// </summary>
    public bool IsExpansionTargetExpanded => _isExpansionTargetExpanded;

    /// <summary>
    /// Optional source bounds for the expansion. Point this at the collapsed search field so the
    /// surface grows from (and collapses back to) that rectangle, including after resize/scroll.
    /// </summary>
    public Control? ExpansionAnchor
    {
        get => GetValue(ExpansionAnchorProperty);
        set => SetValue(ExpansionAnchorProperty, value);
    }

    /// <summary>
    /// Whether the view hides <see cref="ExpansionAnchor"/> once it is fully expanded. Defaults to
    /// true, which suits a standalone bar whose expanded surface renders its own field. The
    /// <see cref="MaterialSearchAppBar"/> composition sets it false because the app bar keeps its
    /// collapsed field in the layout and controls its reveal with the source's opacity rule.
    /// </summary>
    public bool HideAnchorWhenExpanded
    {
        get => GetValue(HideAnchorWhenExpandedProperty);
        set => SetValue(HideAnchorWhenExpandedProperty, value);
    }

    /// <summary>The suggestion items. Filtering is the caller's responsibility.</summary>
    public IEnumerable? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>Template for one suggestion; the item container supplies padding, radius and states.</summary>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The highlighted suggestion, kept in sync with the list selection.</summary>
    public object? SelectedSuggestion
    {
        get => GetValue(SelectedSuggestionProperty);
        set => SetValue(SelectedSuggestionProperty, value);
    }

    /// <summary>Presents the view and, when <see cref="AutoFocus"/> is set, focuses the field.</summary>
    public void Open()
    {
        // Seeding happens in OnPropertyChanged so a bound IsOpen follows the same path. The
        // header field is focused after the expansion delay has been posted.
        SetCurrentValue(IsOpenProperty, true);
        UpdateAutoFocus();
    }

    /// <summary>Hides the view, raises <see cref="Dismissed"/> and restores the previous focus.</summary>
    public void Dismiss(MaterialSearchDismissReason reason = MaterialSearchDismissReason.Programmatic)
    {
        if (!IsOpen)
            return;
        var hadFocus = IsKeyboardFocusWithin;
        SetCurrentValue(IsOpenProperty, false);
        Dismissed?.Invoke(this, new MaterialSearchDismissedEventArgs(reason));
        if (reason == MaterialSearchDismissReason.Outside)
            return; // The pressed control owns focus; do not steal it back.
        if (hadFocus || (AutoFocus && reason != MaterialSearchDismissReason.Scrim))
        {
            if (_anchorBar is not null)
            {
                // Touch-mode-equivalent policy: ClearFocusOnCollapse leaves focus cleared.
                if (!_anchorBar.ClearFocusOnCollapse)
                    _anchorBar.FocusInput();
            }
            else
            {
                RestoreFocus();
            }
        }
    }

    /// <summary>Moves keyboard focus into the field.</summary>
    public void FocusInput() => _searchBar?.FocusInput();

    /// <summary>Clears the query and raises <see cref="ClearRequested"/>.</summary>
    public void Clear()
    {
        if (string.IsNullOrEmpty(Text))
            return;
        SetCurrentValue(TextProperty, string.Empty);
        _searchBar?.FocusInput();
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Confirms the query, raising <see cref="SearchRequested"/>.</summary>
    public void Submit() => SearchRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Activates the highlighted suggestion.</summary>
    public void ActivateSelectedSuggestion()
    {
        var item = SelectedSuggestion ?? _suggestions?.SelectedItem;
        if (item is null)
            return;
        if (item is string text)
            SetCurrentValue(TextProperty, text);
        SuggestionActivated?.Invoke(this, new MaterialSearchSuggestionEventArgs(item));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachParts();

        _searchBar = e.NameScope.Find<MaterialSearchBar>("PART_SearchBar");
        _suggestions = e.NameScope.Find<ListBox>("PART_Suggestions");
        _suggestionsHost = e.NameScope.Find<Border>("PART_SuggestionsHost");
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _divider = e.NameScope.Find<Border>("PART_Divider");
        _root = e.NameScope.Find<Border>("PART_Root");

        if (_searchBar is not null)
        {
            _searchBar.SearchRequested += OnBarSearchRequested;
            _searchBar.ClearRequested += OnBarClearRequested;
            _searchBar.DismissRequested += OnBarDismissRequested;
            _searchBar.LeadingIconInvoked += OnBarLeadingInvoked;
            _searchBar.TrailingIconInvoked += OnBarTrailingInvoked;
            _searchBar.SecondaryIconInvoked += OnBarSecondaryInvoked;
            _searchBar.AddHandler(KeyDownEvent, OnBarPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        if (_suggestions is not null)
        {
            _suggestions.AddHandler(KeyDownEvent, OnSuggestionsKeyDown, RoutingStrategies.Tunnel);
            _suggestions.SelectionChanged += OnSuggestionSelectionChanged;
            _suggestions.Tapped += OnSuggestionTapped;
        }

        if (_scrim is not null)
            _scrim.PointerPressed += OnScrimPressed;

        AttachItemsView();
        UpdateConfiguration();
        UpdateSuggestionsVisibility();
        UpdateAutoFocus();
        HookAnchorBar();
        ApplyInitialExpansionState();
        if (IsOpen)
            SeedHeaderSelectionFromAnchor();
    }

    private void ApplyInitialExpansionState()
    {
        if (_root is null)
            return;
        IsVisible = IsOpen;
        _expansionProgress = IsOpen ? 1 : 0;
        _contentProgress = IsOpen ? 1 : 0;
        UpdateExpansionState(IsOpen);
        _slideProgress = IsOpen && IsGapLayout ? 1 : 0;
        _slideTarget = _slideProgress;
        _root.Opacity = IsOpen ? 1 : 0;
        _root.RenderTransform = null;
        if (IsOpen)
        {
            _expandedCornerRadius = _root.CornerRadius;
            _collapsedCornerRadius = ResolveCollapsedCornerRadius();
        }

        if (_suggestionsHost is not null)
            _suggestionsHost.RenderTransform = null;
        ApplyContentOpacity();
    }

    /// <summary>
    /// Publishes the source's current/target expansion pair. The current value follows the same
    /// 0.02 tolerance as <c>SearchBarState.currentValue</c>, so it remains Expanded until the
    /// closing surface has essentially reached its collapsed geometry.
    /// </summary>
    private void UpdateExpansionState(bool? target = null)
    {
        if (target is { } targetExpanded && _isExpansionTargetExpanded != targetExpanded)
        {
            var oldTarget = _isExpansionTargetExpanded;
            _isExpansionTargetExpanded = targetExpanded;
            RaisePropertyChanged(IsExpansionTargetExpandedProperty, oldTarget, targetExpanded);
        }

        var expanded = _expansionProgress > 0.02;
        if (_isExpanded == expanded)
            return;
        var old = _isExpanded;
        _isExpanded = expanded;
        RaisePropertyChanged(IsExpandedProperty, old, expanded);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is Visual visual && !visual.GetVisualAncestors().Contains(this))
            _previousFocus = focused;

        HookAnchorBar();
        if (IsOpen)
            AttachLightDismiss();
        UpdateAutoFocus();
        // Template bindings for ItemsSource can settle after OnApplyTemplate; refresh the host
        // visibility and the collection subscription once the tree is loaded.
        Dispatcher.UIThread.Post(
            () =>
            {
                AttachItemsView();
                UpdateSuggestionsVisibility();
            },
            DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _expansionGeneration++;
        _expansionSpring?.Dispose();
        _expansionSpring = null;
        _slideSpring?.Dispose();
        _slideSpring = null;
        _contentTween?.Dispose();
        _contentTween = null;
        _closing = false;

        // A detached view cannot finish its springs: put the dropdown back at its natural offset
        // and, when the view is already closed, drop the frozen halfway expansion.
        _slideProgress = IsOpen && IsGapLayout ? 1 : 0;
        _slideTarget = _slideProgress;
        if (_suggestionsHost is not null)
            _suggestionsHost.RenderTransform = null;
        if (!IsOpen && _root is not null)
            FinishExpansion(_root);
        else if (_root is not null)
        {
            // Keep an open view in its final visual state so a reparented view is not stuck at a
            // half-expanded scale. The anchor visibility is restored below.
            _expansionProgress = 1;
            _contentProgress = 1;
            UpdateExpansionState();
            _root.Opacity = 1;
            _root.RenderTransform = null;
            _root.CornerRadius = _expandedCornerRadius;
            if (_suggestionsHost is not null)
                _suggestionsHost.Opacity = 1;
            if (_scrim is not null)
                _scrim.Opacity = 0.32;
        }

        DetachLightDismiss();
        if (ExpansionAnchor is { } anchor && _anchorVisibilityChanged)
        {
            anchor.IsVisible = _anchorWasVisible;
            _anchorVisibilityChanged = false;
        }
        if (_anchorBar is not null)
        {
            _anchorBar.PropertyChanged -= OnAnchorBarPropertyChanged;
            _anchorBar = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModeProperty || change.Property == VariantProperty ||
            change.Property == ConfigurationProperty || change.Property == IsOpenProperty ||
            change.Property == ShowScrimProperty || change.Property == ShowTrailingIconProperty)
        {
            UpdatePseudoClasses();
            if (change.Property == ConfigurationProperty || change.Property == ShowTrailingIconProperty)
                UpdateConfiguration();
            if (change.Property == VariantProperty || change.Property == ModeProperty)
                UpdateContentEffectMode();
            if (change.Property == ModeProperty && IsOpen)
            {
                if (Mode == MaterialSearchViewMode.Docked)
                    AttachLightDismiss();
                else
                    DetachLightDismiss();
            }
            if (change.Property == IsOpenProperty)
            {
                if (change.GetNewValue<bool>())
                {
                    AdoptAnchorInputState();
                    AttachLightDismiss();
                }
                else
                {
                    DetachLightDismiss();
                    PushInputStateToAnchor();
                }

                AnimateExpansion(change.GetNewValue<bool>());
                UpdateAutoFocus();
            }
        }
        else if (change.Property == TextProperty)
        {
            QueryChanged?.Invoke(this, EventArgs.Empty);
            if (IsOpen && !_syncingInput && _anchorBar is not null)
            {
                // The source passes one TextFieldState to the collapsed bar and the expanded
                // view; keep the owner's query in step with edits made in the header.
                _syncingInput = true;
                _anchorBar.SetCurrentValue(MaterialSearchBar.TextProperty, Text);
                _lastSyncedText = Text;
                _syncingInput = false;
            }
        }
        else if (change.Property == SuggestionsProperty)
        {
            AttachItemsView();
            UpdateSuggestionsVisibility();
        }
        else if (change.Property == ShowSuggestionsProperty)
        {
            UpdateSuggestionsVisibility();
        }
        else if (change.Property == ExpansionAnchorProperty)
        {
            if (change.GetOldValue<Control?>() is { } oldAnchor &&
                !ReferenceEquals(oldAnchor, ExpansionAnchor) && _anchorVisibilityChanged)
            {
                oldAnchor.IsVisible = _anchorWasVisible;
                _anchorVisibilityChanged = false;
            }
            HookAnchorBar();
            if (!IsOpen)
            {
                _anchorWasVisible = ExpansionAnchor?.IsVisible ?? true;
                _anchorVisibilityChanged = false;
            }
        }
        else if (change.Property == AutoFocusProperty)
        {
            UpdateAutoFocus();
        }
    }

    private void HookAnchorBar()
    {
        var bar = ExpansionAnchor as MaterialSearchBar;
        if (ReferenceEquals(bar, _anchorBar))
            return;
        if (_anchorBar is not null)
            _anchorBar.PropertyChanged -= OnAnchorBarPropertyChanged;
        _anchorBar = bar;
        if (_anchorBar is not null)
        {
            _anchorBar.PropertyChanged += OnAnchorBarPropertyChanged;
            // The view may already be open when the composition supplies its anchor. There is
            // no IsOpen transition in that case, but both fields still share the same query.
            if (IsOpen)
                AdoptAnchorInputState();
        }
    }

    private void OnAnchorBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!IsOpen || _syncingInput || _anchorBar is null)
            return;
        if (e.Property == MaterialSearchBar.TextProperty)
        {
            // The owner edited the collapsed query while the view is open; adopt it unless the
            // view itself is the source of the change.
            _syncingInput = true;
            SetCurrentValue(TextProperty, _anchorBar.Text);
            _lastSyncedText = Text;
            _syncingInput = false;
        }
    }

    /// <summary>
    /// Copies the collapsed bar's query and caret/selection into the view. Source: the collapsed
    /// bar and the expanded view share one TextFieldState.
    /// </summary>
    private void AdoptAnchorInputState()
    {
        if (_anchorBar is null)
            return;

        var replaceWithCallerText =
            !string.IsNullOrEmpty(Text) && Text != _lastSyncedText;

        _syncingInput = true;
        if (!replaceWithCallerText)
        {
            SetCurrentValue(TextProperty, _anchorBar.Text);
            _lastSyncedText = Text;
        }
        else
        {
            // A caller seeded a different query for this opening; it becomes the shared query.
            _anchorBar.SetCurrentValue(MaterialSearchBar.TextProperty, Text);
            _lastSyncedText = Text;
        }

        if (_searchBar is not null)
        {
            _searchBar.SelectionStart = _anchorBar.SelectionStart;
            _searchBar.SelectionEnd = _anchorBar.SelectionEnd;
        }

        _syncingInput = false;
        _anchorBar.IsExpanded = true;
    }

    /// <summary>Pushes the expanded query and caret/selection back to the collapsed bar.</summary>
    private void PushInputStateToAnchor()
    {
        if (_anchorBar is null)
            return;

        _syncingInput = true;
        _anchorBar.SetCurrentValue(MaterialSearchBar.TextProperty, Text);
        if (_searchBar is not null)
        {
            _anchorBar.SelectionStart = _searchBar.SelectionStart;
            _anchorBar.SelectionEnd = _searchBar.SelectionEnd;
        }

        _anchorBar.IsExpanded = false;
        _lastSyncedText = Text;
        _syncingInput = false;
    }

    private void SeedHeaderSelectionFromAnchor()
    {
        if (_searchBar is null || _anchorBar is null)
            return;
        _searchBar.SelectionStart = _anchorBar.SelectionStart;
        _searchBar.SelectionEnd = _anchorBar.SelectionEnd;
    }

    private double ResolveCollapsedCornerRadius()
    {
        if (_anchorBar is { } bar && bar.CornerRadius.TopLeft > 0)
            return bar.CornerRadius.TopLeft;
        return FieldCornerRadius.TopLeft > 0 ? FieldCornerRadius.TopLeft : 28;
    }

    // Docked expressive is the source two-card layout: ExpandedDockedSearchBarWithGap, whose
    // rememberSearchBarWithGapState defaults are DefaultSpatial to expand and FastSpatial to
    // collapse. Full-screen expressive is the contained surface (ExpandedFullScreenContainedSearchBar
    // with rememberContainedSearchBarState), FastSpatial in both directions. The baseline layouts
    // (ExpandedFullScreenSearchBar / ExpandedDockedSearchBar with rememberSearchBarState) expand
    // with SlowSpatial and collapse with DefaultSpatial.
    private (MaterialSpring Expand, MaterialSpring Collapse) ResolveExpansionSprings() =>
        (Mode, Variant) switch
        {
            (MaterialSearchViewMode.Docked, MaterialSearchViewVariant.Expressive) =>
                (MaterialSpring.SpatialDefault, MaterialSpring.SpatialFast),
            (MaterialSearchViewMode.FullScreen, MaterialSearchViewVariant.Expressive) =>
                (MaterialSpring.SpatialFast, MaterialSpring.SpatialFast),
            _ => (MaterialSpring.SpatialSlow, MaterialSpring.SpatialDefault),
        };

    /// <summary>True for the docked two-card layout that slides its dropdown out from under the bar.</summary>
    private bool IsGapLayout =>
        Mode == MaterialSearchViewMode.Docked && Variant == MaterialSearchViewVariant.Expressive;

    // The contained full-screen surface fades with progress; the baseline full-screen container and
    // both docked containers stay opaque and only their content fades/grows (FullScreenSearchBarLayout
    // alphaBlock and DockedSearchBarLayout). Without an anchor the view has no source bounds to grow
    // from, so the whole surface fades in place.
    private bool SurfaceFadesWithProgress =>
        ExpansionAnchor is null ||
        (Mode == MaterialSearchViewMode.FullScreen && Variant == MaterialSearchViewVariant.Expressive);

    /// <summary>
    /// Applies the source content alpha: the expressive layouts fade the suggestions with
    /// <see cref="_contentProgress"/> (the content effect), the baseline full-screen layout fades
    /// them with the expansion progress, and the docked baseline layout has no content alpha at all.
    /// The scrim follows the expansion progress, as the pinned docked dropdown does
    /// (<c>drawRect(alpha = state.progress)</c>).
    /// </summary>
    private void ApplyContentOpacity()
    {
        if (_suggestionsHost is not null)
        {
            _suggestionsHost.Opacity = Variant == MaterialSearchViewVariant.Expressive
                ? _contentProgress
                : Mode == MaterialSearchViewMode.FullScreen ? _expansionProgress : 1;
        }

        if (_divider is not null)
        {
            // The pinned baseline full-screen layout puts the divider inside the content column,
            // which carries the expansion progress; the docked baseline divider is not faded.
            _divider.Opacity = Mode == MaterialSearchViewMode.FullScreen &&
                               Variant == MaterialSearchViewVariant.Baseline
                ? _expansionProgress
                : 1;
        }

        if (_scrim is not null)
            _scrim.Opacity = 0.32 * _expansionProgress;
    }

    /// <summary>
    /// Switches the content driver when <see cref="Variant"/> or <see cref="Mode"/> changes while
    /// the view is open.
    /// </summary>
    private void UpdateContentEffectMode()
    {
        _contentTween?.Dispose();
        _contentTween = null;
        if (IsOpen && _expansionProgress >= 0.999)
            _contentProgress = 1;
        if (!IsGapLayout)
        {
            _slideSpring?.Dispose();
            _slideSpring = null;
            _slideProgress = 0;
            _slideTarget = 0;
            if (_suggestionsHost is not null)
                _suggestionsHost.RenderTransform = null;
        }

        ApplyContentOpacity();
    }

    private void AttachLightDismiss()
    {
        if (Mode != MaterialSearchViewMode.Docked || _lightDismissRoot is not null)
            return;
        if (TopLevel.GetTopLevel(this) is not { } root)
            return;
        _lightDismissRoot = root;
        root.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachLightDismiss()
    {
        if (_lightDismissRoot is null)
            return;
        _lightDismissRoot.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _lightDismissRoot = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Source docked popups light-dismiss on a press outside the popup. Full-screen uses its
        // scrim (PART_Scrim) instead and must not treat header presses as outside.
        if (!IsOpen || Mode != MaterialSearchViewMode.Docked)
            return;
        if (e.Source is Visual source)
        {
            if (source == this || source.GetSelfAndVisualAncestors().Contains(this))
                return;
            if (ExpansionAnchor is { } anchor &&
                (source == anchor || source.GetSelfAndVisualAncestors().Contains(anchor)))
                return;
        }

        Dismiss(MaterialSearchDismissReason.Outside);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            // Source/gallery contract: Escape clears the query first; a second press dismisses.
            if (!string.IsNullOrEmpty(Text))
            {
                Clear();
                FocusInput();
            }
            else
            {
                Dismiss(MaterialSearchDismissReason.Escape);
            }

            return;
        }

        base.OnKeyDown(e);
    }

    private void DetachParts()
    {
        if (_searchBar is not null)
        {
            _searchBar.SearchRequested -= OnBarSearchRequested;
            _searchBar.ClearRequested -= OnBarClearRequested;
            _searchBar.DismissRequested -= OnBarDismissRequested;
            _searchBar.LeadingIconInvoked -= OnBarLeadingInvoked;
            _searchBar.TrailingIconInvoked -= OnBarTrailingInvoked;
            _searchBar.SecondaryIconInvoked -= OnBarSecondaryInvoked;
            _searchBar.RemoveHandler(KeyDownEvent, OnBarPreviewKeyDown);
        }

        if (_suggestions is not null)
        {
            _suggestions.RemoveHandler(KeyDownEvent, OnSuggestionsKeyDown);
            _suggestions.SelectionChanged -= OnSuggestionSelectionChanged;
            _suggestions.Tapped -= OnSuggestionTapped;
        }

        if (_scrim is not null)
            _scrim.PointerPressed -= OnScrimPressed;

        if (_itemsView is not null)
        {
            _itemsView.CollectionChanged -= OnItemsChanged;
            _itemsView = null;
        }

        _expansionSpring?.Dispose();
        _expansionSpring = null;
        _slideSpring?.Dispose();
        _slideSpring = null;
        _contentTween?.Dispose();
        _contentTween = null;
        _closing = false;
    }

    private void AttachItemsView()
    {
        var view = _suggestions?.ItemsView;
        if (ReferenceEquals(view, _itemsView))
            return;

        if (_itemsView is not null)
            _itemsView.CollectionChanged -= OnItemsChanged;

        _itemsView = view;

        if (_itemsView is not null)
            _itemsView.CollectionChanged += OnItemsChanged;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":docked", Mode == MaterialSearchViewMode.Docked);
        PseudoClasses.Set(":full-screen", Mode == MaterialSearchViewMode.FullScreen);
        PseudoClasses.Set(":baseline", Variant == MaterialSearchViewVariant.Baseline);
        PseudoClasses.Set(":supporting-text", Configuration == MaterialSearchFieldConfiguration.SupportingText);
        PseudoClasses.Set(":closed", !IsOpen);
        PseudoClasses.Set(":scrim",
            ShowScrim &&
            (Mode == MaterialSearchViewMode.FullScreen ||
             Variant == MaterialSearchViewVariant.Expressive));
    }

    private void UpdateConfiguration()
    {
        if (_searchBar is null)
            return;
        // Source anatomy: "Input text" shows the voice action; "Supporting text" does not.
        _searchBar.ShowSecondaryIcon = Configuration == MaterialSearchFieldConfiguration.InputText;
        _searchBar.ShowTrailingIcon = ShowTrailingIcon;
    }

    private void UpdateSuggestionsVisibility()
    {
        if (_suggestionsHost is null)
            return;
        var count = _suggestions?.ItemCount ?? 0;
        _suggestionsHost.IsVisible = ShowSuggestions && count > 0;
    }

    private void UpdateAutoFocus()
    {
        if (!AutoFocus || !IsOpen || !this.IsAttachedToVisualTree())
            return;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (AutoFocus && IsOpen && _searchBar is not null &&
                    this.IsAttachedToVisualTree())
                    _searchBar.FocusInput();
            },
            DispatcherPriority.Loaded);
    }

    private void AnimateExpansion(bool open)
    {
        UpdateExpansionState(open);

        if (_root is null)
        {
            _expansionProgress = IsOpen ? 1 : 0;
            UpdateExpansionState();
            return;
        }

        var generation = ++_expansionGeneration;
        var root = _root;
        // Direction reversals hand the running velocity to the new spring, like Animatable.
        var velocity = _expansionSpring?.Velocity ?? 0;
        _expansionSpring?.Dispose();
        _expansionSpring = null;
        _contentTween?.Dispose();
        _contentTween = null;

        if (open)
        {
            var interrupted = _closing || (IsVisible && _expansionProgress > 0.001);
            _closing = false;
            IsVisible = true;
            if (!interrupted)
            {
                _expandedCornerRadius = root.CornerRadius;
                _collapsedCornerRadius = ResolveCollapsedCornerRadius();
                _anchorWasVisible = ExpansionAnchor?.IsVisible ?? true;
                _anchorVisibilityChanged = false;
                _slideSpring?.Dispose();
                _slideSpring = null;
                _slideProgress = 0;
                _slideTarget = 0;
                _contentProgress = 0;
                velocity = 0;
                ApplyExpansion(root, 0, open: true);
            }

            // Re-evaluate even when the opening interrupted a collapse: the anchor-hide policy
            // may have been changed while the view was running.
            _hideAnchorWhenExpanded = ExpansionAnchor is not null && HideAnchorWhenExpanded;

            // Post so the anchor's bounds are current after resize/scroll; the generation guard
            // prevents a stale open from restarting after the caller closed the view again.
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (generation != _expansionGeneration || !IsOpen || _root is null)
                        return;
                    StartExpansionAnimation(_root, true, _expansionProgress, velocity);
                },
                DispatcherPriority.Loaded);
        }
        else
        {
            // Restore the anchor before collapsing: the reverse transition needs its live bounds.
            if (ExpansionAnchor is { } anchor && _anchorVisibilityChanged)
            {
                anchor.IsVisible = _anchorWasVisible;
                _anchorVisibilityChanged = false;
            }
            _closing = true;
            if (!IsVisible || _expansionProgress <= 0.001)
            {
                root.Opacity = 0;
                FinishExpansion(root);
                return;
            }

            // The source flips targetValue immediately, so the with-gap dropdown starts its exit
            // together with the container collapse.
            SetGapSlideTarget(false);
            StartExpansionAnimation(root, open: false, _expansionProgress, velocity);
        }
    }

    private void StartExpansionAnimation(Border root, bool open, double from, double velocity)
    {
        var (expandSpring, collapseSpring) = ResolveExpansionSprings();
        _expansionSpring?.Dispose();
        _expansionSpring = MaterialMotion.Animate(root, from, open ? 1 : 0,
            value => ApplyExpansion(root, value, open), open ? expandSpring : collapseSpring,
            0.01, velocity);
        StartContentAnimation(root, open);
    }

    // Source AnimationForContentFadeInSpec: tween Short2 (100 ms) after the Short1 (50 ms) delay
    // with the standard-accelerate curve. AnimationForContentFadeOutSpec: tween Short2 with the
    // standard-decelerate curve and no delay. Both run beside the container animation.
    private void StartContentAnimation(Border root, bool open)
    {
        _contentTween?.Dispose();
        _contentTween = null;

        if (Variant != MaterialSearchViewVariant.Expressive)
        {
            // The baseline layouts have no content animation: the full-screen results fade with the
            // expansion progress, the docked results are grown into view by the container.
            ApplyContentOpacity();
            return;
        }

        var from = Math.Clamp(_contentProgress, 0, 1);
        if ((open && from >= 0.999) || (!open && from <= 0.001))
        {
            ApplyContentOpacity();
            return;
        }

        _contentTween = MaterialMotion.Tween(root, from, open ? 1 : 0, 0.1, open ? 0.05 : 0,
            value =>
            {
                _contentProgress = value;
                ApplyContentOpacity();
            },
            open ? CubicBezier(0.3, 0, 1, 1) : CubicBezier(0, 0, 0, 1));
    }

    // Source with-gap layout: the dropdown is visible once progress passes 0.1 and enters/exits
    // with the state's expand/collapse spec (slideIn/slideOut on AnimatedVisibility), independent
    // of the container spring.
    private void UpdateGapSlide(bool open, double progress)
    {
        if (open && progress > 0.1)
            SetGapSlideTarget(true);
    }

    private void SetGapSlideTarget(bool visible)
    {
        if (!IsGapLayout || _suggestionsHost is null)
            return;
        var host = _suggestionsHost;

        var target = visible ? 1d : 0d;
        if (Math.Abs(_slideTarget - target) < 0.001 && _slideSpring is not null)
            return;
        if (Math.Abs(_slideProgress - target) < 0.001)
        {
            _slideTarget = target;
            ApplyGapSlide();
            return;
        }

        var (expandSpring, collapseSpring) = ResolveExpansionSprings();
        var velocity = _slideSpring?.Velocity ?? 0;
        _slideSpring?.Dispose();
        _slideTarget = target;
        _slideSpring = MaterialMotion.Animate(host, _slideProgress, target,
            value =>
            {
                _slideProgress = value;
                ApplyGapSlide();
            },
            visible ? expandSpring : collapseSpring, 0.01, velocity);
    }

    private void ApplyGapSlide()
    {
        if (_suggestionsHost is null)
            return;
        if (_slideProgress <= 0.001 && _slideTarget <= 0.001)
        {
            _suggestionsHost.RenderTransform = null;
            return;
        }

        if (_slideProgress >= 0.999)
        {
            _suggestionsHost.RenderTransform = null;
            return;
        }

        var hostHeight = _suggestionsHost.Bounds.Height;
        _suggestionsHost.RenderTransform = hostHeight > 0
            ? new TranslateTransform(0, -(1 - _slideProgress) * hostHeight * 0.5)
            : null;
    }

    private void ApplyExpansion(Border root, double progress, bool open)
    {
        var p = Math.Clamp(progress, 0, 1);
        _expansionProgress = p;
        UpdateExpansionState();
        // Non-fading layouts stay opaque while they grow; at exactly zero the view is closed, so
        // keep it hidden until the first spring frame has the anchor bounds.
        root.Opacity = SurfaceFadesWithProgress ? p : p > 0 ? 1 : 0;

        var docked = Mode == MaterialSearchViewMode.Docked;
        if (ExpansionAnchor is { } anchor && anchor.IsAttachedToVisualTree() &&
            Bounds.Width > 0 && Bounds.Height > 0)
        {
            var anchorOrigin = anchor.TranslatePoint(new Point(0, 0), this);
            if (anchorOrigin is { } origin)
            {
                var anchorWidth = anchor.Bounds.Width;
                var anchorHeight = anchor.Bounds.Height;
                // Source docked expansion keeps the collapsed width and grows only in height.
                var sx = docked ? 1 : Math.Clamp(anchorWidth / Bounds.Width, 0.05, 1);
                var sy = Math.Clamp(anchorHeight / Bounds.Height, 0.05, 1);
                var dx = docked
                    ? 0
                    : (origin.X + anchorWidth / 2 - Bounds.Width / 2) * (1 - p);
                var dy = (origin.Y + anchorHeight / 2 - Bounds.Height / 2) * (1 - p);
                if (p >= 0.999 || p <= 0.001)
                {
                    root.RenderTransform = null;
                }
                else
                {
                    var group = new TransformGroup();
                    group.Children.Add(new ScaleTransform(
                        sx + (1 - sx) * p, sy + (1 - sy) * p));
                    group.Children.Add(new TranslateTransform(dx, dy));
                    root.RenderTransform = group;
                    root.RenderTransformOrigin = RelativePoint.Center;
                }
            }
        }

        if (IsGapLayout)
        {
            UpdateGapSlide(open, p);
            ApplyGapSlide();
        }
        else if (_suggestionsHost is not null)
            _suggestionsHost.RenderTransform = null;

        ApplyContentOpacity();

        var collapsed = Math.Max(0, _collapsedCornerRadius);
        var radius = collapsed + (_expandedCornerRadius.TopLeft - collapsed) * p;
        root.CornerRadius = new CornerRadius(radius);

        if (open && p >= 0.999)
        {
            root.CornerRadius = _expandedCornerRadius;
            if (_hideAnchorWhenExpanded && ExpansionAnchor is { } expandedAnchor)
            {
                expandedAnchor.IsVisible = false;
                _anchorVisibilityChanged = true;
            }
            _hideAnchorWhenExpanded = false;
        }

        if (!open && p <= 0.0001 && _closing)
            FinishExpansion(root);
    }

    private void FinishExpansion(Border root)
    {
        _closing = false;
        IsVisible = false;
        _expansionProgress = 0;
        _contentProgress = 0;
        UpdateExpansionState();
        _slideProgress = 0;
        _slideTarget = 0;
        root.Opacity = 0;
        root.RenderTransform = null;
        root.ClearValue(Border.CornerRadiusProperty);
        if (_suggestionsHost is not null)
        {
            _suggestionsHost.Opacity = 0;
            _suggestionsHost.RenderTransform = null;
        }

        if (_scrim is not null)
            _scrim.Opacity = 0;
    }

    private static Func<double, double> CubicBezier(double x1, double y1, double x2, double y2) =>
        t =>
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            var low = 0.0;
            var high = 1.0;
            for (var i = 0; i < 24; i++)
            {
                var mid = (low + high) / 2;
                var x = Bezier(mid, x1, x2);
                if (x < t)
                    low = mid;
                else
                    high = mid;
            }
            return Bezier((low + high) / 2, y1, y2);
        };

    private static double Bezier(double t, double p1, double p2)
    {
        var inv = 1 - t;
        return 3 * inv * inv * t * p1 + 3 * inv * t * t * p2 + t * t * t;
    }

    private void RestoreFocus()
    {
        if (_previousFocus is not InputElement previous ||
            !previous.IsAttachedToVisualTree() ||
            !previous.Focusable)
            return;
        previous.Focus(NavigationMethod.Unspecified);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateSuggestionsVisibility();

    private void OnBarSearchRequested(object? sender, EventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);

    private void OnBarClearRequested(object? sender, EventArgs e) => ClearRequested?.Invoke(this, EventArgs.Empty);

    private void OnBarDismissRequested(object? sender, EventArgs e) => Dismiss(MaterialSearchDismissReason.Escape);

    private void OnBarLeadingInvoked(object? sender, EventArgs e) => Dismiss(MaterialSearchDismissReason.Back);

    private void OnBarTrailingInvoked(object? sender, EventArgs e) => Dismiss(MaterialSearchDismissReason.Close);

    private void OnBarSecondaryInvoked(object? sender, EventArgs e) => VoiceInputRequested?.Invoke(this, EventArgs.Empty);

    private void OnSuggestionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suggestions is not null)
            SetCurrentValue(SelectedSuggestionProperty, _suggestions.SelectedItem);
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        // A tap anywhere inside a suggestion performs it, which is the expected pointer and
        // touch behavior for a search suggestion; the selection has already moved by now.
        if (e.Source is Visual source &&
            source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().Any() &&
            SelectedSuggestion is not null)
        {
            e.Handled = true;
            ActivateSelectedSuggestion();
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        Dismiss(MaterialSearchDismissReason.Scrim);
    }

    // Down in the field moves into the suggestion list; the TextBox would otherwise consume it
    // for caret movement, so this handler runs in the tunnel phase.
    private void OnBarPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || _suggestions is not { } list || !list.IsEffectivelyVisible || list.ItemCount == 0)
            return;

        e.Handled = true;
        if (list.SelectedIndex < 0)
            list.SelectedIndex = 0;
        list.ScrollIntoView(list.SelectedIndex);
        list.Focus(NavigationMethod.Directional);
    }

    private void OnSuggestionsKeyDown(object? sender, KeyEventArgs e)
    {
        if (_suggestions is not { } list)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                ActivateSelectedSuggestion();
                break;
            case Key.Escape:
                e.Handled = true;
                if (!string.IsNullOrEmpty(Text) || !string.IsNullOrEmpty(_searchBar?.Text))
                {
                    // Source/gallery contract: Escape clears the query first, then closes on a
                    // second press. Clear() already raises ClearRequested through the header bar.
                    Clear();
                }
                else
                {
                    Dismiss(MaterialSearchDismissReason.Escape);
                }
                break;
            case Key.Up when list.SelectedIndex <= 0:
                e.Handled = true;
                FocusInput();
                break;
        }
    }
}
