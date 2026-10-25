using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>The four Material chip families.</summary>
/// <remarks>
/// Assist chips are action-only. Filter, input and suggestion chips expose a selected state;
/// input chips additionally expose a removal affordance. The visual style axes in the source
/// export (Outlined/Elevated, icon/avatar configurations, trailing and closing icons) are mapped
/// onto <see cref="MaterialChip.Variant"/>, <see cref="MaterialChip.Icon"/>,
/// <see cref="MaterialChip.Avatar"/>, <see cref="MaterialChip.ShowIcon"/>,
/// <see cref="MaterialChip.ShowTrailingIcon"/> and <see cref="MaterialChip.IsRemovable"/>.
/// </remarks>
public enum MaterialChipKind { Assist, Filter, Input, Suggestion }

/// <summary>The two chip container treatments in the source export.</summary>
/// <remarks>Input chips have no style axis in the source; they always use the outlined treatment.</remarks>
public enum MaterialChipVariant { Outlined, Elevated }

/// <summary>A Material assist, filter, input or suggestion chip.</summary>
/// <remarks>
/// <para>
/// A chip is a real <see cref="Button"/>: it retains command execution, access keys, Space/Enter
/// activation, disabled propagation and click routing. Clicking or activating a filter, input or
/// suggestion chip toggles <see cref="IsSelected"/> before <see cref="Button.Click"/> is raised, so
/// commands observe the new selection. Assist chips never toggle selection.
/// </para>
/// <para>
/// Pressing Delete or Backspace on a removable input chip raises <see cref="RemoveRequestedEvent"/>;
/// <see cref="MaterialChipGroup"/> removes direct items and lets the event bubble for bound data.
/// </para>
/// <para>
/// <see cref="IsDraggable"/> drives the source "Dragged" visual state from a real pointer drag
/// threshold. It does not implement reordering or drag-and-drop; a group or application can react to
/// the state through the <c>:dragged</c> pseudo-class or <see cref="IsDragging"/>.
/// </para>
/// </remarks>
[TemplatePart("PART_LeadingIcon", typeof(ContentPresenter))]
[TemplatePart("PART_Avatar", typeof(ContentPresenter))]
[TemplatePart("PART_RemoveButton", typeof(Button))]
public class MaterialChip : Button
{
    public static readonly StyledProperty<MaterialChipKind> KindProperty =
        AvaloniaProperty.Register<MaterialChip, MaterialChipKind>(nameof(Kind));

    public static readonly StyledProperty<MaterialChipVariant> VariantProperty =
        AvaloniaProperty.Register<MaterialChip, MaterialChipVariant>(nameof(Variant));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsSelected), defaultBindingMode: BindingMode.TwoWay,
            coerce: (owner, value) => ((MaterialChip)owner).CoerceSelection(value));

    /// <summary>Leading icon content for assist, filter, input and suggestion chips.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(Icon));

    /// <summary>Input-chip avatar content, rendered in a 24 DIP circle.</summary>
    public static readonly StyledProperty<object?> AvatarProperty =
        AvaloniaProperty.Register<MaterialChip, object?>(nameof(Avatar));

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(ShowIcon), true);

    /// <summary>Shows the filter-chip trailing dropdown caret.</summary>
    public static readonly StyledProperty<bool> ShowTrailingIconProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(ShowTrailingIcon));

    /// <summary>Shows the input-chip closing icon.</summary>
    public static readonly StyledProperty<bool> IsRemovableProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsRemovable));

    /// <summary>Accessible name of the closing icon. Override to localize.</summary>
    public static readonly StyledProperty<string> RemoveButtonAccessibleNameProperty =
        AvaloniaProperty.Register<MaterialChip, string>(nameof(RemoveButtonAccessibleName), "Remove");

    /// <summary>Enables the pointer drag threshold that produces the source "Dragged" state.</summary>
    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<MaterialChip, bool>(nameof(IsDraggable));

    public static readonly DirectProperty<MaterialChip, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<MaterialChip, bool>(nameof(IsDragging), owner => owner.IsDragging);

    /// <summary>Raised when the user activates the closing icon or presses Delete/Backspace.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RemoveRequestedEvent =
        RoutedEvent.Register<MaterialChip, RoutedEventArgs>(nameof(RemoveRequested), RoutingStrategies.Bubble);

    private const double DragThreshold = 4;

    private Button? _removeButton;
    private bool _pointerDown;
    private Point _pointerOrigin;
    private bool _isDragging;
    private bool _suppressClick;
    private MaterialChipAutomationPeer? _peer;

    public MaterialChip()
    {
        UpdateChipPseudoClasses();
        AddHandler(PointerPressedEvent, OnDragPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnDragPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnDragPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnDragPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    public MaterialChipKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public MaterialChipVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public bool ShowIcon { get => GetValue(ShowIconProperty); set => SetValue(ShowIconProperty, value); }
    public bool ShowTrailingIcon { get => GetValue(ShowTrailingIconProperty); set => SetValue(ShowTrailingIconProperty, value); }
    public bool IsRemovable { get => GetValue(IsRemovableProperty); set => SetValue(IsRemovableProperty, value); }
    public string RemoveButtonAccessibleName { get => GetValue(RemoveButtonAccessibleNameProperty); set => SetValue(RemoveButtonAccessibleNameProperty, value); }
    public bool IsDraggable { get => GetValue(IsDraggableProperty); set => SetValue(IsDraggableProperty, value); }

    /// <summary>True while a drag gesture has crossed the pointer threshold.</summary>
    public bool IsDragging
    {
        get => _isDragging;
        private set => SetAndRaise(IsDraggingProperty, ref _isDragging, value);
    }

    /// <summary>Whether this chip family exposes selection.</summary>
    public bool IsSelectable => Kind != MaterialChipKind.Assist;

    /// <summary>Whether the closing icon is actually shown for the current family.</summary>
    public bool CanRemove => IsRemovable && Kind == MaterialChipKind.Input;

    public event EventHandler<RoutedEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    private bool CoerceSelection(bool value) => Kind == MaterialChipKind.Assist ? false : value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            CoerceValue(IsSelectedProperty);
            UpdateChipPseudoClasses();
        }
        else if (change.Property == VariantProperty || change.Property == IconProperty ||
                 change.Property == AvatarProperty || change.Property == ShowIconProperty ||
                 change.Property == ShowTrailingIconProperty || change.Property == IsRemovableProperty)
        {
            UpdateChipPseudoClasses();
        }
        else if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
            _peer?.NotifySelectionChanged((bool)(change.OldValue ?? false), IsSelected);
        }
        else if (change.Property == RemoveButtonAccessibleNameProperty)
        {
            UpdateRemoveButtonName();
        }
    }

    private void UpdateChipPseudoClasses()
    {
        PseudoClasses.Set(":assist", Kind == MaterialChipKind.Assist);
        PseudoClasses.Set(":filter", Kind == MaterialChipKind.Filter);
        PseudoClasses.Set(":input", Kind == MaterialChipKind.Input);
        PseudoClasses.Set(":suggestion", Kind == MaterialChipKind.Suggestion);
        // Input chips have no Elevated variant in the source export.
        var elevated = Kind != MaterialChipKind.Input && Variant == MaterialChipVariant.Elevated;
        PseudoClasses.Set(":elevated", elevated);
        PseudoClasses.Set(":outlined", !elevated);
        var hasAvatar = Kind == MaterialChipKind.Input && Avatar is not null;
        PseudoClasses.Set(":has-avatar", hasAvatar);
        PseudoClasses.Set(":has-icon", !hasAvatar && ShowIcon && Icon is not null);
        PseudoClasses.Set(":trailing", Kind == MaterialChipKind.Filter && ShowTrailingIcon);
        PseudoClasses.Set(":removable", CanRemove);
        PseudoClasses.Set(":selected", IsSelected);
    }

    protected override void OnClick()
    {
        // A completed drag is not an activation.
        if (_suppressClick)
        {
            _suppressClick = false;
            return;
        }
        if (IsSelectable)
            IsSelected = !IsSelected;
        base.OnClick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (CanRemove && e.Key is Key.Delete or Key.Back)
        {
            RequestRemove();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_removeButton is not null)
            _removeButton.Click -= OnRemoveButtonClick;
        base.OnApplyTemplate(e);
        _removeButton = e.NameScope.Find<Button>("PART_RemoveButton");
        if (_removeButton is not null)
        {
            _removeButton.Click += OnRemoveButtonClick;
            _removeButton.Focusable = false;
            KeyboardNavigation.SetIsTabStop(_removeButton, false);
            UpdateRemoveButtonName();
        }
    }

    private void OnRemoveButtonClick(object? sender, RoutedEventArgs e)
    {
        RequestRemove();
        e.Handled = true;
    }

    private void UpdateRemoveButtonName()
    {
        if (_removeButton is not null)
            AutomationProperties.SetName(_removeButton, RemoveButtonAccessibleName);
    }

    /// <summary>Raises <see cref="RemoveRequestedEvent"/> for this chip.</summary>
    public void RequestRemove()
    {
        if (!CanRemove)
            return;
        RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));
    }

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _suppressClick = false;
        if (!IsDraggable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _pointerDown = true;
        _pointerOrigin = e.GetPosition(this);
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pointerDown || IsDragging || !IsDraggable)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndDrag();
            return;
        }
        var position = e.GetPosition(this);
        var dx = position.X - _pointerOrigin.X;
        var dy = position.Y - _pointerOrigin.Y;
        if (dx * dx + dy * dy >= DragThreshold * DragThreshold)
        {
            IsDragging = true;
            PseudoClasses.Set(":dragged", true);
        }
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsDragging)
            _suppressClick = true;
        EndDrag();
    }

    private void OnDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndDrag();
        base.OnDetachedFromVisualTree(e);
    }

    private void EndDrag()
    {
        _pointerDown = false;
        if (!IsDragging)
            return;
        IsDragging = false;
        PseudoClasses.Set(":dragged", false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new MaterialChipAutomationPeer(this);

    private sealed class MaterialChipAutomationPeer : ButtonAutomationPeer, ISelectionItemProvider, IToggleProvider
    {
        private readonly MaterialChip _chip;

        public MaterialChipAutomationPeer(MaterialChip owner) : base(owner) => _chip = owner;

        protected override string GetClassNameCore() => nameof(MaterialChip);

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(_chip);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            return _chip.Content as string ?? base.GetNameCore();
        }

        public bool IsSelected => _chip.IsSelected;

        public ISelectionProvider SelectionContainer
        {
            get
            {
                var group = _chip.FindAncestorOfType<MaterialChipGroup>();
                return (ControlAutomationPeer.CreatePeerForElement(group!) as ISelectionProvider)!;
            }
        }

        public void AddToSelection()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            _chip.IsSelected = true;
        }

        public void RemoveFromSelection()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            _chip.IsSelected = false;
        }

        public void Select()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            _chip.IsSelected = true;
        }

        public ToggleState ToggleState => _chip.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (!_chip.IsEffectivelyEnabled)
                throw new InvalidOperationException("The chip is disabled.");
            _chip.IsSelected = !_chip.IsSelected;
        }

        public void NotifySelectionChanged(bool oldValue, bool newValue)
        {
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                oldValue ? ToggleState.On : ToggleState.Off,
                newValue ? ToggleState.On : ToggleState.Off);
        }
    }
}
