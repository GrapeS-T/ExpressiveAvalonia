using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>The four source states of a swipe list item.</summary>
public enum MaterialSwipeState
{
    /// <summary>Nothing is revealed; the row covers the action rail.</summary>
    Default,

    /// <summary>The source "Initiate reveal" peek: narrow 8 DIP action bars are visible.</summary>
    InitiateReveal,

    /// <summary>The source "Revealed" state: every action is a 48 DIP circle.</summary>
    Revealed,

    /// <summary>The source "Swipe action" state: the primary action grows into a 120 DIP pill.</summary>
    SwipeAction,
}

/// <summary>One action revealed behind a <see cref="MaterialSwipeListItem"/>.</summary>
/// <remarks>
/// <see cref="Icon"/> is drawn as a 24 DIP path. <see cref="Label"/> is the accessible name of
/// the action button and the text of the equivalent context-menu command; the source swipe rail
/// shows icons only. A null <see cref="Background"/> uses the primary role for the primary action
/// and the secondary-container role for the others.
/// </remarks>
public sealed class MaterialSwipeAction
{
    /// <summary>Accessible name and context-menu label; not painted on the rail.</summary>
    public string? Label { get; set; }

    /// <summary>Icon geometry painted at 24 DIP.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>Optional override for the action container.</summary>
    public IBrush? Background { get; set; }

    /// <summary>Optional override for the icon color.</summary>
    public IBrush? Foreground { get; set; }

    /// <summary>The command executed when the action is tapped or swiped to completion.</summary>
    public ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    /// <summary>
    /// Marks the action that expands into the 120 DIP swipe-action pill. When no action is
    /// marked, the first action is primary, matching the source Reveal element 01.
    /// </summary>
    public bool IsPrimary { get; set; }
}

/// <summary>Raised when a swipe action is invoked by tap, keyboard or a completed swipe.</summary>
public sealed class MaterialSwipeActionInvokedEventArgs : EventArgs
{
    public MaterialSwipeActionInvokedEventArgs(MaterialSwipeAction action, int index, bool bySwipe)
    {
        Action = action;
        Index = index;
        BySwipe = bySwipe;
    }

    public MaterialSwipeAction Action { get; }
    public int Index { get; }

    /// <summary>True when the row was dragged past <see cref="MaterialSwipeListItem.SwipeActionThreshold"/>.</summary>
    public bool BySwipe { get; }
}

/// <summary>
/// A native list item with the source swipe-to-reveal anatomy.
/// </summary>
/// <remarks>
/// <para>
/// The control derives from <see cref="ListBoxItem"/>, so selection, focus, keyboard activation
/// and the shared Material list anatomy come from the ListBoxItem theme. It adds the source
/// Reveal element rail: one to three actions that start as 8 DIP bars, grow into 48 DIP circles
/// (Revealed) and, when dragged further, expand the primary action into a 120 DIP pill
/// (Swipe action). Drag distance maps continuously to the rail width; release snaps to the
/// nearest state with a spatial spring, and reduced motion jumps straight to the target.
/// </para>
/// <para>
/// Keyboard: Left/Right (reversed in right-to-left flow) reveal and collapse the rail and move
/// focus to the primary action; Escape collapses it. The same actions are always available from
/// the context menu, so assistive technology never needs a gesture.
/// </para>
/// </remarks>
[TemplatePart("PART_SwipeReveal", typeof(Border))]
[TemplatePart("PART_SwipeActions", typeof(Panel))]
[TemplatePart("PART_SwipeContent", typeof(Border))]
[PseudoClasses(":swipe-peek", ":swipe-revealed", ":swipe-action")]
public class MaterialSwipeListItem : ListBoxItem
{
    /// <summary>Fraction of the revealed width the row must travel before the revealed state is kept.</summary>
    public static readonly StyledProperty<double> RevealThresholdProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, double>(nameof(RevealThreshold), 0.5,
            validate: static value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>Fraction of the extra 72 DIP travel that commits the primary swipe action.</summary>
    public static readonly StyledProperty<double> SwipeActionThresholdProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, double>(nameof(SwipeActionThreshold), 0.6,
            validate: static value => double.IsFinite(value) && value is >= 0 and <= 1);

    /// <summary>Two-way revealed state; setting it animates the rail.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, bool>(nameof(IsRevealed),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The current source swipe state. Set by the control; styles may select on it.</summary>
    public static readonly StyledProperty<MaterialSwipeState> SwipeStateProperty =
        AvaloniaProperty.Register<MaterialSwipeListItem, MaterialSwipeState>(nameof(SwipeState));

    private const double ActionSize = 48;
    private const double PeekActionSize = 8;
    private const double ActionGap = 2;
    private const double SwipeActionExtra = 72;

    private readonly AvaloniaList<MaterialSwipeAction> _actions = [];
    private readonly List<Button> _buttons = [];
    private Border? _reveal;
    private Panel? _actionPanel;
    private Border? _content;
    private TranslateTransform? _translate;
    private MaterialMotion.SpringAnimation? _slide;
    private bool _tracking;
    private bool _dragging;
    private bool _updatingState;
    private Point _pressPoint;
    private double _pressOffset;
    private double _offset;
    private ContextMenu? _menu;
    private DispatcherTimer? _settleTimer;

    public MaterialSwipeListItem()
    {
        _actions.CollectionChanged += OnActionsChanged;
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        ActualThemeVariantChanged += (_, _) =>
        {
            if (_actions.Count > 0)
                RebuildActions();
        };
        UpdatePseudoClasses(MaterialSwipeState.Default);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Theme resources (the action button theme and the role brushes) resolve once attached.
        if (_actions.Count > 0)
        {
            RebuildActions();
            UpdateMenu();
        }
    }

    /// <summary>Raised after an action has been executed.</summary>
    public event EventHandler<MaterialSwipeActionInvokedEventArgs>? ActionInvoked;

    /// <summary>Reuses the shared ListBoxItem control theme, including its swipe template parts.</summary>
    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    /// <summary>The reveal rail actions, primary action first. Mutations rebuild the rail.</summary>
    public IList<MaterialSwipeAction> SwipeActions => _actions;

    public double RevealThreshold
    {
        get => GetValue(RevealThresholdProperty);
        set => SetValue(RevealThresholdProperty, value);
    }

    public double SwipeActionThreshold
    {
        get => GetValue(SwipeActionThresholdProperty);
        set => SetValue(SwipeActionThresholdProperty, value);
    }

    public bool IsRevealed
    {
        get => GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    /// <summary>The current source swipe state, exposed for bindings and styles.</summary>
    public MaterialSwipeState SwipeState
    {
        get => GetValue(SwipeStateProperty);
        private set => SetValue(SwipeStateProperty, value);
    }

    private double Sign => FlowDirection == FlowDirection.RightToLeft ? 1 : -1;

    /// <summary>Reveals the rail and moves focus to the primary action.</summary>
    public void Reveal(bool animate = true)
    {
        if (_actions.Count == 0)
            return;
        StopSettleTimer();
        SetState(MaterialSwipeState.Revealed, revealed: true);
        SlideTo(Sign * BaseReveal, animate);
        FocusPrimaryAction();
    }

    /// <summary>Collapses the rail and returns focus to the item when an action held it.</summary>
    public void Collapse(bool animate = true)
    {
        var focusWasInRail = _buttons.Any(button => button.IsFocused);
        SetState(MaterialSwipeState.Default, revealed: false);
        SlideTo(0, animate);
        if (focusWasInRail)
            Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSettleTimer();
        _slide?.Dispose();
        _slide = null;
    }

    /// <summary>Executes an action by index, as if the user tapped it.</summary>
    public void InvokeAction(int index, bool bySwipe = false)
    {
        if (index < 0 || index >= _actions.Count)
            return;
        Invoke(_actions[index], index, bySwipe);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _slide?.Dispose();
        _slide = null;

        _reveal = e.NameScope.Find<Border>("PART_SwipeReveal");
        _actionPanel = e.NameScope.Find<Panel>("PART_SwipeActions");
        _content = e.NameScope.Find<Border>("PART_SwipeContent");
        _translate = new TranslateTransform();
        if (_content is not null)
            _content.RenderTransform = _translate;

        RebuildActions();
        ApplyOffset(Sign * OffsetForState());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRevealedProperty)
        {
            if (_updatingState)
                return;
            if (change.GetNewValue<bool>())
            {
                SetState(MaterialSwipeState.Revealed, revealed: true);
                if (_actions.Count > 0)
                    SlideTo(Sign * BaseReveal, animate: true);
            }
            else if (SwipeState is not MaterialSwipeState.Default)
            {
                SetState(MaterialSwipeState.Default, revealed: false);
                SlideTo(0, animate: true);
            }
        }
        else if (change.Property == FlowDirectionProperty)
        {
            RebuildActions();
            ApplyOffset(Sign * OffsetForState());
        }
        else if (change.Property == IsEnabledProperty && !change.GetNewValue<bool>())
        {
            Collapse(animate: false);
        }
    }

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildActions();
        UpdateMenu();
        if (_actions.Count == 0)
            Collapse(animate: false);
        else
            ApplyOffset(Sign * OffsetForState());
    }

    private void RebuildActions()
    {
        if (_actionPanel is null)
            return;

        foreach (var button in _buttons)
            button.Click -= OnActionClick;
        _buttons.Clear();
        _actionPanel.Children.Clear();

        // The primary action sits at the trailing edge, matching Reveal element 01 in the source.
        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        for (var step = 0; step < _actions.Count; step++)
        {
            var index = rightToLeft ? step : _actions.Count - 1 - step;
            var action = _actions[index];
            var primary = index == PrimaryIndex;
            var button = new Button
            {
                Width = PeekActionSize,
                Height = ActionSize,
                MinWidth = 0,
                MinHeight = 0,
                Padding = default,
                BorderThickness = default,
                CornerRadius = new CornerRadius(ActionSize / 2),
                Background = action.Background ?? ResolveBrush(
                    primary ? "MaterialPrimaryBrush" : "MaterialSecondaryContainerBrush",
                    primary ? Colors.Purple : Colors.Transparent),
                Foreground = action.Foreground ?? ResolveBrush(
                    primary ? "MaterialOnPrimaryBrush" : "MaterialOnSecondaryContainerBrush",
                    Colors.White),
                Content = action.Icon is null ? null : new PathIcon { Data = action.Icon, Width = 24, Height = 24 },
                IsTabStop = false,
                Tag = index,
            };
            button.Theme = this.TryFindResource("MaterialSwipeActionButton", out var theme) ? theme as ControlTheme : null;
            AutomationProperties.SetName(button, action.Label ?? "Action");
            button.Click += OnActionClick;
            _buttons.Add(button);
            _actionPanel.Children.Add(button);
        }

        if (_actionPanel is not null)
        {
            _actionPanel.HorizontalAlignment = rightToLeft
                ? Avalonia.Layout.HorizontalAlignment.Left
                : Avalonia.Layout.HorizontalAlignment.Right;
            _actionPanel.Margin = rightToLeft ? new Thickness(2, 0, 0, 0) : new Thickness(0, 0, 2, 0);
        }

        ApplyOffset(Sign * OffsetForState());
    }

    private void UpdateMenu()
    {
        if (_actions.Count == 0)
        {
            if (_menu is not null)
            {
                ContextMenu = null;
                _menu = null;
            }
            return;
        }

        _menu = new ContextMenu();
        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            var index = i;
            var item = new MenuItem
            {
                Header = action.Label ?? "Action",
                Command = action.Command,
                CommandParameter = action.CommandParameter,
            };
            item.Click += (_, _) => ActionInvoked?.Invoke(
                this, new MaterialSwipeActionInvokedEventArgs(action, index, bySwipe: false));
            _menu.Items.Add(item);
        }
        ContextMenu = _menu;
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index })
            return;
        InvokeAction(index, bySwipe: false);
        e.Handled = true;
    }

    private void Invoke(MaterialSwipeAction action, int index, bool bySwipe)
    {
        if (action.Command?.CanExecute(action.CommandParameter) == true)
            action.Command.Execute(action.CommandParameter);
        ActionInvoked?.Invoke(this, new MaterialSwipeActionInvokedEventArgs(action, index, bySwipe));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _actions.Count == 0)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Visual source && (IsInsideActions(source) || FindInteractive(source) is not null))
            return;

        if (SwipeState is not MaterialSwipeState.Default)
        {
            // Tapping the row while the rail is open closes it instead of activating the row.
            Collapse();
            e.Handled = true;
            return;
        }

        _tracking = true;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _pressOffset = _offset;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_tracking)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _tracking = false;
            return;
        }

        var point = e.GetPosition(this);
        var dx = point.X - _pressPoint.X;
        var dy = point.Y - _pressPoint.Y;
        if (!_dragging)
        {
            if (Math.Abs(dx) < 6 || Math.Abs(dx) < Math.Abs(dy) * 1.2)
                return;
            _dragging = true;
            _slide?.Dispose();
            _slide = null;
            e.Pointer.Capture(this);
        }

        var magnitude = Math.Clamp((_pressOffset + dx) * Sign, 0, DragMaximum);
        ApplyOffset(Sign * magnitude);
        SetState(StateFor(magnitude), revealed: magnitude >= BaseReveal * RevealThreshold);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_tracking)
            return;
        var dragged = _dragging;
        _tracking = false;
        _dragging = false;
        if (!dragged)
            return;

        e.Pointer.Capture(null);
        e.Handled = true;

        var magnitude = Math.Abs(_offset);
        var primary = PrimaryIndex;
        if (primary >= 0 && magnitude >= BaseReveal + SwipeActionExtra * SwipeActionThreshold)
        {
            SetState(MaterialSwipeState.SwipeAction, revealed: false);
            SlideTo(Sign * (BaseReveal + SwipeActionExtra));
            Invoke(_actions[primary], primary, bySwipe: true);
            // Let the swipe-action pill read for one short beat, then collapse the row.
            _settleTimer ??= CreateSettleTimer();
            _settleTimer.Stop();
            _settleTimer.Start();
        }
        else if (magnitude >= BaseReveal * RevealThreshold)
        {
            SetState(MaterialSwipeState.Revealed, revealed: true);
            SlideTo(Sign * BaseReveal);
        }
        else if (magnitude > PeekReveal * 0.5)
        {
            SetState(MaterialSwipeState.InitiateReveal, revealed: false);
            SlideTo(Sign * PeekReveal);
        }
        else
        {
            SetState(MaterialSwipeState.Default, revealed: false);
            SlideTo(0);
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_tracking && !_dragging)
            return;
        _tracking = false;
        _dragging = false;
        ApplyOffset(Sign * OffsetForState());
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEnabled || _actions.Count == 0)
            return;

        var revealKey = Sign < 0 ? Key.Left : Key.Right;
        var collapseKey = Sign < 0 ? Key.Right : Key.Left;

        if (e.Key == revealKey && SwipeState == MaterialSwipeState.Default)
        {
            Reveal();
            e.Handled = true;
        }
        else if (e.Key == collapseKey && SwipeState != MaterialSwipeState.Default)
        {
            Collapse();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SwipeState != MaterialSwipeState.Default)
        {
            Collapse();
            e.Handled = true;
        }
    }

    private void FocusPrimaryAction()
    {
        var primary = PrimaryIndex;
        foreach (var button in _buttons)
        {
            if (button.Tag is int index && index == primary)
            {
                button.Focus();
                return;
            }
        }
    }

    private int PrimaryIndex
    {
        get
        {
            for (var i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].IsPrimary)
                    return i;
            }
            return _actions.Count > 0 ? 0 : -1;
        }
    }

    private double PeekReveal => _actions.Count * PeekActionSize + (_actions.Count - 1) * ActionGap;

    private double BaseReveal => _actions.Count * ActionSize + (_actions.Count - 1) * ActionGap;

    private double DragMaximum => BaseReveal + (PrimaryIndex >= 0 ? SwipeActionExtra : 0);

    private double OffsetForState() => SwipeState switch
    {
        MaterialSwipeState.Revealed => BaseReveal,
        MaterialSwipeState.InitiateReveal => PeekReveal,
        MaterialSwipeState.SwipeAction => BaseReveal + SwipeActionExtra,
        _ => 0,
    };

    private MaterialSwipeState StateFor(double magnitude)
    {
        if (magnitude >= BaseReveal + 1)
            return MaterialSwipeState.SwipeAction;
        if (magnitude >= BaseReveal * RevealThreshold)
            return MaterialSwipeState.Revealed;
        return magnitude > 0 ? MaterialSwipeState.InitiateReveal : MaterialSwipeState.Default;
    }

    private void SetState(MaterialSwipeState state, bool revealed)
    {
        SwipeState = state;
        UpdatePseudoClasses(state);
        _updatingState = true;
        SetCurrentValue(IsRevealedProperty, revealed);
        _updatingState = false;
    }

    private void UpdatePseudoClasses(MaterialSwipeState state)
    {
        PseudoClasses.Set(":swipe-peek", state == MaterialSwipeState.InitiateReveal);
        PseudoClasses.Set(":swipe-revealed", state == MaterialSwipeState.Revealed);
        PseudoClasses.Set(":swipe-action", state == MaterialSwipeState.SwipeAction);
    }

    private void SlideTo(double target, bool animate = true)
    {
        if (!animate || MaterialMotion.GetIsReduced(this))
        {
            _slide?.Dispose();
            _slide = null;
            ApplyOffset(target);
            return;
        }

        if (_slide is not null)
        {
            _slide.Retarget(target);
            return;
        }
        _slide = MaterialMotion.Animate(this, _offset, target, ApplyOffset, MaterialSpring.SpatialFast);
    }

    private void ApplyOffset(double offset)
    {
        _offset = offset;
        if (_translate is not null)
            _translate.X = offset;

        var magnitude = Math.Abs(offset);
        var visible = magnitude > 0.5 && _buttons.Count > 0;
        if (_reveal is not null)
            _reveal.IsVisible = visible;
        if (_actionPanel is not null)
            _actionPanel.IsVisible = visible;
        if (!visible)
            return;

        var peek = PeekReveal;
        var baseReveal = BaseReveal;
        var progress = baseReveal > peek
            ? Math.Clamp((magnitude - peek) / (baseReveal - peek), 0, 1)
            : 1;
        var extraProgress = Math.Clamp(Math.Max(0, magnitude - baseReveal) / SwipeActionExtra, 0, 1);
        var primary = PrimaryIndex;

        foreach (var button in _buttons)
        {
            if (button.Tag is not int actionIndex)
                continue;
            var width = PeekActionSize + (ActionSize - PeekActionSize) * progress;
            if (actionIndex == primary)
                width += SwipeActionExtra * extraProgress;
            button.Width = width;
            button.CornerRadius = new CornerRadius(Math.Min(width, ActionSize) / 2);
            button.IsTabStop = progress > 0.5;
        }
    }

    private DispatcherTimer CreateSettleTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Collapse();
        };
        return timer;
    }

    private void StopSettleTimer()
    {
        _settleTimer?.Stop();
        _settleTimer = null;
    }

    private bool IsInsideActions(Visual source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, this))
                return false;
            if (current is Button button && _buttons.Contains(button))
                return true;
        }
        return false;
    }

    private static Control? FindInteractive(Visual source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or TextBox or Slider or ComboBox)
                return current as Control;
        }
        return null;
    }

    private IBrush ResolveBrush(string key, Color fallback) =>
        this.TryFindResource(key, out var value) && value is IBrush brush ? brush : new SolidColorBrush(fallback);
}
