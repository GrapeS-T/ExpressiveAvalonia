using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>The four source keyboard configurations.</summary>
public enum MaterialKeyboardConfiguration
{
    /// <summary>QWERTY letters with a symbol layer.</summary>
    Base,
    /// <summary>Digits with arithmetic operators and a symbol layer.</summary>
    Alphanumeric,
    /// <summary>Phone keypad: digits, decimal point, space and enter.</summary>
    Keypad,
    /// <summary>Digits only; no symbol layer.</summary>
    NumericOnly
}

/// <summary>The three source keyboard presentations (the source <c>Style</c> axis).</summary>
public enum MaterialKeyboardStyle
{
    /// <summary>Full-width, 46 DIP rows with a gesture bar.</summary>
    Portrait,
    /// <summary>Full-width, 34 DIP rows with a gesture bar.</summary>
    Landscape,
    /// <summary>Compact floating card with an elevation shadow, draggable by its chrome bar.</summary>
    Floating
}

/// <summary>Which key set is showing.</summary>
public enum MaterialKeyboardLayer { Letters, Symbols, Emoji }

/// <summary>System actions exposed by the keyboard chrome.</summary>
public enum MaterialKeyboardChrome
{
    /// <summary>The leading chevron in the chrome bar.</summary>
    Back,
    Gif,
    Settings,
    Translate,
    Sticker,
    More,
    VoiceInput,
    Language,
    /// <summary>The collapse key in the bottom bar.</summary>
    Collapse,
    /// <summary>The hide-keyboard key in the bottom bar.</summary>
    Hide
}

/// <summary>Reports an inserted text run so hosts can react without a TextBox target.</summary>
public sealed class MaterialKeyboardTextEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

/// <summary>Reports a chrome action; set <see cref="Handled"/> when the host responded.</summary>
public sealed class MaterialKeyboardChromeEventArgs(MaterialKeyboardChrome action) : EventArgs
{
    public MaterialKeyboardChrome Action { get; } = action;
    public bool Handled { get; set; }
}

/// <summary>
/// An on-screen keyboard built from the source keyboard configurations and presentations.
/// </summary>
/// <remarks>
/// <para>
/// This is an example composition from the project's design source, not an M3 composable and not a
/// replacement for the operating-system IME. It never publishes itself as a system input service;
/// hosts wire <see cref="InputTarget"/> or react to <see cref="TextInserted"/>.
/// </para>
/// <para>
/// Keys are native <see cref="Button"/>s (or <see cref="RepeatButton"/> for backspace), so pointer,
/// Space/Enter activation, focus visuals and automation come from Avalonia. The control adds the
/// keyboard-specific behaviour: shift and double-tap caps lock, the symbol and emoji layers,
/// hardware-key input, arrow-key focus movement between keys, auto-repeating backspace, and
/// insertion into <see cref="InputTarget"/> or the <see cref="Text"/> buffer.
/// </para>
/// <para>
/// Chrome glyphs (GIF, settings, translate, sticker, more, voice, language, hide, collapse) are the
/// source vectors. Those keys report through <see cref="ChromeInvoked"/> because the actions belong
/// to the host system, not to a keyboard widget.
/// </para>
/// <para>
/// Floating presentation adds an elevation shadow and a drag handle on the chrome bar, and dismisses
/// on Escape. State transitions follow the theme duration tokens, which MaterialTheme zeroes under
/// reduced motion.
/// </para>
/// </remarks>
public sealed class MaterialKeyboard : TemplatedControl
{
    /// <summary>The source configuration to lay out.</summary>
    public static readonly StyledProperty<MaterialKeyboardConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialKeyboard, MaterialKeyboardConfiguration>(nameof(Configuration));

    /// <summary>The source presentation (the source <c>Style</c> axis).</summary>
    public static readonly StyledProperty<MaterialKeyboardStyle> PresentationProperty =
        AvaloniaProperty.Register<MaterialKeyboard, MaterialKeyboardStyle>(nameof(Presentation));

    /// <summary>The visible key set. Switching layers rebuilds the keys.</summary>
    public static readonly StyledProperty<MaterialKeyboardLayer> LayerProperty =
        AvaloniaProperty.Register<MaterialKeyboard, MaterialKeyboardLayer>(nameof(Layer));

    /// <summary>Shift state. Double-tapping shift toggles <see cref="IsCapsLocked"/>.</summary>
    public static readonly StyledProperty<bool> IsShiftedProperty =
        AvaloniaProperty.Register<MaterialKeyboard, bool>(nameof(IsShifted));

    /// <summary>Caps lock, engaged by double-tapping shift.</summary>
    public static readonly StyledProperty<bool> IsCapsLockedProperty =
        AvaloniaProperty.Register<MaterialKeyboard, bool>(nameof(IsCapsLocked));

    /// <summary>Insertion target. When null, typed text accumulates in <see cref="Text"/>.</summary>
    public static readonly StyledProperty<TextBox?> InputTargetProperty =
        AvaloniaProperty.Register<MaterialKeyboard, TextBox?>(nameof(InputTarget));

    /// <summary>Text buffer used when no <see cref="InputTarget"/> is set.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MaterialKeyboard, string>(nameof(Text), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the keyboard is shown. Dismissing sets this to false and raises Dismissed.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MaterialKeyboard, bool>(nameof(IsOpen), true);

    /// <summary>Shows the source chrome bar with the media and system keys.</summary>
    public static readonly StyledProperty<bool> ShowChromeBarProperty =
        AvaloniaProperty.Register<MaterialKeyboard, bool>(nameof(ShowChromeBar), true);

    /// <summary>Shows the source bottom bar with collapse, gesture handle and hide keys.</summary>
    public static readonly StyledProperty<bool> ShowBottomBarProperty =
        AvaloniaProperty.Register<MaterialKeyboard, bool>(nameof(ShowBottomBar), true);

    private Grid? _chromeBar;
    private Grid? _keyHost;
    private Grid? _bottomBar;
    private Border? _surface;
    private TranslateTransform? _drag;
    private Point _dragOrigin;
    private bool _dragging;
    private DateTime _lastShiftPress = DateTime.MinValue;

    // Key positions feed arrow-key navigation; the list order is the visual row order.
    private readonly List<List<Button>> _keyRows = [];

    static MaterialKeyboard()
    {
        ConfigurationProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.RebuildKeys());
        PresentationProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.Rebuild());
        LayerProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.RebuildKeys());
        IsShiftedProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.RebuildKeys());
        IsCapsLockedProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.RebuildKeys());
        ShowChromeBarProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.UpdateChromeVisibility());
        ShowBottomBarProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, _) => keyboard.UpdateChromeVisibility());
        IsOpenProperty.Changed.AddClassHandler<MaterialKeyboard>(static (keyboard, change) =>
            keyboard.IsVisible = change.GetNewValue<bool>());
    }

    public MaterialKeyboard()
    {
        Template = new FuncControlTemplate<MaterialKeyboard>((keyboard, _) => keyboard.Build());
        AutomationProperties.SetName(this, "Keyboard");
    }

    /// <summary>Raised for every inserted text run.</summary>
    public event EventHandler<MaterialKeyboardTextEventArgs>? TextEntered;

    /// <summary>Raised when enter is pressed and the target does not accept newlines.</summary>
    public event EventHandler? ReturnPressed;

    /// <summary>Raised after the keyboard is dismissed.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Raised for chrome keys that belong to the host system.</summary>
    public event EventHandler<MaterialKeyboardChromeEventArgs>? ChromeInvoked;

    public MaterialKeyboardConfiguration Configuration { get => GetValue(ConfigurationProperty); set => SetValue(ConfigurationProperty, value); }
    public MaterialKeyboardStyle Presentation { get => GetValue(PresentationProperty); set => SetValue(PresentationProperty, value); }
    public MaterialKeyboardLayer Layer { get => GetValue(LayerProperty); set => SetValue(LayerProperty, value); }
    public bool IsShifted { get => GetValue(IsShiftedProperty); set => SetValue(IsShiftedProperty, value); }
    public bool IsCapsLocked { get => GetValue(IsCapsLockedProperty); set => SetValue(IsCapsLockedProperty, value); }
    public TextBox? InputTarget { get => GetValue(InputTargetProperty); set => SetValue(InputTargetProperty, value); }
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool ShowChromeBar { get => GetValue(ShowChromeBarProperty); set => SetValue(ShowChromeBarProperty, value); }
    public bool ShowBottomBar { get => GetValue(ShowBottomBarProperty); set => SetValue(ShowBottomBarProperty, value); }

    /// <summary>Hides the keyboard and raises <see cref="Dismissed"/>.</summary>
    public void Dismiss()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the keyboard again after <see cref="Dismiss"/>.</summary>
    public void Show() => IsOpen = true;

    /// <summary>Presses a key by its display label, e.g. "Q", "space" or "?123".</summary>
    /// <returns>False when the current layer has no such key.</returns>
    public bool PressKey(string label)
    {
        foreach (var row in _keyRows)
            foreach (var key in row)
                if (string.Equals(key.Tag as string, label, StringComparison.Ordinal))
                {
                    key.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    return true;
                }
        return false;
    }

    private Control Build()
    {
        _keyHost = new Grid
        {
            Margin = new Thickness(8, 4, 8, 4),
            RowSpacing = RowSpacing
        };

        _chromeBar = BuildChromeBar();
        _bottomBar = BuildBottomBar();

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(_chromeBar);
        Grid.SetRow(_keyHost, 1);
        root.Children.Add(_keyHost);
        Grid.SetRow(_bottomBar, 2);
        root.Children.Add(_bottomBar);

        _drag = new TranslateTransform();
        _surface = new Border
        {
            Child = root,
            RenderTransform = _drag
        };
        _surface.Bind(Border.BackgroundProperty, _surface.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        Rebuild();
        return new Border { Child = _surface };
    }

    private double KeyHeight => Presentation switch
    {
        MaterialKeyboardStyle.Landscape => 34,
        MaterialKeyboardStyle.Floating => 41,
        _ => 46
    };

    private double RowSpacing => Presentation == MaterialKeyboardStyle.Portrait ? 6 : 4;

    private void Rebuild()
    {
        if (_surface is null) return;
        if (Presentation == MaterialKeyboardStyle.Floating)
        {
            _surface.MaxWidth = 342;
            _surface.CornerRadius = new CornerRadius(28);
            _surface.Bind(Border.BoxShadowProperty, _surface.GetResourceObservable("MaterialElevationLevel3Shadow"));
        }
        else
        {
            _surface.MaxWidth = double.PositiveInfinity;
            _surface.CornerRadius = new CornerRadius(0);
            _surface.ClearValue(Border.BoxShadowProperty);
        }
        if (_keyHost is not null) _keyHost.RowSpacing = RowSpacing;
        UpdateChromeVisibility();
        RebuildKeys();
    }

    private void UpdateChromeVisibility()
    {
        if (_chromeBar is not null) _chromeBar.IsVisible = ShowChromeBar;
        if (_bottomBar is not null) _bottomBar.IsVisible = ShowBottomBar;
    }

    private Grid BuildChromeBar()
    {
        var leading = ChromeButton(MaterialSourceIcons.Chevron, "Back", MaterialKeyboardChrome.Back, 20);
        leading.HorizontalAlignment = HorizontalAlignment.Left;

        var centre = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        centre.Children.Add(ChromeButton(MaterialSourceIcons.Gif, "GIF", MaterialKeyboardChrome.Gif, 22));
        centre.Children.Add(ChromeButton(MaterialSourceIcons.Settings, "Settings", MaterialKeyboardChrome.Settings, 20));
        centre.Children.Add(ChromeButton(MaterialSourceIcons.Translate, "Translate", MaterialKeyboardChrome.Translate, 20));
        centre.Children.Add(ChromeButton(MaterialSourceIcons.Sticker, "Stickers", MaterialKeyboardChrome.Sticker, 20));

        var divider = new Border { Width = 1, Height = 24, VerticalAlignment = VerticalAlignment.Center };
        divider.Bind(Border.BackgroundProperty, divider.GetResourceObservable("MaterialOutlineVariantBrush"));

        var trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        trailing.Children.Add(ChromeButton(MaterialSourceIcons.More, "More", MaterialKeyboardChrome.More, 20));
        trailing.Children.Add(ChromeButton(MaterialSourceIcons.Mic, "Voice input", MaterialKeyboardChrome.VoiceInput, 20));

        var grid = new Grid
        {
            Height = 44,
            Margin = new Thickness(8, 0, 8, 0),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto")
        };
        grid.Children.Add(leading);
        Grid.SetColumn(centre, 1);
        grid.Children.Add(centre);
        Grid.SetColumn(divider, 2);
        grid.Children.Add(divider);
        Grid.SetColumn(trailing, 3);
        grid.Children.Add(trailing);

        // The floating presentation is dragged by its chrome bar, like the source navigation handle.
        grid.PointerPressed += OnDragStart;
        grid.PointerMoved += OnDragMove;
        grid.PointerReleased += OnDragEnd;
        return grid;
    }

    private Grid BuildBottomBar()
    {
        var collapse = ChromeButton(MaterialSourceIcons.Collapse, "Collapse keyboard", MaterialKeyboardChrome.Collapse, 20);
        collapse.HorizontalAlignment = HorizontalAlignment.Left;
        var hide = ChromeButton(MaterialSourceIcons.Hide, "Hide keyboard", MaterialKeyboardChrome.Hide, 20);
        hide.HorizontalAlignment = HorizontalAlignment.Right;

        var handle = new Border
        {
            Width = 72,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        handle.Bind(Border.BackgroundProperty, handle.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var grid = new Grid
        {
            Height = 36,
            Margin = new Thickness(8, 0, 8, 0),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        grid.Children.Add(collapse);
        Grid.SetColumn(handle, 1);
        grid.Children.Add(handle);
        Grid.SetColumn(hide, 2);
        grid.Children.Add(hide);
        return grid;
    }

    private Button ChromeButton(string icon, string name, MaterialKeyboardChrome action, double size)
    {
        var button = new Button
        {
            Content = ChromeIcon(icon, size),
            Padding = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Width = 32,
            Height = 32,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(button, MaterialControlSize.ExtraSmall);
        MaterialAssist.SetContainerInset(button, new Thickness(0));
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => RaiseChrome(action);
        return button;
    }

    private void RebuildKeys()
    {
        if (_keyHost is null) return;
        _keyHost.Children.Clear();
        _keyHost.RowDefinitions.Clear();
        _keyRows.Clear();

        var rows = BuildRows();
        for (var r = 0; r < rows.Count; r++)
        {
            _keyHost.RowDefinitions.Add(new RowDefinition(KeyHeight, GridUnitType.Pixel));
            var row = new Grid
            {
                ColumnDefinitions = Columns(rows[r]),
                VerticalAlignment = VerticalAlignment.Center
            };
            var buttons = new List<Button>(rows[r].Length);
            for (var c = 0; c < rows[r].Length; c++)
            {
                var key = CreateKey(rows[r][c]);
                Grid.SetColumn(key, c);
                row.Children.Add(key);
                buttons.Add(key);
            }
            Grid.SetRow(row, r);
            _keyHost.Children.Add(row);
            _keyRows.Add(buttons);
        }
    }

    private static ColumnDefinitions Columns(KeySpec[] row)
    {
        var definitions = new ColumnDefinitions();
        foreach (var key in row) definitions.Add(new ColumnDefinition(key.Weight, GridUnitType.Star));
        return definitions;
    }

    private Button CreateKey(KeySpec spec)
    {
        // RepeatButton keeps its native auto-repeat behaviour; the defaults (500 ms delay, 100 ms
        // interval) match the source keyboard's held-backspace feel.
        var key = spec.Action == KeyAction.Backspace ? new RepeatButton() : new Button();

        key.Tag = spec.Label;
        key.Margin = new Thickness(3, 0, 3, 0);
        key.Padding = new Thickness(0);
        key.MinWidth = 0;
        key.MinHeight = 0;
        key.CornerRadius = new CornerRadius(6);
        key.HorizontalContentAlignment = HorizontalAlignment.Center;
        key.VerticalContentAlignment = VerticalAlignment.Center;
        key.HorizontalAlignment = HorizontalAlignment.Stretch;
        key.VerticalAlignment = VerticalAlignment.Stretch;
        MaterialAssist.SetButtonVariant(key, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(key, MaterialControlSize.Small);
        MaterialAssist.SetButtonShape(key, MaterialButtonShape.Square);
        MaterialAssist.SetContainerInset(key, new Thickness(0));

        if (spec.Action == KeyAction.Spacer)
        {
            key.IsVisible = false;
            key.IsHitTestVisible = false;
            key.IsTabStop = false;
            key.Background = Brushes.Transparent;
            return key;
        }

        var accent = spec.Action is KeyAction.Enter;
        key.Bind(TemplatedControl.BackgroundProperty, key.GetResourceObservable(
            accent ? "MaterialPrimaryContainerBrush" : "MaterialSurfaceContainerHighestBrush"));
        key.Bind(TemplatedControl.ForegroundProperty, key.GetResourceObservable(
            accent ? "MaterialOnPrimaryContainerBrush" : "MaterialOnSurfaceBrush"));

        if (spec.Icon is not null)
        {
            key.Content = ChromeIcon(spec.Icon, spec.Action is KeyAction.Shift ? 18 : 22);
            AutomationProperties.SetName(key, spec.Automation ?? spec.Label);
        }
        else
        {
            key.Content = spec.Label;
            MaterialTypography.SetStyle(key,
                spec.Label.Length <= 2 ? MaterialTypeStyle.TitleMedium : MaterialTypeStyle.LabelMedium);
            AutomationProperties.SetName(key, spec.Automation ?? spec.Label);
        }

        if (spec.Action == KeyAction.Shift)
        {
            if (IsShifted || IsCapsLocked)
                key.Bind(TemplatedControl.BackgroundProperty,
                    key.GetResourceObservable("MaterialPrimaryContainerBrush"));
            AutomationProperties.SetName(key, IsCapsLocked ? "Caps lock on" : "Shift");
        }

        key.Click += (_, _) => HandleKey(spec);
        return key;
    }

    private Shape ChromeIcon(string icon, double size)
    {
        var shape = MaterialSourceIcons.Create(icon, size, Brushes.Transparent);
        shape.Bind(Shape.FillProperty, this.GetResourceObservable("MaterialOnSurfaceBrush"));
        return shape;
    }

    private void HandleKey(KeySpec spec)
    {
        switch (spec.Action)
        {
            case KeyAction.Insert:
                Insert(ApplyShift(spec.Insert ?? spec.Label));
                break;
            case KeyAction.Space:
                Insert(" ");
                break;
            case KeyAction.Backspace:
                Backspace();
                break;
            case KeyAction.Enter:
                Enter();
                break;
            case KeyAction.Shift:
                ToggleShift();
                break;
            case KeyAction.Symbols:
                Layer = MaterialKeyboardLayer.Symbols;
                break;
            case KeyAction.Letters:
                Layer = MaterialKeyboardLayer.Letters;
                break;
            case KeyAction.Emoji:
                Layer = Layer == MaterialKeyboardLayer.Emoji
                    ? MaterialKeyboardLayer.Letters
                    : MaterialKeyboardLayer.Emoji;
                break;
            case KeyAction.Language:
                RaiseChrome(MaterialKeyboardChrome.Language);
                break;
            case KeyAction.Dismiss:
                Dismiss();
                break;
            case KeyAction.Chrome:
                if (spec.Chrome is { } chrome) RaiseChrome(chrome);
                break;
        }
    }

    private string ApplyShift(string text)
    {
        if (!IsShifted && !IsCapsLocked) return text;
        if (text.Length != 1 || !char.IsLetter(text[0])) return text;
        var upper = text.ToUpperInvariant();
        // A single shift is consumed by the next letter, like the source keyboard.
        if (IsShifted && !IsCapsLocked) IsShifted = false;
        return upper;
    }

    private void ToggleShift()
    {
        var now = DateTime.UtcNow;
        var doubleTap = (now - _lastShiftPress).TotalMilliseconds <= 400;
        _lastShiftPress = now;
        if (doubleTap)
        {
            IsCapsLocked = !IsCapsLocked;
            IsShifted = IsCapsLocked;
        }
        else
        {
            IsShifted = !IsShifted;
            if (!IsShifted) IsCapsLocked = false;
        }
    }

    private void Insert(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (InputTarget is { } target)
        {
            var value = target.Text ?? string.Empty;
            var start = Math.Clamp(target.SelectionStart, 0, value.Length);
            var end = Math.Clamp(target.SelectionEnd, 0, value.Length);
            if (end < start) (start, end) = (end, start);
            target.Text = string.Concat(value.AsSpan(0, start), text, value.AsSpan(end));
            target.CaretIndex = start + text.Length;
            target.SelectionStart = target.CaretIndex;
            target.SelectionEnd = target.CaretIndex;
        }
        else
        {
            Text += text;
        }
        TextEntered?.Invoke(this, new MaterialKeyboardTextEventArgs(text));
    }

    private void Backspace()
    {
        if (InputTarget is { } target)
        {
            var value = target.Text ?? string.Empty;
            var start = Math.Clamp(target.SelectionStart, 0, value.Length);
            var end = Math.Clamp(target.SelectionEnd, 0, value.Length);
            if (end < start) (start, end) = (end, start);
            if (start != end)
            {
                target.Text = string.Concat(value.AsSpan(0, start), value.AsSpan(end));
                target.CaretIndex = start;
            }
            else if (start > 0)
            {
                var remove = start >= 2 && char.IsLowSurrogate(value[start - 1]) ? 2 : 1;
                target.Text = string.Concat(value.AsSpan(0, start - remove), value.AsSpan(start));
                target.CaretIndex = start - remove;
            }
            else return;
            target.SelectionStart = target.CaretIndex;
            target.SelectionEnd = target.CaretIndex;
            return;
        }

        if (Text.Length == 0) return;
        var count = Text.Length >= 2 && char.IsLowSurrogate(Text[^1]) ? 2 : 1;
        Text = Text[..^count];
    }

    private void Enter()
    {
        if (InputTarget is { AcceptsReturn: true })
        {
            Insert(Environment.NewLine);
            return;
        }
        ReturnPressed?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseChrome(MaterialKeyboardChrome action)
    {
        var args = new MaterialKeyboardChromeEventArgs(action);
        ChromeInvoked?.Invoke(this, args);
        if (args.Handled) return;
        if (action is MaterialKeyboardChrome.Collapse or MaterialKeyboardChrome.Hide) Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled) return;
        switch (e.Key)
        {
            case Key.Left:
                MoveFocus(-1, 0);
                e.Handled = true;
                return;
            case Key.Right:
                MoveFocus(1, 0);
                e.Handled = true;
                return;
            case Key.Up:
                MoveFocus(0, -1);
                e.Handled = true;
                return;
            case Key.Down:
                MoveFocus(0, 1);
                e.Handled = true;
                return;
            case Key.Back:
                Backspace();
                e.Handled = true;
                return;
            case Key.Escape when Presentation == MaterialKeyboardStyle.Floating:
                Dismiss();
                e.Handled = true;
                return;
            case Key.LeftShift:
            case Key.RightShift:
                ToggleShift();
                e.Handled = true;
                return;
            case Key.CapsLock:
                IsCapsLocked = !IsCapsLocked;
                IsShifted = IsCapsLocked;
                e.Handled = true;
                return;
        }

        // Space and Enter belong to the focused key button; only reach here when no key has focus.
        var symbol = e.KeySymbol;
        if (string.IsNullOrEmpty(symbol)) return;
        if (symbol.Length == 1 && char.IsLetter(symbol[0]) && (IsShifted || IsCapsLocked))
            symbol = symbol.ToUpperInvariant();
        if (symbol == " ") { Insert(" "); e.Handled = true; return; }
        if (symbol.Length == 1 && !char.IsControl(symbol[0]))
        {
            Insert(symbol);
            e.Handled = true;
        }
    }

    private void MoveFocus(int dx, int dy)
    {
        if (_keyRows.Count == 0) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
        int row;
        var column = 0;
        if (focused is not null && FindPosition(focused) is { } found)
        {
            (row, column) = found;
            row = Math.Clamp(row + dy, 0, _keyRows.Count - 1);
            column = Math.Clamp(column + dx, 0, _keyRows[row].Count - 1);
        }
        else
        {
            row = dy >= 0 ? 0 : _keyRows.Count - 1;
        }

        // Skip hidden spacer keys so arrow keys always land on a real key.
        for (var r = row; r >= 0 && r < _keyRows.Count; r += dy >= 0 ? 1 : -1)
        {
            var keys = _keyRows[r];
            for (var c = column; c >= 0 && c < keys.Count; c += dx >= 0 ? 1 : -1)
            {
                if (!keys[c].IsVisible) continue;
                keys[c].Focus(NavigationMethod.Tab);
                return;
            }
            column = dx >= 0 ? 0 : keys.Count - 1;
            if (dy == 0) return;
        }
    }

    private (int Row, int Column)? FindPosition(Button key)
    {
        for (var r = 0; r < _keyRows.Count; r++)
        {
            var index = _keyRows[r].IndexOf(key);
            if (index >= 0) return (r, index);
        }
        return null;
    }

    private void OnDragStart(object? sender, PointerPressedEventArgs e)
    {
        if (Presentation != MaterialKeyboardStyle.Floating) return;
        // Chrome keys are buttons; only the empty part of the bar starts a drag.
        if (e.Source is Visual visual && visual.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        _dragging = true;
        _dragOrigin = e.GetPosition(this);
        e.Pointer.Capture(sender as IInputElement);
    }

    private void OnDragMove(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _drag is null) return;
        var position = e.GetPosition(this);
        _drag.X += position.X - _dragOrigin.X;
        _drag.Y += position.Y - _dragOrigin.Y;
        _dragOrigin = position;
    }

    private void OnDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
        if (_drag is null || _surface is null || VisualRoot is null) return;
        // Keep the floating card inside the host area; the keyboard's own bounds are that area.
        var host = Bounds.Size;
        var limitX = Math.Max(0, host.Width - _surface.Bounds.Width);
        var limitY = Math.Max(0, host.Height - _surface.Bounds.Height);
        _drag.X = Math.Clamp(_drag.X, 0, limitX);
        _drag.Y = Math.Clamp(_drag.Y, 0, limitY);
    }

    private List<KeySpec[]> BuildRows()
    {
        if (Layer == MaterialKeyboardLayer.Emoji) return EmojiRows();
        return Configuration switch
        {
            MaterialKeyboardConfiguration.Alphanumeric =>
                Layer == MaterialKeyboardLayer.Symbols ? AlphanumericSymbols() : AlphanumericLetters(),
            MaterialKeyboardConfiguration.Keypad => KeypadRows(),
            MaterialKeyboardConfiguration.NumericOnly => NumericRows(),
            _ => Layer == MaterialKeyboardLayer.Symbols ? BaseSymbols() : BaseLetters()
        };
    }

    private List<KeySpec[]> BaseLetters() =>
    [
        Letters("QWERTYUIOP"),
        [Spacer(0.5), .. Letters("ASDFGHJKL"), Spacer(0.5)],
        [
            new("shift", null, KeyAction.Shift, 1.5, "Shift", MaterialSourceIcons.Shift),
            .. Letters("ZXCVBNM"),
            new("backspace", null, KeyAction.Backspace, 1.5, "Backspace", MaterialSourceIcons.Backspace)
        ],
        [
            new("?123", null, KeyAction.Symbols, 1.5, "Symbols"),
            new("emoji", null, KeyAction.Emoji, 1, "Emoji", MaterialSourceIcons.Emoji),
            new("language", null, KeyAction.Language, 1, "Switch language", MaterialSourceIcons.Language),
            new("space", " ", KeyAction.Space, 3, "Space"),
            new(".", ".", KeyAction.Insert, 1),
            new("enter", null, KeyAction.Enter, 1.5, "Enter", MaterialSourceIcons.Enter)
        ]
    ];

    private List<KeySpec[]> BaseSymbols() =>
    [
        Letters("1234567890"),
        [
            new("@", "@", KeyAction.Insert), new("#", "#", KeyAction.Insert), new("$", "$", KeyAction.Insert),
            new("%", "%", KeyAction.Insert), new("&", "&", KeyAction.Insert), new("*", "*", KeyAction.Insert),
            new("-", "-", KeyAction.Insert), new("+", "+", KeyAction.Insert), new("(", "(", KeyAction.Insert),
            new(")", ")", KeyAction.Insert)
        ],
        [
            new("[", "[", KeyAction.Insert), new("]", "]", KeyAction.Insert), new("{", "{", KeyAction.Insert),
            new("}", "}", KeyAction.Insert), new("<", "<", KeyAction.Insert), new(">", ">", KeyAction.Insert),
            new("/", "/", KeyAction.Insert), new("\\", "\\", KeyAction.Insert), new("|", "|", KeyAction.Insert),
            new("~", "~", KeyAction.Insert)
        ],
        [
            new("ABC", null, KeyAction.Letters, 1.5, "Letters"),
            new("emoji", null, KeyAction.Emoji, 1, "Emoji", MaterialSourceIcons.Emoji),
            new("space", " ", KeyAction.Space, 3, "Space"),
            new(".", ".", KeyAction.Insert, 1),
            new("enter", null, KeyAction.Enter, 1.5, "Enter", MaterialSourceIcons.Enter)
        ]
    ];

    private List<KeySpec[]> AlphanumericLetters() =>
    [
        [
            new("+", "+", KeyAction.Insert), new("1", "1", KeyAction.Insert), new("2", "2", KeyAction.Insert),
            new("3", "3", KeyAction.Insert), new("%", "%", KeyAction.Insert)
        ],
        [
            new("-", "-", KeyAction.Insert), new("4", "4", KeyAction.Insert), new("5", "5", KeyAction.Insert),
            new("6", "6", KeyAction.Insert), new("space", " ", KeyAction.Space, 1, "Space")
        ],
        [
            new("*", "*", KeyAction.Insert), new("7", "7", KeyAction.Insert), new("8", "8", KeyAction.Insert),
            new("9", "9", KeyAction.Insert),
            new("backspace", null, KeyAction.Backspace, 1, "Backspace", MaterialSourceIcons.Backspace)
        ],
        [
            new("/", "/", KeyAction.Insert), new("?123", null, KeyAction.Symbols, 1, "Symbols"),
            new("0", "0", KeyAction.Insert), new("=", "=", KeyAction.Insert),
            new("enter", null, KeyAction.Enter, 1, "Enter", MaterialSourceIcons.Enter)
        ],
        [
            new("emoji", null, KeyAction.Emoji, 1, "Emoji", MaterialSourceIcons.Emoji),
            new(",", ",", KeyAction.Insert), new(".", ".", KeyAction.Insert),
            new("language", null, KeyAction.Language, 1, "Switch language", MaterialSourceIcons.Language)
        ]
    ];

    private List<KeySpec[]> AlphanumericSymbols() =>
    [
        Letters("1234567890"),
        [
            new("@", "@", KeyAction.Insert), new("#", "#", KeyAction.Insert), new("$", "$", KeyAction.Insert),
            new("%", "%", KeyAction.Insert), new("&", "&", KeyAction.Insert), new("*", "*", KeyAction.Insert),
            new("-", "-", KeyAction.Insert), new("+", "+", KeyAction.Insert), new("(", "(", KeyAction.Insert),
            new(")", ")", KeyAction.Insert)
        ],
        [
            new("=", "=", KeyAction.Insert), new("/", "/", KeyAction.Insert), new("<", "<", KeyAction.Insert),
            new(">", ">", KeyAction.Insert), new("[", "[", KeyAction.Insert), new("]", "]", KeyAction.Insert),
            new("{", "{", KeyAction.Insert), new("}", "}", KeyAction.Insert), new("|", "|", KeyAction.Insert),
            new("\\", "\\", KeyAction.Insert)
        ],
        [
            new("ABC", null, KeyAction.Letters, 1.5, "Letters"),
            new("space", " ", KeyAction.Space, 3, "Space"),
            new(".", ".", KeyAction.Insert, 1),
            new("enter", null, KeyAction.Enter, 1.5, "Enter", MaterialSourceIcons.Enter)
        ]
    ];

    private List<KeySpec[]> KeypadRows() =>
    [
        Digits("123"),
        Digits("456"),
        Digits("789"),
        [
            new(".", ".", KeyAction.Insert), new("0", "0", KeyAction.Insert),
            new("backspace", null, KeyAction.Backspace, 1, "Backspace", MaterialSourceIcons.Backspace)
        ],
        [
            new("?123", null, KeyAction.Symbols, 1, "Symbols"),
            new("space", " ", KeyAction.Space, 1, "Space"),
            new("enter", null, KeyAction.Enter, 1, "Enter", MaterialSourceIcons.Enter)
        ]
    ];

    private List<KeySpec[]> NumericRows() => Presentation == MaterialKeyboardStyle.Landscape
        ?
        [
            [.. Digits("123"), new(",", ",", KeyAction.Insert)],
            [.. Digits("456"), new("space", " ", KeyAction.Space, 1, "Space")],
            [.. Digits("789"), new("backspace", null, KeyAction.Backspace, 1, "Backspace", MaterialSourceIcons.Backspace)],
            [
                new(".", ".", KeyAction.Insert), new("0", "0", KeyAction.Insert),
                new("enter", null, KeyAction.Enter, 2, "Enter", MaterialSourceIcons.Enter)
            ]
        ]
        :
        [
            Digits("123"),
            Digits("456"),
            Digits("789"),
            [
                new(".", ".", KeyAction.Insert), new("0", "0", KeyAction.Insert),
                new("backspace", null, KeyAction.Backspace, 1, "Backspace", MaterialSourceIcons.Backspace)
            ],
            [
                new("space", " ", KeyAction.Space, 2, "Space"),
                new("enter", null, KeyAction.Enter, 1, "Enter", MaterialSourceIcons.Enter)
            ]
        ];

    private static List<KeySpec[]> EmojiRows() =>
    [
        EmojiLine("😀😂🥰😎🤔😭😡🎉"),
        EmojiLine("❤️👍👏🔥✨🌟🎵🎧"),
        EmojiLine("🍕🍔🍜☕🍰🌮🥟🍣"),
        EmojiLine("🐶🐱🌞🌈🌊🍀⚡💡"),
        [
            new("ABC", null, KeyAction.Letters, 1.5, "Letters"),
            new("space", " ", KeyAction.Space, 3, "Space"),
            new("backspace", null, KeyAction.Backspace, 1, "Backspace", MaterialSourceIcons.Backspace),
            new("enter", null, KeyAction.Enter, 1.5, "Enter", MaterialSourceIcons.Enter)
        ]
    ];

    private static KeySpec[] EmojiLine(string emoji)
    {
        var specs = new List<KeySpec>();
        // Enumerate runes so surrogate pairs and variation selectors stay intact.
        foreach (var rune in emoji.EnumerateRunes())
        {
            var text = rune.ToString();
            specs.Add(new KeySpec(text, text, KeyAction.Insert));
        }
        return specs.ToArray();
    }

    private static KeySpec[] Letters(string letters)
    {
        var specs = new KeySpec[letters.Length];
        for (var i = 0; i < letters.Length; i++)
            specs[i] = new KeySpec(letters[i].ToString(), letters[i].ToString(), KeyAction.Insert);
        return specs;
    }

    private static KeySpec[] Digits(string digits) => Letters(digits);

    private static KeySpec Spacer(double weight) => new("spacer", null, KeyAction.Spacer, weight);

    private enum KeyAction { Insert, Space, Backspace, Enter, Shift, Symbols, Letters, Emoji, Language, Dismiss, Chrome, Spacer }

    private readonly record struct KeySpec(
        string Label,
        string? Insert,
        KeyAction Action,
        double Weight = 1,
        string? Automation = null,
        string? Icon = null,
        MaterialKeyboardChrome? Chrome = null);
}
