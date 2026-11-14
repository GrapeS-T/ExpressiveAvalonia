using System;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>Horizontal alignment of the query and hint text inside a search bar.</summary>
public enum MaterialSearchBarAlignment
{
    /// <summary>Text starts after the leading icon, 4 DIPs from its edge (source default).</summary>
    LeftAligned,

    /// <summary>Text is centered in the content area; the source's "Centered" building block.</summary>
    Centered,
}

/// <summary>
/// The Material 3 search bar: a 56 DIP pill with an optional leading icon button, a native
/// single-line text field, up to two trailing icon buttons and an optional profile avatar.
/// </summary>
/// <remarks>
/// <para>
/// Editing, selection, clipboard, undo, input methods and the caret stay native: the template
/// hosts an ordinary <see cref="TextBox"/>. Enter raises <see cref="SearchRequested"/>; Escape
/// clears a non-empty query first and only then raises <see cref="DismissRequested"/>, which is
/// the platform convention for search fields.
/// </para>
/// <para>
/// Geometry defaults mirror the source component: a menu glyph as the leading icon, a magnifier
/// as the first trailing action and a hidden "stars" second action. Docked and full-screen search
/// views override them with a back arrow and a close/microphone pair.
/// </para>
/// <para>
/// The container is the control's own <see cref="TemplatedControl.Background"/>,
/// <see cref="TemplatedControl.BorderBrush"/> and <see cref="TemplatedControl.CornerRadius"/>, so
/// the source's Flat (<c>#f3edf7</c>), On-scroll (<c>#e6e0e9</c>) and layout-header (transparent)
/// treatments are plain property values rather than separate templates.
/// </para>
/// </remarks>
[TemplatePart("PART_Container", typeof(Border))]
[TemplatePart("PART_TextBox", typeof(TextBox))]
[TemplatePart("PART_LeadingButton", typeof(Button))]
[TemplatePart("PART_ClearButton", typeof(Button))]
[TemplatePart("PART_TrailingButton", typeof(Button))]
[TemplatePart("PART_SecondaryButton", typeof(Button))]
[PseudoClasses(":centered", ":pressed")]
public class MaterialSearchBar : TemplatedControl
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(Text), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="PlaceholderText"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(PlaceholderText));

    /// <summary>Defines the <see cref="Alignment"/> property.</summary>
    public static readonly StyledProperty<MaterialSearchBarAlignment> AlignmentProperty =
        AvaloniaProperty.Register<MaterialSearchBar, MaterialSearchBarAlignment>(nameof(Alignment));

    /// <summary>Defines the <see cref="ShowLeadingIcon"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLeadingIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ShowLeadingIcon), true);

    /// <summary>Defines the <see cref="LeadingIcon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> LeadingIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, Geometry?>(nameof(LeadingIcon));

    /// <summary>Defines the <see cref="LeadingIconCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> LeadingIconCommandProperty =
        AvaloniaProperty.Register<MaterialSearchBar, ICommand?>(nameof(LeadingIconCommand));

    /// <summary>Defines the <see cref="LeadingIconName"/> property.</summary>
    public static readonly StyledProperty<string?> LeadingIconNameProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(LeadingIconName));

    /// <summary>Defines the <see cref="ShowClearButton"/> property.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ShowClearButton), true);

    /// <summary>Defines the <see cref="ShowTrailingIcon"/> property.</summary>
    public static readonly StyledProperty<bool> ShowTrailingIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ShowTrailingIcon), true);

    /// <summary>Defines the <see cref="TrailingIcon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> TrailingIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, Geometry?>(nameof(TrailingIcon));

    /// <summary>Defines the <see cref="TrailingIconCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> TrailingIconCommandProperty =
        AvaloniaProperty.Register<MaterialSearchBar, ICommand?>(nameof(TrailingIconCommand));

    /// <summary>Defines the <see cref="TrailingIconName"/> property.</summary>
    public static readonly StyledProperty<string?> TrailingIconNameProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(TrailingIconName));

    /// <summary>Defines the <see cref="ShowSecondaryIcon"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSecondaryIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ShowSecondaryIcon));

    /// <summary>Defines the <see cref="SecondaryIcon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> SecondaryIconProperty =
        AvaloniaProperty.Register<MaterialSearchBar, Geometry?>(nameof(SecondaryIcon));

    /// <summary>Defines the <see cref="SecondaryIconCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> SecondaryIconCommandProperty =
        AvaloniaProperty.Register<MaterialSearchBar, ICommand?>(nameof(SecondaryIconCommand));

    /// <summary>Defines the <see cref="SecondaryIconName"/> property.</summary>
    public static readonly StyledProperty<string?> SecondaryIconNameProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(SecondaryIconName));

    /// <summary>Defines the <see cref="ShowAvatar"/> property.</summary>
    public static readonly StyledProperty<bool> ShowAvatarProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ShowAvatar));

    /// <summary>Defines the <see cref="AvatarInitial"/> property.</summary>
    public static readonly StyledProperty<string?> AvatarInitialProperty =
        AvaloniaProperty.Register<MaterialSearchBar, string?>(nameof(AvatarInitial));

    /// <summary>Defines the <see cref="AvatarSource"/> property.</summary>
    public static readonly StyledProperty<IImage?> AvatarSourceProperty =
        AvaloniaProperty.Register<MaterialSearchBar, IImage?>(nameof(AvatarSource));

    /// <summary>Defines the <see cref="AutoFocus"/> property.</summary>
    public static readonly StyledProperty<bool> AutoFocusProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(AutoFocus));

    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(IsExpanded),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="SelectionStart"/> property.</summary>
    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<MaterialSearchBar, int>(nameof(SelectionStart),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="SelectionEnd"/> property.</summary>
    public static readonly StyledProperty<int> SelectionEndProperty =
        AvaloniaProperty.Register<MaterialSearchBar, int>(nameof(SelectionEnd),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="ClearFocusOnCollapse"/> property.</summary>
    public static readonly StyledProperty<bool> ClearFocusOnCollapseProperty =
        AvaloniaProperty.Register<MaterialSearchBar, bool>(nameof(ClearFocusOnCollapse));

    private TextBox? _textBox;
    private bool _syncingSelection;
    private bool _selectionSeeded;
    private Button? _clearButton;
    private Button? _leadingButton;
    private Button? _trailingButton;
    private Button? _secondaryButton;
    private IDisposable? _focusClearTimer;

    /// <summary>Raised when the user confirms the query with Enter.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Raised after the query has been cleared, by the clear button or Escape.</summary>
    public event EventHandler? ClearRequested;

    /// <summary>Raised when Escape is pressed while the query is already empty.</summary>
    public event EventHandler? DismissRequested;

    /// <summary>Raised when <see cref="LeadingIconCommand"/> is not set and the leading action is pressed.</summary>
    public event EventHandler? LeadingIconInvoked;

    /// <summary>Raised when <see cref="TrailingIconCommand"/> is not set and the trailing action is pressed.</summary>
    public event EventHandler? TrailingIconInvoked;

    /// <summary>Raised when <see cref="SecondaryIconCommand"/> is not set and the second action is pressed.</summary>
    public event EventHandler? SecondaryIconInvoked;

    /// <summary>Raised whenever <see cref="Text"/> changes.</summary>
    public event EventHandler? QueryChanged;

    /// <summary>
    /// Raised when the collapsed bar asks to be expanded: a press on the field, the first typed
    /// character, or Down while the field is focused. The owner opens its
    /// <see cref="MaterialSearchView"/> and sets <see cref="IsExpanded"/>; icon buttons never raise
    /// this event, matching the source component.
    /// </summary>
    public event EventHandler? ExpansionRequested;

    /// <summary>The current query. Bound two-way by default.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The hint shown while <see cref="Text"/> is empty.</summary>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Whether the query and hint are left aligned or centered.</summary>
    public MaterialSearchBarAlignment Alignment
    {
        get => GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    /// <summary>Shows the 48 DIP leading icon button. Defaults to true.</summary>
    public bool ShowLeadingIcon
    {
        get => GetValue(ShowLeadingIconProperty);
        set => SetValue(ShowLeadingIconProperty, value);
    }

    /// <summary>The leading glyph. The theme supplies the source's menu glyph.</summary>
    public Geometry? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    /// <summary>Invoked when the leading icon button is activated.</summary>
    public ICommand? LeadingIconCommand
    {
        get => GetValue(LeadingIconCommandProperty);
        set => SetValue(LeadingIconCommandProperty, value);
    }

    /// <summary>Accessible name and tooltip of the leading icon button.</summary>
    public string? LeadingIconName
    {
        get => GetValue(LeadingIconNameProperty);
        set => SetValue(LeadingIconNameProperty, value);
    }

    /// <summary>
    /// Shows the clear button. It is only visible while <see cref="Text"/> is not empty, so the
    /// resting bar keeps the source's single trailing action. Defaults to true.
    /// </summary>
    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <summary>Shows the first trailing icon button. Defaults to true.</summary>
    public bool ShowTrailingIcon
    {
        get => GetValue(ShowTrailingIconProperty);
        set => SetValue(ShowTrailingIconProperty, value);
    }

    /// <summary>The first trailing glyph. The theme supplies the source's magnifier.</summary>
    public Geometry? TrailingIcon
    {
        get => GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
    }

    /// <summary>Invoked when the first trailing icon button is activated.</summary>
    public ICommand? TrailingIconCommand
    {
        get => GetValue(TrailingIconCommandProperty);
        set => SetValue(TrailingIconCommandProperty, value);
    }

    /// <summary>Accessible name and tooltip of the first trailing icon button.</summary>
    public string? TrailingIconName
    {
        get => GetValue(TrailingIconNameProperty);
        set => SetValue(TrailingIconNameProperty, value);
    }

    /// <summary>Shows the optional second trailing icon button. Defaults to false.</summary>
    public bool ShowSecondaryIcon
    {
        get => GetValue(ShowSecondaryIconProperty);
        set => SetValue(ShowSecondaryIconProperty, value);
    }

    /// <summary>The second trailing glyph. The theme supplies the source's "stars" glyph.</summary>
    public Geometry? SecondaryIcon
    {
        get => GetValue(SecondaryIconProperty);
        set => SetValue(SecondaryIconProperty, value);
    }

    /// <summary>Invoked when the second trailing icon button is activated.</summary>
    public ICommand? SecondaryIconCommand
    {
        get => GetValue(SecondaryIconCommandProperty);
        set => SetValue(SecondaryIconCommandProperty, value);
    }

    /// <summary>Accessible name and tooltip of the second trailing icon button.</summary>
    public string? SecondaryIconName
    {
        get => GetValue(SecondaryIconNameProperty);
        set => SetValue(SecondaryIconNameProperty, value);
    }

    /// <summary>Shows the 30 DIP profile avatar at the trailing edge. Defaults to false.</summary>
    public bool ShowAvatar
    {
        get => GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    /// <summary>Monogram shown when <see cref="AvatarSource"/> is not set.</summary>
    public string? AvatarInitial
    {
        get => GetValue(AvatarInitialProperty);
        set => SetValue(AvatarInitialProperty, value);
    }

    /// <summary>Image avatar; falls back to the <see cref="AvatarInitial"/> monogram.</summary>
    public IImage? AvatarSource
    {
        get => GetValue(AvatarSourceProperty);
        set => SetValue(AvatarSourceProperty, value);
    }

    /// <summary>Moves focus into the text field when the bar is attached. Defaults to false.</summary>
    public bool AutoFocus
    {
        get => GetValue(AutoFocusProperty);
        set => SetValue(AutoFocusProperty, value);
    }

    /// <summary>
    /// Whether the owner currently presents the expanded search view. The bar itself only reacts:
    /// it keeps its query, caret and selection so the owning view can continue the same editing
    /// session, exposes the source's "Suggestions available" state to automation, and can clear
    /// focus when the session ends.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Caret or selection start, kept in sync with the native <see cref="TextBox"/>. It lets the
    /// expanded view continue the exact editing position instead of starting a new one.
    /// </summary>
    public int SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    /// <summary>Caret or selection end, kept in sync with the native <see cref="TextBox"/>.</summary>
    public int SelectionEnd
    {
        get => GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    /// <summary>
    /// Clears the field's focus shortly after <see cref="IsExpanded"/> becomes false. The source
    /// does this in touch mode only; desktop hosts default to false so the query keeps focus after
    /// the view collapses.
    /// </summary>
    public bool ClearFocusOnCollapse
    {
        get => GetValue(ClearFocusOnCollapseProperty);
        set => SetValue(ClearFocusOnCollapseProperty, value);
    }

    /// <summary>
    /// Moves keyboard focus into the text field. The caret/selection position is preserved;
    /// <paramref name="selectAll"/> selects the whole query instead.
    /// </summary>
    public void FocusInput(bool selectAll = false)
    {
        if (_textBox is null)
            return;
        _textBox.Focus(NavigationMethod.Directional);
        if (selectAll)
            _textBox.SelectAll();
        else
            ApplySelectionToTextBox();
    }

    /// <summary>Clears the query, raises <see cref="ClearRequested"/> and keeps focus in the field.</summary>
    public void Clear()
    {
        if (string.IsNullOrEmpty(Text))
            return;
        SetCurrentValue(TextProperty, string.Empty);
        SetCurrentValue(SelectionStartProperty, 0);
        SetCurrentValue(SelectionEndProperty, 0);
        _textBox?.Focus(NavigationMethod.Directional);
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="SearchRequested"/> for the current query.</summary>
    public void Submit() => SearchRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_textBox is not null)
        {
            _textBox.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _textBox.PropertyChanged -= OnTextBoxPropertyChanged;
            _textBox.GotFocus -= OnTextBoxGotFocus;
        }

        if (_clearButton is not null)
            _clearButton.Click -= OnClearClick;
        if (_leadingButton is not null)
            _leadingButton.Click -= OnLeadingClick;
        if (_trailingButton is not null)
            _trailingButton.Click -= OnTrailingClick;
        if (_secondaryButton is not null)
            _secondaryButton.Click -= OnSecondaryClick;

        _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        _leadingButton = e.NameScope.Find<Button>("PART_LeadingButton");
        _trailingButton = e.NameScope.Find<Button>("PART_TrailingButton");
        _secondaryButton = e.NameScope.Find<Button>("PART_SecondaryButton");

        if (_textBox is not null)
        {
            // Tunnel: the native TextBox class handler must not consume Enter/Escape first.
            _textBox.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            _textBox.PropertyChanged += OnTextBoxPropertyChanged;
            _textBox.GotFocus += OnTextBoxGotFocus;
            if (!_selectionSeeded)
            {
                _selectionSeeded = true;
                // Source TextFieldState starts with the caret after pre-set text; a bar opened
                // with an existing query should keep editing at that end, not at index 0.
                if (Text is { Length: > 0 } && SelectionStart == 0 && SelectionEnd == 0)
                {
                    _syncingSelection = true;
                    SetCurrentValue(SelectionStartProperty, Text.Length);
                    SetCurrentValue(SelectionEndProperty, Text.Length);
                    _syncingSelection = false;
                }
            }
            ApplySelectionToTextBox();
        }

        if (_clearButton is not null)
            _clearButton.Click += OnClearClick;
        if (_leadingButton is not null)
            _leadingButton.Click += OnLeadingClick;
        if (_trailingButton is not null)
            _trailingButton.Click += OnTrailingClick;
        if (_secondaryButton is not null)
            _secondaryButton.Click += OnSecondaryClick;

        UpdateAlignment();
        UpdateAutomationState();
        UpdateAutoFocus();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Resource lookup can fail during OnApplyTemplate when the control is not yet in a tree;
        // refresh the localized automation strings once resources resolve.
        UpdateAutomationState();
        UpdateAutoFocus();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _focusClearTimer?.Dispose();
        _focusClearTimer = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AlignmentProperty)
            UpdateAlignment();
        else if (change.Property == AutoFocusProperty)
            UpdateAutoFocus();
        else if (change.Property == TextProperty)
            QueryChanged?.Invoke(this, EventArgs.Empty);
        else if (change.Property == IsExpandedProperty)
        {
            UpdateAutomationState();
            if (!change.GetNewValue<bool>() && ClearFocusOnCollapse)
                ClearFocusAfterCollapse();
        }
        else if (change.Property == SelectionStartProperty || change.Property == SelectionEndProperty)
        {
            if (!_syncingSelection)
                ApplySelectionToTextBox();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // The pill itself is a press target: clicking anywhere that is not the text field, a
        // button or the avatar focuses the field, which is the source's whole-bar interaction.
        if (e.Handled || _textBox is null)
            return;
        if (e.Source is Visual source &&
            (source == _textBox ||
             source.GetSelfAndVisualAncestors().Any(ancestor => ancestor == _textBox || ancestor is Button)))
            return;

        // TemplatedControl has no built-in :pressed pseudo class; the source Pressed state is a
        // 10% state layer, so the bar tracks the press itself.
        PseudoClasses.Set(":pressed", true);
        if (!_textBox.IsFocused)
            FocusInput();

        // Source InputField: a press on the field (not on an icon) expands the search bar.
        RequestExpansion();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        PseudoClasses.Set(":pressed", false);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(":pressed", false);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Reached when focus is on one of the icon buttons rather than the field.
        if (!e.Handled && (e.Key is Key.Escape or Key.Enter) && HandleKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                Submit();
                return true;
            case Key.Escape:
                if (string.IsNullOrEmpty(Text))
                    DismissRequested?.Invoke(this, EventArgs.Empty);
                else
                    Clear();
                return true;
            default:
                return false;
        }
    }

    private void UpdateAlignment() =>
        PseudoClasses.Set(":centered", Alignment == MaterialSearchBarAlignment.Centered);

    private void UpdateAutoFocus()
    {
        if (!AutoFocus || !this.IsAttachedToVisualTree())
            return;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!AutoFocus || !this.IsAttachedToVisualTree())
                    return;
                if (_textBox is { IsEffectivelyEnabled: true, IsVisible: true } textBox)
                    textBox.Focus(NavigationMethod.Directional);
            },
            DispatcherPriority.Loaded);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        // Source InputField preview handler: Down expands a collapsed bar. The expanded view
        // moves focus into its suggestion list from its own tunnel handler.
        if (!IsExpanded && e.Key is Key.Down or Key.NumPad2)
        {
            e.Handled = true;
            RequestExpansion();
            return;
        }

        if (HandleKey(e.Key))
            e.Handled = true;
    }

    private void RequestExpansion()
    {
        if (IsExpanded)
            return;
        ExpansionRequested?.Invoke(this, EventArgs.Empty);
    }


    private void OnTextBoxGotFocus(object? sender, FocusChangedEventArgs e) => ApplySelectionToTextBox();

    private void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_textBox is not { } box)
            return;
        if (e.Property == TextBox.TextProperty)
        {
            // TextChanged is posted by Avalonia: an anchor update made while expanded could
            // otherwise arrive after collapse restored focus and reopen the view. Observe the
            // edit synchronously, while its focus/expansion state still describes that edit.
            var previousLength = e.GetOldValue<string?>()?.Length ?? 0;
            if ((box.Text?.Length ?? 0) > previousLength && box.IsFocused)
                RequestExpansion();
            return;
        }
        if (e.Property != TextBox.SelectionStartProperty && e.Property != TextBox.SelectionEndProperty)
            return;

        // Avalonia clears the native selection when focus leaves (ClearSelectionOnLostFocus) and
        // clamps it when text is replaced externally. The pinned source keeps one TextFieldState
        // selection shared by the collapsed bar and the expanded field, so an unfocused field must
        // not overwrite it; FocusInput/GotFocus re-applies it to the native field on return.
        if (!box.IsFocused)
            return;

        _syncingSelection = true;
        SetCurrentValue(SelectionStartProperty, box.SelectionStart);
        SetCurrentValue(SelectionEndProperty, box.SelectionEnd);
        _syncingSelection = false;
    }

    /// <summary>Pushes <see cref="SelectionStart"/>/<see cref="SelectionEnd"/> into the native field.</summary>
    private void ApplySelectionToTextBox()
    {
        if (_textBox is not { } box || _syncingSelection)
            return;

        var length = box.Text?.Length ?? 0;
        var start = Math.Clamp(SelectionStart, 0, length);
        var end = Math.Clamp(SelectionEnd, 0, length);
        if (box.SelectionStart == start && box.SelectionEnd == end && box.CaretIndex == end)
            return;

        // Avalonia's TextBox caret setter collapses the selection to the caret
        // (OnCaretIndexChanged writes SelectionStart/End = CaretIndex), so move the caret first and
        // rebuild the range afterwards; the reverse order leaves only a caret and loses 2..5.
        _syncingSelection = true;
        box.CaretIndex = end;
        box.SelectionStart = start;
        box.SelectionEnd = end;
        _syncingSelection = false;
    }

    private void UpdateAutomationState()
    {
        // Source semantics: the input field is described as "Search"; while expanded it also
        // reports that suggestions are available. Avalonia has no state description, so the
        // latter maps to HelpText.
        var name = this.TryFindResource("MaterialSearchSearchText", out var nameValue) &&
                   nameValue is string nameText
            ? nameText
            : "Search";
        string? help = null;
        if (IsExpanded &&
            this.TryFindResource("MaterialSearchSuggestionsAvailableText", out var value) &&
            value is string text)
        {
            help = text;
        }

        AutomationProperties.SetHelpText(this, help);
        if (_textBox is not null)
        {
            AutomationProperties.SetName(_textBox, name);
            AutomationProperties.SetHelpText(_textBox, help);
        }
    }

    private void ClearFocusAfterCollapse()
    {
        _focusClearTimer?.Dispose();
        // Source touch-mode policy: wait out the expansion delay, then clear focus.
        _focusClearTimer = DispatcherTimer.RunOnce(
            () =>
            {
                _focusClearTimer = null;
                if (IsExpanded)
                    return;
                if (_textBox is { IsFocused: true })
                    TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
            },
            TimeSpan.FromMilliseconds(100));
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) => Clear();

    private void OnLeadingClick(object? sender, RoutedEventArgs e)
    {
        if (LeadingIconCommand is null)
            LeadingIconInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrailingClick(object? sender, RoutedEventArgs e)
    {
        if (TrailingIconCommand is null)
            TrailingIconInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        if (SecondaryIconCommand is null)
            SecondaryIconInvoked?.Invoke(this, EventArgs.Empty);
    }
}
