using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The two plain tooltip layouts exported by the source component.</summary>
public enum MaterialPlainToolTipType
{
    /// <summary>One line that hugs its text and truncates at the 200 DIP maximum width.</summary>
    SingleLine,
    /// <summary>A fixed 200 DIP frame whose text wraps onto as many lines as it needs.</summary>
    MultiLine,
}

/// <summary>
/// Adds the Material lifecycle around Avalonia's native plain <see cref="ToolTip"/>: keyboard
/// focus activation, touch long press, anchor placement, and an accessibility help text.
/// </summary>
/// <remarks>
/// Hover timing, pointer placement and light dismissal stay with Avalonia's
/// <c>ToolTipService</c>; this class never duplicates that timer. When enabled, a tooltip opens
/// when the owner receives focus through the keyboard or a screen reader, closes on Escape,
/// focus loss, owner disablement/hiding, window deactivation and visual-tree detachment.
/// <para>
/// Plain tooltip visuals (single line and multi line) are the <c>ToolTip</c> theme in
/// <c>Themes/Menus.axaml</c>: inverse surface, 4 px radius, 8/4 padding, 24 px minimum height,
/// body/small type and a 200 px wrap width.
/// </para>
/// </remarks>
public sealed class MaterialToolTip : AvaloniaObject
{
    /// <summary>Enables the keyboard, touch and accessibility lifecycle for the owner's tooltip.</summary>
    public static readonly AttachedProperty<bool> KeyboardEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("KeyboardEnabled");

    /// <summary>
    /// Places the tooltip below the owner at a 4 px offset when the application has not chosen
    /// a placement itself. The pointer placement Avalonia uses by default is wrong for tooltips
    /// opened from the keyboard, where no pointer is near the focused control.
    /// </summary>
    public static readonly AttachedProperty<bool> AutoPlacementProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("AutoPlacement", true);

    /// <summary>
    /// Opens the tooltip after a 500 ms touch press, the Material long-press gesture. Requires
    /// <see cref="KeyboardEnabledProperty"/>, which MaterialTheme already sets on interactive controls.
    /// </summary>
    public static readonly AttachedProperty<bool> LongPressEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("LongPressEnabled");

    /// <summary>
    /// Mirrors a string tooltip into <see cref="AutomationProperties.HelpTextProperty"/> so screen
    /// readers can read it. Avalonia's tooltip popup exposes no automation peer of its own.
    /// An explicitly authored help text is never overwritten.
    /// </summary>
    public static readonly AttachedProperty<bool> ExposeHelpTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialToolTip, Control, bool>("ExposeHelpText", true);

    private const int LongPressMilliseconds = 500;
    private const double LongPressSlop = 8;
    private static readonly ConditionalWeakTable<Control, Behavior> Behaviors = new();

    static MaterialToolTip()
    {
        KeyboardEnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
                Behaviors.GetValue(control, static owner => new Behavior(owner)).Attach();
            else if (Behaviors.TryGetValue(control, out var behavior))
                behavior.Detach();
        });
        ExposeHelpTextProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.SyncHelpText();
        });
        LongPressEnabledProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.UpdatePointerHandlers();
        });
        AutoPlacementProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            if (Behaviors.TryGetValue(control, out var behavior)) behavior.UpdatePlacement();
        });
    }

    public static bool GetKeyboardEnabled(Control control) => control.GetValue(KeyboardEnabledProperty);
    public static void SetKeyboardEnabled(Control control, bool enabled) => control.SetValue(KeyboardEnabledProperty, enabled);
    public static bool GetAutoPlacement(Control control) => control.GetValue(AutoPlacementProperty);
    public static void SetAutoPlacement(Control control, bool enabled) => control.SetValue(AutoPlacementProperty, enabled);
    public static bool GetLongPressEnabled(Control control) => control.GetValue(LongPressEnabledProperty);
    public static void SetLongPressEnabled(Control control, bool enabled) => control.SetValue(LongPressEnabledProperty, enabled);
    public static bool GetExposeHelpText(Control control) => control.GetValue(ExposeHelpTextProperty);
    public static void SetExposeHelpText(Control control, bool enabled) => control.SetValue(ExposeHelpTextProperty, enabled);

    /// <summary>
    /// Builds tooltip content that reproduces one of the two source plain tooltip variants.
    /// </summary>
    /// <remarks>
    /// A plain <see cref="string"/> tip is already correct for the single-line variant: the
    /// <c>ToolTip</c> theme hugs short text and wraps long text at the 200 DIP maximum width.
    /// Use this factory when the fixed 200 DIP frame matters, for example in a layout comparison,
    /// or to force single-line truncation for a long string. The returned block is 184 DIP wide
    /// because the 200 DIP source frame includes the theme's 8 DIP horizontal padding.
    /// </remarks>
    public static Control CreateContent(string text, MaterialPlainToolTipType type = MaterialPlainToolTipType.SingleLine)
    {
        var block = new TextBlock
        {
            Text = text,
            // The 200 DIP source frame includes the 8 DIP horizontal padding on both sides.
            MaxWidth = 184,
            TextWrapping = type == MaterialPlainToolTipType.MultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = type == MaterialPlainToolTipType.SingleLine ? TextTrimming.CharacterEllipsis : TextTrimming.None
        };
        if (type == MaterialPlainToolTipType.MultiLine)
            block.Width = 184;
        // Body/small already binds font size, weight, letter spacing and line height.
        MaterialTypography.SetStyle(block, MaterialTypeStyle.BodySmall);
        return block;
    }

    /// <summary>Per-owner state; the table keeps it alive exactly as long as the owner.</summary>
    private sealed class Behavior
    {
        private readonly Control _control;
        private DispatcherTimer? _longPressTimer;
        private Window? _window;
        private Point _pressOrigin;
        private bool _pressed;
        private bool _openedByLongPress;
        private bool _attached;
        private bool _helpTextOwned;
        private string? _helpTextWritten;
        private bool _placementApplied;
        private bool _offsetApplied;
        private bool _pointerAttached;

        public Behavior(Control control) => _control = control;

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            _control.GotFocus += GotFocus;
            _control.LostFocus += LostFocus;
            _control.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
            _control.AttachedToVisualTree += AttachedToTree;
            _control.DetachedFromVisualTree += DetachedFromTree;
            _control.PropertyChanged += ControlPropertyChanged;
            UpdatePointerHandlers();
            ApplyPlacement();
            SyncHelpText();
            if (_control.IsAttachedToVisualTree()) HookWindow();
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            _control.GotFocus -= GotFocus;
            _control.LostFocus -= LostFocus;
            _control.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
            UpdatePointerHandlers();
            _control.AttachedToVisualTree -= AttachedToTree;
            _control.DetachedFromVisualTree -= DetachedFromTree;
            _control.PropertyChanged -= ControlPropertyChanged;
            UnhookWindow();
            CancelLongPress();
            _pressed = false;
            _openedByLongPress = false;
            HideTip();
            RestorePlacement();
            ReleaseHelpText();
        }

        /// <summary>
        /// Pointer tracking is only needed for the opt-in touch long press; attaching it to every
        /// themed control would put a pointer-move handler on each one.
        /// </summary>
        public void UpdatePointerHandlers()
        {
            var wanted = _attached && GetLongPressEnabled(_control);
            if (wanted == _pointerAttached) return;
            _pointerAttached = wanted;
            if (wanted)
            {
                _control.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel);
                _control.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, RoutingStrategies.Tunnel);
                _control.AddHandler(InputElement.PointerMovedEvent, PointerMoved, RoutingStrategies.Tunnel);
                // PointerCaptureLost is a direct event, so it is subscribed through its CLR event.
                _control.PointerCaptureLost += PointerCaptureLost;
            }
            else
            {
                _control.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
                _control.RemoveHandler(InputElement.PointerReleasedEvent, PointerReleased);
                _control.RemoveHandler(InputElement.PointerMovedEvent, PointerMoved);
                _control.PointerCaptureLost -= PointerCaptureLost;
                CancelLongPress();
                _pressed = false;
                if (_openedByLongPress)
                {
                    _openedByLongPress = false;
                    HideTip();
                }
            }
        }

        private void GotFocus(object? sender, FocusChangedEventArgs args)
        {
            // Pointer focus keeps Avalonia's hover timing; only keyboard and programmatic focus
            // show the tooltip immediately. A focus move inside the owner must not re-open it.
            if (args.Source != _control || args.NavigationMethod == NavigationMethod.Pointer) return;
            ShowTip();
        }

        private void LostFocus(object? sender, FocusChangedEventArgs args)
        {
            if (!_control.IsKeyboardFocusWithin) HideTip();
        }

        private void KeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape || !ToolTip.GetIsOpen(_control)) return;
            HideTip();
            args.Handled = true;
        }

        private void PointerPressed(object? sender, PointerPressedEventArgs args)
        {
            if (args.Pointer.Type != PointerType.Touch) return;
            if (!GetLongPressEnabled(_control) || ToolTip.GetTip(_control) is null) return;
            _pressed = true;
            _pressOrigin = args.GetPosition(_control);
            CancelLongPress();
            _longPressTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(LongPressMilliseconds),
                DispatcherPriority.Input, OnLongPress);
            _longPressTimer.Start();
        }

        private void OnLongPress(object? sender, EventArgs args)
        {
            CancelLongPress();
            if (!_pressed || !_control.IsEffectivelyEnabled || !_control.IsEffectivelyVisible) return;
            if (ToolTip.GetTip(_control) is null) return;
            _openedByLongPress = true;
            ShowTip();
        }

        private void PointerReleased(object? sender, PointerReleasedEventArgs args)
        {
            _pressed = false;
            CancelLongPress();
            // Material long-press tooltips are dismissed when the finger lifts; keyboard-opened
            // tooltips stay until Escape, focus loss or another pointer action.
            if (_openedByLongPress)
            {
                _openedByLongPress = false;
                HideTip();
            }
        }

        private void PointerMoved(object? sender, PointerEventArgs args)
        {
            if (!_pressed) return;
            var delta = args.GetPosition(_control) - _pressOrigin;
            if (Math.Abs(delta.X) > LongPressSlop || Math.Abs(delta.Y) > LongPressSlop)
            {
                _pressed = false;
                CancelLongPress();
            }
        }

        private void PointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
        {
            _pressed = false;
            CancelLongPress();
            if (_openedByLongPress)
            {
                _openedByLongPress = false;
                HideTip();
            }
        }

        private void ControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ToolTip.TipProperty)
            {
                ApplyPlacement();
                SyncHelpText();
            }
            else if (change.Property == InputElement.IsEffectivelyEnabledProperty && !_control.IsEffectivelyEnabled ||
                     change.Property == Visual.IsVisibleProperty && !_control.IsVisible)
            {
                CancelLongPress();
                _pressed = false;
                HideTip();
            }
        }

        private void AttachedToTree(object? sender, VisualTreeAttachmentEventArgs args) => HookWindow();

        private void DetachedFromTree(object? sender, VisualTreeAttachmentEventArgs args)
        {
            UnhookWindow();
            CancelLongPress();
            _pressed = false;
            HideTip();
        }

        private void HookWindow()
        {
            var window = TopLevel.GetTopLevel(_control) as Window;
            if (ReferenceEquals(window, _window)) return;
            UnhookWindow();
            _window = window;
            if (_window is not null) _window.Deactivated += WindowDeactivated;
        }

        private void UnhookWindow()
        {
            if (_window is null) return;
            _window.Deactivated -= WindowDeactivated;
            _window = null;
        }

        private void WindowDeactivated(object? sender, EventArgs args)
        {
            if (ToolTip.GetIsOpen(_control)) HideTip();
        }

        private void ShowTip()
        {
            if (!_control.IsEffectivelyEnabled || !_control.IsEffectivelyVisible) return;
            if (ToolTip.GetTip(_control) is null) return;
            ToolTip.SetIsOpen(_control, true);
        }

        private void HideTip() => ToolTip.SetIsOpen(_control, false);

        private void CancelLongPress()
        {
            _longPressTimer?.Stop();
            _longPressTimer = null;
        }

        public void UpdatePlacement()
        {
            if (!_attached) return;
            if (GetAutoPlacement(_control)) ApplyPlacement();
            else RestorePlacement();
        }

        private void ApplyPlacement()
        {
            if (!GetAutoPlacement(_control) || ToolTip.GetTip(_control) is null) return;
            // Only take ownership of a value the application has not authored itself, so a
            // consumer style or local value always wins.
            if (!_placementApplied && !_control.IsSet(ToolTip.PlacementProperty))
            {
                _control.SetValue(ToolTip.PlacementProperty, PlacementMode.Bottom);
                _placementApplied = true;
            }
            if (!_offsetApplied && !_control.IsSet(ToolTip.VerticalOffsetProperty))
            {
                _control.SetValue(ToolTip.VerticalOffsetProperty, 4d);
                _offsetApplied = true;
            }
        }

        private void RestorePlacement()
        {
            if (_placementApplied)
            {
                _placementApplied = false;
                _control.ClearValue(ToolTip.PlacementProperty);
            }
            if (_offsetApplied)
            {
                _offsetApplied = false;
                _control.ClearValue(ToolTip.VerticalOffsetProperty);
            }
        }

        public void SyncHelpText()
        {
            if (!_attached)
            {
                ReleaseHelpText();
                return;
            }

            var text = GetTipText(ToolTip.GetTip(_control));
            if (!GetExposeHelpText(_control) || text is null)
            {
                ReleaseHelpText();
                return;
            }

            var current = AutomationProperties.GetHelpText(_control);
            if (_helpTextOwned)
            {
                if (!string.Equals(current, _helpTextWritten, StringComparison.Ordinal))
                {
                    // Another author replaced the value; never fight it.
                    _helpTextOwned = false;
                    _helpTextWritten = null;
                }
                else
                {
                    AutomationProperties.SetHelpText(_control, text);
                    _helpTextWritten = text;
                    return;
                }
            }

            if (_control.IsSet(AutomationProperties.HelpTextProperty) || !string.IsNullOrEmpty(current))
            {
                // Authored or bound help text wins over the tooltip mirror.
                return;
            }

            AutomationProperties.SetHelpText(_control, text);
            _helpTextOwned = true;
            _helpTextWritten = text;
        }

        private void ReleaseHelpText()
        {
            if (!_helpTextOwned)
            {
                _helpTextWritten = null;
                return;
            }
            if (string.Equals(AutomationProperties.GetHelpText(_control), _helpTextWritten, StringComparison.Ordinal))
                AutomationProperties.SetHelpText(_control, null);
            _helpTextOwned = false;
            _helpTextWritten = null;
        }

        private static string? GetTipText(object? tip) => tip switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            TextBlock block when !string.IsNullOrWhiteSpace(block.Text) => block.Text,
            _ => null
        };
    }
}
