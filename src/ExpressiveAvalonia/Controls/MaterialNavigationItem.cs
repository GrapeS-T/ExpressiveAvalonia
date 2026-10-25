using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>A single selectable destination inside a <see cref="MaterialNavigationDrawer"/>.</summary>
/// <remarks>
/// The anatomy matches the source building block: a 56 DIP full-radius pill with a 24 DIP icon
/// slot, a label and a trailing badge label. Content is the label and keeps access-key support;
/// <see cref="Icon"/> accepts a <see cref="MaterialIcon"/>, a <see cref="PathIcon"/> or any 24 DIP
/// icon content, and <see cref="BadgeText"/> renders the trailing label.
/// Selection is single-choice per drawer. Pointer, Space and Enter activation select the item;
/// setting <see cref="IsSelected"/> directly also routes through the owning drawer, so a
/// selection declared in XAML is applied once the item attaches.
/// </remarks>
[PseudoClasses(":selected", ":icon", ":no-icon", ":badge")]
public sealed class MaterialNavigationItem : Button
{
    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MaterialNavigationItem, object?>(nameof(Icon));

    /// <summary>Defines the <see cref="BadgeText"/> property.</summary>
    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<MaterialNavigationItem, string?>(nameof(BadgeText));

    /// <summary>Defines the <see cref="ShowBadge"/> property.</summary>
    public static readonly StyledProperty<bool> ShowBadgeProperty =
        AvaloniaProperty.Register<MaterialNavigationItem, bool>(nameof(ShowBadge), true);

    /// <summary>Defines the <see cref="IsSelected"/> property.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MaterialNavigationItem, bool>(
            nameof(IsSelected), defaultBindingMode: BindingMode.TwoWay);

    private bool _syncingSelection;
    private IDisposable? _iconColorBinding;

    static MaterialNavigationItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<MaterialNavigationItem>(
            static (item, change) => item.OnIsSelectedChanged(change));
        IconProperty.Changed.AddClassHandler<MaterialNavigationItem>(
            static (item, _) =>
            {
                item.UpdatePseudoClasses();
                item.SyncIconColor();
            });
        BadgeTextProperty.Changed.AddClassHandler<MaterialNavigationItem>(
            static (item, _) => item.UpdatePseudoClasses());
        ShowBadgeProperty.Changed.AddClassHandler<MaterialNavigationItem>(
            static (item, _) => item.UpdatePseudoClasses());
    }

    public MaterialNavigationItem() => UpdatePseudoClasses();

    /// <summary>Icon content shown in the leading 24 DIP slot; hidden when null.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Trailing badge label, for example an unread count.</summary>
    public string? BadgeText
    {
        get => GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    /// <summary>Whether <see cref="BadgeText"/> is shown. Defaults to true.</summary>
    public bool ShowBadge
    {
        get => GetValue(ShowBadgeProperty);
        set => SetValue(ShowBadgeProperty, value);
    }

    /// <summary>Whether this item is the selected destination of its drawer.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>The drawer that owns this item, or null when it is not inside one.</summary>
    internal MaterialNavigationDrawer? Drawer { get; set; }

    /// <summary>Applies selection without routing back through the drawer.</summary>
    internal void SetSelected(bool value)
    {
        _syncingSelection = true;
        try
        {
            SetCurrentValue(IsSelectedProperty, value);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    protected override void OnClick()
    {
        // Selection happens before the Click event so handlers and Command observers see the
        // item as the current destination, mirroring native Material drawers. An item used
        // outside a drawer keeps its own selected state.
        if (Drawer is { } drawer)
            drawer.Select(this);
        else
            SetSelected(true);

        base.OnClick();
    }

    // Items are usually direct children of the drawer, but they can also come from an
    // ItemTemplate whose generated container has no logical parent, so the visual tree is the
    // reliable way to find the owning drawer.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachToDrawer();
        SyncIconColor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachFromDrawer();
        _iconColorBinding?.Dispose();
        _iconColorBinding = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachToDrawer()
    {
        if (Drawer is not null)
            return;

        var drawer = this.GetVisualAncestors().OfType<MaterialNavigationDrawer>().FirstOrDefault();
        if (drawer is null)
            return;

        Drawer = drawer;
        drawer.Register(this);
    }

    private void DetachFromDrawer()
    {
        Drawer?.Unregister(this);
        Drawer = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MaterialNavigationItemAutomationPeer(this);

    private void OnIsSelectedChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var selected = change.GetNewValue<bool>();
        PseudoClasses.Set(":selected", selected);
        if (ControlAutomationPeer.FromElement(this) is MaterialNavigationItemAutomationPeer peer)
            peer.NotifySelectionChanged(selected);

        if (_syncingSelection)
            return;

        if (selected)
        {
            Drawer?.Select(this);
        }
        else if (Drawer is { } drawer && ReferenceEquals(drawer.SelectedItem, this))
        {
            drawer.ClearSelection();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":icon", Icon is not null);
        PseudoClasses.Set(":no-icon", Icon is null);
        PseudoClasses.Set(":badge", ShowBadge && !string.IsNullOrEmpty(BadgeText));
        PseudoClasses.Set(":selected", IsSelected);
    }

    /// <summary>
    /// Icon controls such as <see cref="MaterialIcon"/> set their own theme foreground, which
    /// would otherwise ignore the item's content color in the selected, hover and disabled
    /// states. A local binding wins over theme setters and follows every state change.
    /// </summary>
    private void SyncIconColor()
    {
        _iconColorBinding?.Dispose();
        _iconColorBinding = null;

        // TextBlock content inherits TextElement.Foreground, so only controls with their own
        // theme foreground need the explicit binding.
        if (Icon is TemplatedControl icon)
            _iconColorBinding = icon.Bind(TemplatedControl.ForegroundProperty,
                this.GetObservable(ForegroundProperty));
    }

    private sealed class MaterialNavigationItemAutomationPeer(MaterialNavigationItem owner)
        : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        public new MaterialNavigationItem Owner => (MaterialNavigationItem)base.Owner;

        public bool IsSelected => Owner.IsSelected;

        public ISelectionProvider? SelectionContainer => Owner.Drawer is { } drawer
            ? CreatePeerForElement(drawer) as ISelectionProvider
            : null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            if (Owner.Drawer is { } drawer && ReferenceEquals(drawer.SelectedItem, Owner))
                drawer.ClearSelection();
            else
                Owner.SetSelected(false);
        }

        public void Select()
        {
            if (Owner.Drawer is { } drawer)
                drawer.Select(Owner);
            else
                Owner.SetSelected(true);
        }

        internal void NotifySelectionChanged(bool selected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !selected, selected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetItemStatusCore() =>
            Owner.ShowBadge && !string.IsNullOrEmpty(Owner.BadgeText)
                ? Owner.BadgeText
                : base.GetItemStatusCore();
    }
}
