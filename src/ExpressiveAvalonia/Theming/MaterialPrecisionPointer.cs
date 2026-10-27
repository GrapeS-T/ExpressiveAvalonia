using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// Avalonia adapter for the source precision-pointer flag.
/// </summary>
/// <remarks>
/// <para>
/// The source value is true only when the component-sizing feature flag is enabled and both a
/// non-virtual alphabetic keyboard and a non-stylus mouse are present. The public Avalonia input
/// contract has no device enumeration (unlike Android's <c>InputManager</c>), so the built-in
/// adapter observes the window's input: a <see cref="KeyDeviceType.Keyboard"/> key event marks a
/// keyboard as present, and a <see cref="PointerType.Mouse"/> pointer event marks a mouse as
/// present. Touch and pen/stylus pointers never count. Virtual keyboards cannot be distinguished
/// by Avalonia, so a host that can enumerate devices should use
/// <see cref="SetDevicePresence"/> for exact presence and device-change behavior.
/// </para>
/// <para>
/// The flag defaults to false, exactly like
/// <c>ComposeMaterial3Flags.isPrecisionPointerComponentSizingEnabled</c>. Windows and other
/// hierarchies must call <see cref="EnsureListenersRegistered"/> once from a root control
/// (for example a window or catalog page) to feed the adapter; repeated calls for the same
/// <see cref="TopLevel"/> are no-ops. <see cref="Changed"/> is raised only when the resolved
/// value changes.
/// </para>
/// </remarks>
public static class MaterialPrecisionPointer
{
    private static readonly ConditionalWeakTable<TopLevel, InputTracker> Trackers = new();
    private static bool _componentSizingEnabled;
    private static bool _keyboardPresent;
    private static bool _mousePresent;
    private static bool _shouldUse;

    /// <summary>
    /// Gets or sets the equivalent of the source
    /// <c>isPrecisionPointerComponentSizingEnabled</c> feature flag.
    /// </summary>
    public static bool IsComponentSizingEnabled
    {
        get => _componentSizingEnabled;
        set
        {
            if (_componentSizingEnabled == value)
                return;
            _componentSizingEnabled = value;
            Recompute();
        }
    }

    /// <summary>
    /// Gets the resolved source value: the feature flag and both device families are present.
    /// </summary>
    public static bool ShouldUsePrecisionPointerComponentSizing => _shouldUse;

    /// <summary>
    /// Gets or sets whether a non-gamepad, non-remote keyboard is present. The built-in adapter
    /// sets this from keyboard input; a platform host may set it from its device list.
    /// </summary>
    public static bool IsKeyboardPresent
    {
        get => _keyboardPresent;
        set
        {
            if (_keyboardPresent == value)
                return;
            _keyboardPresent = value;
            Recompute();
        }
    }

    /// <summary>
    /// Gets or sets whether a non-stylus mouse is present. The built-in adapter sets this from
    /// mouse pointer input; a platform host may set it from its device list.
    /// </summary>
    public static bool IsMousePresent
    {
        get => _mousePresent;
        set
        {
            if (_mousePresent == value)
                return;
            _mousePresent = value;
            Recompute();
        }
    }

    /// <summary>Raised when <see cref="ShouldUsePrecisionPointerComponentSizing"/> changes.</summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// Supplies exact device presence, for hosts that can enumerate input devices. Equivalent to
    /// assigning <see cref="IsKeyboardPresent"/> and <see cref="IsMousePresent"/> together.
    /// </summary>
    public static void SetDevicePresence(bool hasKeyboard, bool hasMouse)
    {
        if (_keyboardPresent == hasKeyboard && _mousePresent == hasMouse)
            return;
        _keyboardPresent = hasKeyboard;
        _mousePresent = hasMouse;
        Recompute();
    }

    /// <summary>
    /// Registers the shared input adapter for <paramref name="host"/>'s <see cref="TopLevel"/>.
    /// If the control is not attached yet, registration is deferred until it is. Calling this
    /// again for the same hierarchy does nothing.
    /// </summary>
    public static void EnsureListenersRegistered(Control host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (TopLevel.GetTopLevel(host) is { } topLevel)
        {
            Register(topLevel);
            return;
        }

        void Attached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            host.AttachedToVisualTree -= Attached;
            if (TopLevel.GetTopLevel(host) is { } attachedTopLevel)
                Register(attachedTopLevel);
        }

        host.AttachedToVisualTree += Attached;
    }

    private static void Register(TopLevel topLevel) =>
        Trackers.GetValue(topLevel, static level => new InputTracker(level));

    private static void Recompute()
    {
        var next = _componentSizingEnabled && _keyboardPresent && _mousePresent;
        if (next == _shouldUse)
            return;
        _shouldUse = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private sealed class InputTracker
    {
        internal InputTracker(TopLevel topLevel)
        {
            topLevel.AddHandler(
                InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(
                InputElement.PointerEnteredEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(
                InputElement.PointerMovedEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(
                InputElement.PointerPressedEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(
                InputElement.PointerWheelChangedEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyDeviceType == KeyDeviceType.Keyboard)
                IsKeyboardPresent = true;
        }

        private static void OnPointer(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
                IsMousePresent = true;
        }
    }
}