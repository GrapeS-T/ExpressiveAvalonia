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
    private ItemsSourceView? _itemsView;
    private IInputElement? _previousFocus;

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
        if (hadFocus)
            RestoreFocus();
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
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is Visual visual && !visual.GetVisualAncestors().Contains(this))
            _previousFocus = focused;

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
            if (change.Property == IsOpenProperty)
                UpdateAutoFocus();
        }
        else if (change.Property == TextProperty)
        {
            QueryChanged?.Invoke(this, EventArgs.Empty);
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
        else if (change.Property == AutoFocusProperty)
        {
            UpdateAutoFocus();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            Dismiss(MaterialSearchDismissReason.Escape);
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
        PseudoClasses.Set(":scrim", Mode == MaterialSearchViewMode.FullScreen && ShowScrim);
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
                if (AutoFocus && IsOpen && _searchBar is not null)
                    _searchBar.FocusInput();
            },
            DispatcherPriority.Loaded);
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
                FocusInput();
                if (string.IsNullOrEmpty(Text))
                    Dismiss(MaterialSearchDismissReason.Escape);
                break;
            case Key.Up when list.SelectedIndex <= 0:
                e.Handled = true;
                FocusInput();
                break;
        }
    }
}
