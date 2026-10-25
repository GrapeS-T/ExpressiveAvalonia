using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Shared action, overflow and keyboard plumbing for <see cref="MaterialAppBar"/>,
/// <see cref="MaterialBottomAppBar"/> and <see cref="MaterialToolbar"/>.
/// </summary>
/// <remarks>
/// Each bar renders the first <see cref="MaxVisibleActions"/> actions inline as icon buttons and
/// moves the remainder into a flyout opened by the template's overflow button. The flyout also
/// hosts <see cref="OverflowContent"/>, so an application can add arbitrary content such as a
/// profile card or a slider next to the generated commands. Arrow, Home and End keys move focus
/// between the action buttons; Escape closes the overflow flyout and returns focus to its button.
/// </remarks>
public abstract class MaterialBarBase : TemplatedControl
{
    /// <summary>Maximum number of actions rendered inline; the rest are moved into the overflow.</summary>
    public static readonly StyledProperty<int> MaxVisibleActionsProperty =
        AvaloniaProperty.Register<MaterialBarBase, int>(
            nameof(MaxVisibleActions), 3, validate: value => value >= 0);

    /// <summary>Arbitrary content appended to the overflow flyout below the generated commands.</summary>
    public static readonly StyledProperty<object?> OverflowContentProperty =
        AvaloniaProperty.Register<MaterialBarBase, object?>(nameof(OverflowContent));

    /// <summary>Where the overflow flyout opens relative to the overflow button.</summary>
    public static readonly StyledProperty<PlacementMode> OverflowPlacementProperty =
        AvaloniaProperty.Register<MaterialBarBase, PlacementMode>(
            nameof(OverflowPlacement), PlacementMode.BottomEdgeAlignedRight);

    public static readonly DirectProperty<MaterialBarBase, IReadOnlyList<MaterialBarAction>> VisibleActionsProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarBase, IReadOnlyList<MaterialBarAction>>(
            nameof(VisibleActions), bar => bar.VisibleActions, (bar, value) => bar.VisibleActions = value);

    public static readonly DirectProperty<MaterialBarBase, bool> HasOverflowProperty =
        AvaloniaProperty.RegisterDirect<MaterialBarBase, bool>(
            nameof(HasOverflow), bar => bar.HasOverflow, (bar, value) => bar.HasOverflow = value);

    private readonly AvaloniaList<MaterialBarAction> _actions = new();
    private readonly AvaloniaList<MaterialBarAction> _visible = new();
    private readonly List<MaterialBarAction> _hidden = new();
    private IReadOnlyList<MaterialBarAction> _visibleActions;
    private bool _hasOverflow;
    private ItemsControl? _actionsHost;
    private Button? _overflowButton;
    private Flyout? _overflowFlyout;

    protected MaterialBarBase()
    {
        _visibleActions = _visible;
        _actions.CollectionChanged += (_, _) => UpdateOverflow();
    }

    static MaterialBarBase()
    {
        MaxVisibleActionsProperty.Changed.AddClassHandler<MaterialBarBase>((bar, _) => bar.UpdateOverflow());
        OverflowContentProperty.Changed.AddClassHandler<MaterialBarBase>((bar, _) => bar.UpdateOverflow());
    }

    /// <summary>The actions in declaration order. Mutations are observed immediately.</summary>
    public IList<MaterialBarAction> Actions => _actions;

    /// <summary>The actions currently rendered inline, in declaration order.</summary>
    public IReadOnlyList<MaterialBarAction> VisibleActions
    {
        get => _visibleActions;
        private set => SetAndRaise(VisibleActionsProperty, ref _visibleActions, value);
    }

    /// <summary>True when at least one action is hidden or <see cref="OverflowContent"/> is set.</summary>
    public bool HasOverflow
    {
        get => _hasOverflow;
        private set => SetAndRaise(HasOverflowProperty, ref _hasOverflow, value);
    }

    public int MaxVisibleActions
    {
        get => GetValue(MaxVisibleActionsProperty);
        set => SetValue(MaxVisibleActionsProperty, value);
    }

    public object? OverflowContent
    {
        get => GetValue(OverflowContentProperty);
        set => SetValue(OverflowContentProperty, value);
    }

    public PlacementMode OverflowPlacement
    {
        get => GetValue(OverflowPlacementProperty);
        set => SetValue(OverflowPlacementProperty, value);
    }

    /// <summary>Opens the overflow flyout on the template's overflow button.</summary>
    public void ShowOverflow()
    {
        if (_overflowButton is null || !_overflowButton.IsAttachedToVisualTree())
            return;

        CloseOverflow(restoreFocus: false);

        var panel = new StackPanel { Spacing = 2, MinWidth = 200 };
        foreach (var action in _hidden)
            panel.Children.Add(CreateOverflowItem(action, () => CloseOverflow(restoreFocus: true)));
        panel.KeyDown += (_, args) => OnOverflowKeyDown(panel, args);

        if (OverflowContent is { } content)
        {
            panel.Children.Add(new ContentPresenter
            {
                Content = content,
                Margin = new Thickness(12, 8, 12, 4)
            });
        }

        var flyout = new Flyout
        {
            Content = panel,
            Placement = GetOverflowPlacement(),
            OverlayDismissEventPassThrough = false
        };
        if (this.TryFindResource("MaterialBarOverflowPresenter", out var presenter) && presenter is ControlTheme theme)
            flyout.FlyoutPresenterTheme = theme;

        _overflowFlyout = flyout;
        flyout.ShowAt(_overflowButton);
        // The popup root takes focus when it opens; move it to the first command so the menu is
        // reachable without another Tab.
        Dispatcher.UIThread.Post(() =>
        {
            if (flyout.IsOpen && panel.Children.Count > 0)
                panel.Children.OfType<Button>().FirstOrDefault()?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes the overflow flyout and restores focus to the overflow button.</summary>
    public void HideOverflow() => CloseOverflow(restoreFocus: true);

    private void CloseOverflow(bool restoreFocus)
    {
        var flyout = _overflowFlyout;
        _overflowFlyout = null;
        if (flyout is null)
            return;
        flyout.Hide();
        if (!restoreFocus)
            return;
        if (_overflowButton is { IsEffectivelyEnabled: true, IsVisible: true } button && button.IsAttachedToVisualTree())
            button.Focus(NavigationMethod.Directional);
    }

    protected virtual PlacementMode GetOverflowPlacement() => OverflowPlacement;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_overflowButton is not null)
            _overflowButton.Click -= OnOverflowButtonClick;

        _actionsHost = e.NameScope.Find<ItemsControl>("PART_Actions");
        _overflowButton = e.NameScope.Find<Button>("PART_OverflowButton");
        if (_overflowButton is not null)
            _overflowButton.Click += OnOverflowButtonClick;

        UpdateOverflow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _overflowFlyout?.Hide();
        _overflowFlyout = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.Escape && _overflowFlyout is { IsOpen: true })
        {
            HideOverflow();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
            return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End))
            return;

        var buttons = FocusableButtons();
        if (buttons.Count == 0)
            return;

        var source = e.Source as Visual;
        var index = -1;
        for (var i = 0; i < buttons.Count; i++)
        {
            if (ReferenceEquals(buttons[i], source) || (source is not null && buttons[i].IsVisualAncestorOf(source)))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;

        int next;
        if (e.Key == Key.Home)
            next = 0;
        else if (e.Key == Key.End)
            next = buttons.Count - 1;
        else
        {
            var forward = e.Key is Key.Right or Key.Down;
            next = (index + (forward ? 1 : -1) + buttons.Count) % buttons.Count;
        }

        buttons[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnOverflowKeyDown(Panel panel, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Escape)
        {
            CloseOverflow(restoreFocus: true);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
            return;

        var items = new List<Button>();
        foreach (var child in panel.Children)
        {
            if (child is Button { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } button)
                items.Add(button);
        }
        if (items.Count == 0)
            return;

        var source = e.Source as Visual;
        var index = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], source) || (source is not null && items[i].IsVisualAncestorOf(source)))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return;

        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => items.Count - 1,
            Key.Down or Key.Right => (index + 1) % items.Count,
            Key.Up or Key.Left => (index - 1 + items.Count) % items.Count,
            _ => -1
        };
        if (next < 0)
            return;

        items[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnOverflowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_overflowFlyout is { IsOpen: true })
            HideOverflow();
        else
            ShowOverflow();
    }

    private void UpdateOverflow()
    {
        var limit = MaxVisibleActions;
        _visible.Clear();
        _hidden.Clear();
        var index = 0;
        foreach (var action in _actions)
        {
            if (index++ < limit)
                _visible.Add(action);
            else
                _hidden.Add(action);
        }

        HasOverflow = _hidden.Count > 0 || OverflowContent is not null;
        if (!HasOverflow)
        {
            _overflowFlyout?.Hide();
            _overflowFlyout = null;
        }
    }

    private List<Button> FocusableButtons()
    {
        var buttons = new List<Button>();
        if (_actionsHost is not null)
        {
            foreach (var button in _actionsHost.GetVisualDescendants().OfType<Button>())
            {
                if (button.Focusable && button.IsEffectivelyEnabled && button.IsEffectivelyVisible)
                    buttons.Add(button);
            }
        }
        if (_overflowButton is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } overflow)
            buttons.Add(overflow);
        return buttons;
    }

    private static Button CreateOverflowItem(MaterialBarAction action, Action close)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (action.Icon is not null)
            content.Children.Add(new PathIcon { Data = action.Icon, Width = 20, Height = 20 });
        if (!string.IsNullOrEmpty(action.Text))
            content.Children.Add(new TextBlock { Text = action.Text, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = content,
            Command = action.Command,
            CommandParameter = action.CommandParameter,
            IsEnabled = action.IsEnabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 48,
            Padding = new Thickness(12, 0),
            Background = Brushes.Transparent
        };
        if (action.ToolTip is { } tip)
            ToolTip.SetTip(button, tip);
        // Text treatment keeps the neutral label color while reusing the shared state layer,
        // focus ring and disabled composition from the button theme.
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        AutomationProperties.SetName(button, action.EffectiveAutomationName ?? action.Text ?? string.Empty);
        button.Click += (_, _) => close();
        return button;
    }
}
