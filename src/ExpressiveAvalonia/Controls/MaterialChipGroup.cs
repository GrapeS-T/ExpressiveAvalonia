using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExpressiveAvalonia.Controls;

/// <summary>The two chip-group layouts in the source export.</summary>
public enum MaterialChipGroupLayout { SingleRow, MultipleRows }

/// <summary>Selection policy enforced by a <see cref="MaterialChipGroup"/>.</summary>
public enum MaterialChipSelectionMode { None, Single, Multiple }

/// <summary>Describes chips added to and removed from a group selection.</summary>
public sealed class MaterialChipSelectionChangedEventArgs : RoutedEventArgs
{
    public MaterialChipSelectionChangedEventArgs(RoutedEvent routedEvent,
        IReadOnlyList<MaterialChip> addedItems, IReadOnlyList<MaterialChip> removedItems) : base(routedEvent)
    {
        AddedItems = addedItems;
        RemovedItems = removedItems;
    }

    public IReadOnlyList<MaterialChip> AddedItems { get; }
    public IReadOnlyList<MaterialChip> RemovedItems { get; }
}

/// <summary>Describes a chip that requested removal.</summary>
public sealed class MaterialChipRemovedEventArgs : EventArgs
{
    public MaterialChipRemovedEventArgs(MaterialChip chip) => Chip = chip;
    public MaterialChip Chip { get; }
}

/// <summary>A single-row scrollable or wrapping multi-row Material chip group.</summary>
/// <remarks>
/// <para>
/// The group owns layout, selection policy, roving focus and removal for direct
/// <see cref="MaterialChip"/> items. Selection mode <see cref="MaterialChipSelectionMode.None"/>
/// clears child selection; <see cref="MaterialChipSelectionMode.Single"/> keeps one chip selected;
/// <see cref="MaterialChipSelectionMode.Multiple"/> allows any number.
/// </para>
/// <para>
/// Arrow keys move focus (Left/Right wrap in reading order, Up/Down use the nearest chip in that
/// direction so wrapping rows behave naturally), Home/End jump to the ends, and Space/Enter are
/// handled by the focused chip itself. Delete/Backspace requests removal of a removable input chip.
/// Tab enters and leaves the group as one stop; the focused or selected chip is the tab stop.
/// </para>
/// <para>
/// When <see cref="ItemsControl.ItemsSource"/> is null the group removes the chip from
/// <see cref="ItemsControl.Items"/> itself. With a bound source the group raises
/// <see cref="MaterialChip.RemoveRequestedEvent"/> and <see cref="ChipRemoved"/> and leaves data
/// ownership to the application. Data-templated containers are not chip instances, so selection
/// enforcement applies to chips supplied directly or as item controls.
/// </para>
/// </remarks>
public class MaterialChipGroup : ItemsControl
{
    public static readonly StyledProperty<MaterialChipGroupLayout> LayoutProperty =
        AvaloniaProperty.Register<MaterialChipGroup, MaterialChipGroupLayout>(nameof(Layout));

    public static readonly StyledProperty<MaterialChipSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<MaterialChipGroup, MaterialChipSelectionMode>(nameof(SelectionMode),
            MaterialChipSelectionMode.Multiple);

    /// <summary>Family applied to chips generated for non-<see cref="MaterialChip"/> items.</summary>
    public static readonly StyledProperty<MaterialChipKind> ContainerKindProperty =
        AvaloniaProperty.Register<MaterialChipGroup, MaterialChipKind>(nameof(ContainerKind),
            MaterialChipKind.Suggestion);

    public static readonly RoutedEvent<MaterialChipSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MaterialChipGroup, MaterialChipSelectionChangedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Bubble);

    private readonly List<MaterialChip> _chips = [];
    private readonly HashSet<MaterialChip> _selected = [];
    private readonly HashSet<MaterialChip> _registered = [];
    private bool _syncing;

    public MaterialChipGroup()
    {
        // One tab stop for the group; arrows and Home/End move focus between chips.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        // Tunnel so the surrounding ScrollViewer does not consume arrow keys for scrolling
        // before the roving-focus handler sees them.
        AddHandler(KeyDownEvent, OnGroupKeyDown, RoutingStrategies.Tunnel);
        AddHandler(MaterialChip.RemoveRequestedEvent, OnChipRemoveRequested);
        // Direct chip items are their own containers, so container callbacks alone are not a
        // reliable registration signal; mirror the item collection instead.
        Items.CollectionChanged += OnItemsChanged;
        UpdatePseudoClasses();
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        SyncChipsFromItems();
    }

    private void SyncChipsFromItems()
    {
        // ItemsSource containers are registered by the container callbacks; the item collection
        // only mirrors chips supplied directly (which are their own containers).
        if (ItemsSource is not null)
            return;
        var chips = Items.OfType<MaterialChip>().ToList();
        foreach (var chip in _chips.Where(chip => !chips.Contains(chip)).ToList())
            UnregisterChip(chip);
        _chips.Clear();
        _chips.AddRange(chips);
        foreach (var chip in chips)
            RegisterChip(chip);
        SyncSelection();
        UpdateTabStops();
    }

    public MaterialChipGroupLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public MaterialChipSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>
    /// Family used for containers generated from non-chip items. Set this to
    /// <see cref="MaterialChipKind.Assist"/> for action-only data, or to
    /// <see cref="MaterialChipKind.Input"/> to let data chips request removal.
    /// </summary>
    public MaterialChipKind ContainerKind { get => GetValue(ContainerKindProperty); set => SetValue(ContainerKindProperty, value); }

    /// <summary>Raised after the selection set changes, with the chips that entered and left it.</summary>
    public event EventHandler<MaterialChipSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Raised when a chip asks to be removed, before direct-item removal.</summary>
    public event EventHandler<MaterialChipRemovedEventArgs>? ChipRemoved;

    /// <summary>The chips currently hosted by the group, in item order.</summary>
    public IReadOnlyList<MaterialChip> Chips => _chips;

    /// <summary>The chips whose <see cref="MaterialChip.IsSelected"/> is true.</summary>
    public IReadOnlyList<MaterialChip> SelectedChips => _chips.Where(chip => chip.IsSelected).ToList();

    /// <summary>Selects or clears a chip while honoring <see cref="SelectionMode"/>.</summary>
    public void SelectChip(MaterialChip chip, bool selected = true)
    {
        ArgumentNullException.ThrowIfNull(chip);
        if (!_registered.Contains(chip))
            throw new ArgumentException("The chip does not belong to this group.", nameof(chip));
        chip.IsSelected = selected;
    }

    /// <summary>Clears every chip selection in the group.</summary>
    public void ClearSelection()
    {
        _syncing = true;
        foreach (var chip in _chips)
            chip.IsSelected = false;
        _syncing = false;
        SyncSelection();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == SelectionModeProperty)
        {
            UpdatePseudoClasses();
            ApplySelectionMode();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            SyncChipsFromItems();
        }
        else if (change.Property == ContainerKindProperty)
        {
            ApplyContainerKind();
        }
    }

    private void ApplyContainerKind()
    {
        foreach (var chip in _chips)
        {
            if (ItemFromContainer(chip) is MaterialChip)
                continue;
            chip.Kind = ContainerKind;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":single-row", Layout == MaterialChipGroupLayout.SingleRow);
        PseudoClasses.Set(":multiple-rows", Layout == MaterialChipGroupLayout.MultipleRows);
        PseudoClasses.Set(":none-select", SelectionMode == MaterialChipSelectionMode.None);
        PseudoClasses.Set(":single-select", SelectionMode == MaterialChipSelectionMode.Single);
        PseudoClasses.Set(":multi-select", SelectionMode == MaterialChipSelectionMode.Multiple);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not MaterialChip chip)
            return;
        if (!_chips.Contains(chip))
            _chips.Insert(Math.Min(index, _chips.Count), chip);
        // Data-generated containers inherit the group's family; self-container chips keep theirs.
        if (item is not MaterialChip)
            chip.Kind = ContainerKind;
        RegisterChip(chip);
        SyncSelection();
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is MaterialChip chip)
        {
            UnregisterChip(chip);
            _chips.Remove(chip);
            UpdateTabStops();
        }
        base.ClearContainerForItemOverride(container);
    }

    /// <summary>Material chips are their own containers in both direct and bound item modes.</summary>
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<MaterialChip>(item, out recycleKey);

    /// <summary>Data items become chips of <see cref="ContainerKind"/> with the item as content.</summary>
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new MaterialChip { Kind = ContainerKind };

    /// <summary>Subscribes a chip; the ordered <c>_chips</c> list is maintained by the callers.</summary>
    private void RegisterChip(MaterialChip chip)
    {
        if (!_registered.Add(chip))
            return;
        chip.PropertyChanged += OnChipPropertyChanged;
        chip.GotFocus += OnChipGotFocus;

        if (chip.IsSelected)
        {
            if (SelectionMode == MaterialChipSelectionMode.None)
            {
                chip.IsSelected = false;
            }
            else if (SelectionMode == MaterialChipSelectionMode.Single)
            {
                _syncing = true;
                foreach (var other in _chips)
                {
                    if (!ReferenceEquals(other, chip) && other.IsSelected)
                        other.IsSelected = false;
                }
                _syncing = false;
            }
        }
    }

    private void UnregisterChip(MaterialChip chip)
    {
        if (!_registered.Remove(chip))
            return;
        chip.PropertyChanged -= OnChipPropertyChanged;
        chip.GotFocus -= OnChipGotFocus;
        _chips.Remove(chip);
        if (_selected.Remove(chip))
            RaiseEvent(new MaterialChipSelectionChangedEventArgs(SelectionChangedEvent, [], [chip]));
    }

    private void OnChipPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MaterialChip.IsSelectedProperty || _syncing || sender is not MaterialChip chip)
            return;

        if (chip.IsSelected)
        {
            if (SelectionMode == MaterialChipSelectionMode.None)
            {
                _syncing = true;
                chip.IsSelected = false;
                _syncing = false;
                SyncSelection();
                return;
            }
            if (SelectionMode == MaterialChipSelectionMode.Single)
            {
                _syncing = true;
                foreach (var other in _chips)
                {
                    if (!ReferenceEquals(other, chip) && other.IsSelected)
                        other.IsSelected = false;
                }
                _syncing = false;
            }
        }
        SyncSelection();
    }

    private void ApplySelectionMode()
    {
        if (SelectionMode == MaterialChipSelectionMode.None)
        {
            _syncing = true;
            foreach (var chip in _chips)
                chip.IsSelected = false;
            _syncing = false;
        }
        else if (SelectionMode == MaterialChipSelectionMode.Single)
        {
            _syncing = true;
            var keep = _chips.FirstOrDefault(chip => chip.IsSelected);
            foreach (var chip in _chips)
            {
                if (!ReferenceEquals(chip, keep))
                    chip.IsSelected = false;
            }
            _syncing = false;
        }
        SyncSelection();
    }

    private void SyncSelection()
    {
        var current = _chips.Where(chip => chip.IsSelected).ToList();
        var added = current.Where(chip => !_selected.Contains(chip)).ToList();
        var removed = _selected.Where(chip => !current.Contains(chip)).ToList();
        if (added.Count == 0 && removed.Count == 0)
        {
            UpdateTabStops();
            return;
        }
        _selected.Clear();
        foreach (var chip in current)
            _selected.Add(chip);
        RaiseEvent(new MaterialChipSelectionChangedEventArgs(SelectionChangedEvent, added, removed));
        UpdateTabStops();
    }

    private void OnChipGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is MaterialChip chip)
        {
            SetRovingTabStop(chip);
            chip.BringIntoView();
        }
    }

    private void UpdateTabStops()
    {
        if (_chips.Count == 0)
            return;
        var focused = _chips.FirstOrDefault(chip => chip.IsFocused);
        var selected = _chips.FirstOrDefault(chip => chip.IsSelected && chip.IsEffectivelyEnabled);
        var first = _chips.FirstOrDefault(chip => chip.IsEffectivelyEnabled);
        SetRovingTabStop(focused ?? selected ?? first);
    }

    private void SetRovingTabStop(MaterialChip? tabStop)
    {
        foreach (var chip in _chips)
            KeyboardNavigation.SetIsTabStop(chip, ReferenceEquals(chip, tabStop));
        // TabNavigation.Once forwards tab focus to this element, so the group is one tab stop.
        KeyboardNavigation.SetTabOnceActiveElement(this, tabStop!);
    }

    private void OnGroupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        var chips = _chips.Where(chip => chip.IsEffectivelyEnabled).ToList();
        if (chips.Count == 0)
            return;
        var focused = chips.FirstOrDefault(chip => chip.IsFocused);
        if (focused is null)
            return;

        switch (e.Key)
        {
            case Key.Left:
                MoveLinear(chips, focused, FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveLinear(chips, focused, FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? -1 : 1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSpatial(focused, -1);
                e.Handled = true;
                break;
            case Key.Down:
                MoveSpatial(focused, 1);
                e.Handled = true;
                break;
            case Key.Home:
                FocusChip(chips[0]);
                e.Handled = true;
                break;
            case Key.End:
                FocusChip(chips[^1]);
                e.Handled = true;
                break;
        }
    }

    private static void MoveLinear(List<MaterialChip> chips, MaterialChip from, int delta)
    {
        var index = chips.IndexOf(from);
        if (index < 0)
            return;
        var next = (index + delta + chips.Count) % chips.Count;
        FocusChip(chips[next]);
    }

    private void MoveSpatial(MaterialChip from, int direction)
    {
        var origin = CenterRelativeToGroup(from);
        if (origin is null)
            return;

        MaterialChip? best = null;
        var bestScore = double.MaxValue;
        foreach (var chip in _chips)
        {
            if (ReferenceEquals(chip, from) || !chip.IsEffectivelyEnabled)
                continue;
            var center = CenterRelativeToGroup(chip);
            if (center is null)
                continue;
            var dy = center.Value.Y - origin.Value.Y;
            if (direction < 0 ? dy >= -0.5 : dy <= 0.5)
                continue;
            var dx = Math.Abs(center.Value.X - origin.Value.X);
            var score = Math.Abs(dy) * 1000 + dx;
            if (score < bestScore)
            {
                bestScore = score;
                best = chip;
            }
        }
        if (best is not null)
            FocusChip(best);
    }

    private Point? CenterRelativeToGroup(MaterialChip chip) =>
        chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), this);

    private static void FocusChip(MaterialChip chip)
    {
        chip.Focus(NavigationMethod.Directional);
        chip.BringIntoView();
    }

    private void OnChipRemoveRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not MaterialChip chip || !_chips.Contains(chip))
            return;
        ChipRemoved?.Invoke(this, new MaterialChipRemovedEventArgs(chip));
        if (ItemsSource is not null || !Items.Contains(chip))
            return;
        var index = _chips.IndexOf(chip);
        var hadFocus = chip.IsFocused || IsKeyboardFocusWithin;
        Items.Remove(chip);
        e.Handled = true;
        // Removing the focused chip must not drop keyboard focus into the window chrome.
        if (hadFocus && _chips.Count > 0)
        {
            var next = _chips[Math.Min(Math.Max(index, 0), _chips.Count - 1)];
            SetRovingTabStop(next);
            FocusChip(next);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new MaterialChipGroupAutomationPeer(this);

    private sealed class MaterialChipGroupAutomationPeer(MaterialChipGroup owner)
        : ItemsControlAutomationPeer(owner), ISelectionProvider
    {
        private readonly MaterialChipGroup _group = owner;

        protected override string GetClassNameCore() => nameof(MaterialChipGroup);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        public bool CanSelectMultiple => _group.SelectionMode == MaterialChipSelectionMode.Multiple;
        public bool IsSelectionRequired => false;

        public IReadOnlyList<AutomationPeer> GetSelection()
        {
            var peers = new List<AutomationPeer>(_group.SelectedChips.Count);
            foreach (var chip in _group.SelectedChips)
            {
                if (ControlAutomationPeer.CreatePeerForElement(chip) is { } peer)
                    peers.Add(peer);
            }
            return peers;
        }
    }
}
