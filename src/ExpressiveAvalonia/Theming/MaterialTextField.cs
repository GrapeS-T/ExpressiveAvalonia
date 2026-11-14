using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Theming;

/// <summary>
/// How a secure <see cref="TextBox"/> obscures its text, matching the source
/// <c>TextObfuscationMode</c>. <see cref="TextObfuscationMode.System"/> is the default and
/// leaves the native <see cref="TextBox.PasswordChar"/> masking in place; desktop Avalonia has
/// no platform password-visibility setting, so it resolves to
/// <see cref="TextObfuscationMode.Hidden"/>.
/// </summary>
public enum TextObfuscationMode
{
    /// <summary>Shows all content while keeping the native password field semantics.</summary>
    Visible,
    /// <summary>Briefly shows the character that was just inserted.</summary>
    RevealLastTyped,
    /// <summary>Hides every character, as the desktop password convention does.</summary>
    Hidden,
    /// <summary>Gives the platform the choice; on desktop Avalonia this resolves to Hidden.</summary>
    System,
}

/// <summary>The Material text field container treatment.</summary>
public enum MaterialTextFieldVariant
{
    Filled,
    Outlined,
}

/// <summary>
/// Where the label sits relative to the container, matching the source
/// <c>TextFieldLabelPosition</c>. <see cref="Auto"/> keeps the source defaults: Inside for the
/// filled variant and Cutout for the outlined variant.
/// </summary>
public enum MaterialTextFieldLabelPosition
{
    Auto,
    Inside,
    Cutout,
    Above,
}

/// <summary>
/// Horizontal alignment of the label, matching <c>Alignment.Horizontal</c> in the source
/// <c>TextFieldLabelPosition</c> constructors. <see cref="Start"/> and <see cref="End"/> are
/// flow-relative: they resolve to left/right through the field's <c>FlowDirection</c>.
/// </summary>
public enum MaterialTextFieldLabelAlignment
{
    Start,
    Center,
    End,
}


/// <summary>
/// Material text field options for the native <see cref="TextBox"/>. Editing, undo,
/// selection, input methods, clipboard commands and password masking remain native.
/// </summary>
/// <remarks>
/// The attached options cover the source family's axes: <see cref="VariantProperty"/> is the
/// Filled/Outlined style, <see cref="LabelProperty"/> plus <see cref="SupportingTextProperty"/>
/// cover the label/placeholder/input-text configurations, <see cref="LeadingContentProperty"/>
/// and <see cref="TrailingContentProperty"/> are the leading/trailing icon slots, and
/// <see cref="ShowPasswordRevealProperty"/> and <see cref="ShowClearButtonProperty"/> add the
/// trailing actions, and <see cref="TextObfuscationModeProperty"/> selects native masking,
/// all-visible text or the source's timed last-character reveal. Enabled, hovered, focused,
/// error and disabled states come from the native control's own pseudo-classes.
/// <para>The source bundles alignment with the label position
/// (<c>Inside/Cutout(isAlwaysMinimized, minimizedAlignment, expandedAlignment)</c> and
/// <c>Above(alignment)</c>). This attached API keeps the position as
/// <see cref="MaterialTextFieldLabelPosition"/> and exposes the constructor arguments separately:
/// <see cref="IsLabelAlwaysMinimizedProperty"/>, <see cref="MinimizedLabelAlignmentProperty"/>,
/// <see cref="ExpandedLabelAlignmentProperty"/> and <see cref="AboveLabelAlignmentProperty"/>.
/// Start/End resolve through <see cref="Visual.FlowDirection"/>. The inline label is positioned
/// by the decoration motion, which interpolates between the expanded and minimized resolved
/// alignments over the same progress as the label's vertical movement; the resolved values remain
/// available as <c>EffectiveLabelAlignment</c>/<c>EffectiveAboveLabelAlignment</c> for the template
/// and for application inspection.</para>
/// </remarks>
public sealed class MaterialTextField : AvaloniaObject
{
    public static readonly AttachedProperty<MaterialTextFieldVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldVariant>("Variant");
    public static readonly AttachedProperty<string?> LabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("Label");
    public static readonly AttachedProperty<MaterialTextFieldLabelPosition> LabelPositionProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldLabelPosition>("LabelPosition");
    /// <summary>Keeps the in-container label in its minimized (floating) form at all times.</summary>
    public static readonly AttachedProperty<bool> IsLabelAlwaysMinimizedProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsLabelAlwaysMinimized");
    /// <summary>
    /// Horizontal alignment of the label while it is minimized (floating). Matches the
    /// <c>minimizedAlignment</c> constructor argument of the source Inside/Cutout positions.
    /// </summary>
    public static readonly AttachedProperty<MaterialTextFieldLabelAlignment> MinimizedLabelAlignmentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldLabelAlignment>("MinimizedLabelAlignment");
    /// <summary>
    /// Horizontal alignment of the label while it is expanded (resting inside the empty,
    /// unfocused field). Matches the <c>expandedAlignment</c> constructor argument of the source
    /// Inside/Cutout positions.
    /// </summary>
    public static readonly AttachedProperty<MaterialTextFieldLabelAlignment> ExpandedLabelAlignmentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldLabelAlignment>("ExpandedLabelAlignment");
    /// <summary>
    /// Horizontal alignment of an <see cref="MaterialTextFieldLabelPosition.Above"/> label, which
    /// is always minimized. Matches the <c>alignment</c> constructor argument of the source
    /// Above position.
    /// </summary>
    public static readonly AttachedProperty<MaterialTextFieldLabelAlignment> AboveLabelAlignmentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldLabelAlignment>("AboveLabelAlignment");
    public static readonly AttachedProperty<string?> SupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("SupportingText");
    public static readonly AttachedProperty<string?> PrefixProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("Prefix");
    public static readonly AttachedProperty<string?> SuffixProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("Suffix");
    public static readonly AttachedProperty<object?> LeadingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, object?>("LeadingContent");
    public static readonly AttachedProperty<object?> TrailingContentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, object?>("TrailingContent");
    public static readonly AttachedProperty<bool> ShowPasswordRevealProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("ShowPasswordReveal");
    /// <summary>
    /// Selects how secure text is obscured. The default <see cref="TextObfuscationMode.System"/>
    /// preserves the previous native <see cref="TextBox.PasswordChar"/> behavior.
    /// Explicit Hidden and RevealLastTyped modes ensure a nonzero native password character,
    /// preserving native clipboard and automation semantics if none is configured.
    /// </summary>
    public static readonly AttachedProperty<TextObfuscationMode> TextObfuscationModeProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, TextObfuscationMode>(
            "TextObfuscationMode", TextObfuscationMode.System);
    /// <summary>
    /// Display-only text for the template presenter: the real text in visible modes and a
    /// same-length mask with at most the last insertion revealed. It never replaces
    /// <see cref="TextBox.Text"/>, so native editing, undo, selection and IME stay intact.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<string?> DisplayTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("DisplayText");
    /// <summary>
    /// Password character the template presenter applies. <c>'\0'</c> means the presenter draws
    /// <see cref="DisplayTextProperty"/> unmasked, which lets RevealLastTyped show only one glyph.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<char> EffectivePasswordCharProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, char>("EffectivePasswordChar");
    /// <summary>Shows a trailing clear action while the field has text. Escape also clears.</summary>
    public static readonly AttachedProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("ShowClearButton");
    /// <summary>
    /// Interaction/error/disabled target brush for the label. The theme writes it, while the
    /// template motion animates the rendered label toward it with the source FastEffects spring.
    /// This is template plumbing, not consumer API.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, IBrush?>("LabelBrush");

    // Theme state is separate from the public options so it never changes native TextBox properties.
    // These members are template plumbing, not consumer API.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsEnabled");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<string?> EffectiveLabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("EffectiveLabel");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> HasLabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("HasLabel");
    /// <summary>True when the label is painted inside the container (Inside/Cutout).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> HasInlineLabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("HasInlineLabel");
    /// <summary>True when the label is painted above the container.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsLabelAboveProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsLabelAbove");
    /// <summary>True when the outlined border is cut around the floating label.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsLabelCutoutProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsLabelCutout");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsLabelFloatingProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsLabelFloating");
    /// <summary>Resolved alignment of the inline label for the current floating state and flow.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<HorizontalAlignment> EffectiveLabelAlignmentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, HorizontalAlignment>("EffectiveLabelAlignment");
    /// <summary>Resolved alignment of the <see cref="MaterialTextFieldLabelPosition.Above"/> label.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<HorizontalAlignment> EffectiveAboveLabelAlignmentProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, HorizontalAlignment>("EffectiveAboveLabelAlignment");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsPlaceholderAllowedProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsPlaceholderAllowed", true);
    /// <summary>
    /// False when the floating placeholder is serving as the label; the placeholder must not
    /// also render inside the empty input slot in that configuration.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsPlaceholderVisibleProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsPlaceholderVisible", true);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsRevealAvailableProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsRevealAvailable");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsClearAvailableProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsClearAvailable");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<string?> ValidationMessageProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("ValidationMessage");
    /// <summary>Validation replaces the supporting text in the same slot, as Material specifies.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<string?> EffectiveSupportingTextProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("EffectiveSupportingText");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<string?> AccessibleDescriptionProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("AccessibleDescription");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> InputMarginProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, Thickness>("InputMargin");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> PrefixMarginProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, Thickness>("PrefixMargin");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> SuffixMarginProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, Thickness>("SuffixMargin");
    private static readonly AttachedProperty<FieldState?> StateProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, FieldState?>("State");

    static MaterialTextField()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextBox>((box, args) =>
        {
            if (args.NewValue is true)
            {
                if (box.GetValue(StateProperty) is null)
                    box.SetValue(StateProperty, new FieldState(box));
            }
            else if (box.GetValue(StateProperty) is { } state)
            {
                state.Dispose();
                box.ClearValue(StateProperty);
            }
        });
    }

    public static MaterialTextFieldVariant GetVariant(TextBox box) => box.GetValue(VariantProperty);
    public static void SetVariant(TextBox box, MaterialTextFieldVariant value) => box.SetValue(VariantProperty, value);
    public static string? GetLabel(TextBox box) => box.GetValue(LabelProperty);
    public static void SetLabel(TextBox box, string? value) => box.SetValue(LabelProperty, value);
    public static MaterialTextFieldLabelPosition GetLabelPosition(TextBox box) => box.GetValue(LabelPositionProperty);
    public static void SetLabelPosition(TextBox box, MaterialTextFieldLabelPosition value) =>
        box.SetValue(LabelPositionProperty, value);
    public static bool GetIsLabelAlwaysMinimized(TextBox box) => box.GetValue(IsLabelAlwaysMinimizedProperty);
    public static void SetIsLabelAlwaysMinimized(TextBox box, bool value) =>
        box.SetValue(IsLabelAlwaysMinimizedProperty, value);
    public static MaterialTextFieldLabelAlignment GetMinimizedLabelAlignment(TextBox box) =>
        box.GetValue(MinimizedLabelAlignmentProperty);
    public static void SetMinimizedLabelAlignment(TextBox box, MaterialTextFieldLabelAlignment value) =>
        box.SetValue(MinimizedLabelAlignmentProperty, value);
    public static MaterialTextFieldLabelAlignment GetExpandedLabelAlignment(TextBox box) =>
        box.GetValue(ExpandedLabelAlignmentProperty);
    public static void SetExpandedLabelAlignment(TextBox box, MaterialTextFieldLabelAlignment value) =>
        box.SetValue(ExpandedLabelAlignmentProperty, value);
    public static MaterialTextFieldLabelAlignment GetAboveLabelAlignment(TextBox box) =>
        box.GetValue(AboveLabelAlignmentProperty);
    public static void SetAboveLabelAlignment(TextBox box, MaterialTextFieldLabelAlignment value) =>
        box.SetValue(AboveLabelAlignmentProperty, value);
    public static string? GetSupportingText(TextBox box) => box.GetValue(SupportingTextProperty);
    public static void SetSupportingText(TextBox box, string? value) => box.SetValue(SupportingTextProperty, value);
    public static string? GetPrefix(TextBox box) => box.GetValue(PrefixProperty);
    public static void SetPrefix(TextBox box, string? value) => box.SetValue(PrefixProperty, value);
    public static string? GetSuffix(TextBox box) => box.GetValue(SuffixProperty);
    public static void SetSuffix(TextBox box, string? value) => box.SetValue(SuffixProperty, value);
    public static object? GetLeadingContent(TextBox box) => box.GetValue(LeadingContentProperty);
    public static void SetLeadingContent(TextBox box, object? value) => box.SetValue(LeadingContentProperty, value);
    public static object? GetTrailingContent(TextBox box) => box.GetValue(TrailingContentProperty);
    public static void SetTrailingContent(TextBox box, object? value) => box.SetValue(TrailingContentProperty, value);
    public static bool GetShowPasswordReveal(TextBox box) => box.GetValue(ShowPasswordRevealProperty);
    public static void SetShowPasswordReveal(TextBox box, bool value) => box.SetValue(ShowPasswordRevealProperty, value);
    public static TextObfuscationMode GetTextObfuscationMode(TextBox box) => box.GetValue(TextObfuscationModeProperty);
    public static void SetTextObfuscationMode(TextBox box, TextObfuscationMode value) =>
        box.SetValue(TextObfuscationModeProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetDisplayText(TextBox box) => box.GetValue(DisplayTextProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static char GetEffectivePasswordChar(TextBox box) => box.GetValue(EffectivePasswordCharProperty);
    public static bool GetShowClearButton(TextBox box) => box.GetValue(ShowClearButtonProperty);
    public static void SetShowClearButton(TextBox box, bool value) => box.SetValue(ShowClearButtonProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IBrush? GetLabelBrush(TextBox box) => box.GetValue(LabelBrushProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetLabelBrush(TextBox box, IBrush? value) => box.SetValue(LabelBrushProperty, value);
    public static bool GetIsEnabled(TextBox box) => box.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(TextBox box, bool value) => box.SetValue(IsEnabledProperty, value);
    public static string? GetEffectiveLabel(TextBox box) => box.GetValue(EffectiveLabelProperty);
    public static bool GetHasLabel(TextBox box) => box.GetValue(HasLabelProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetHasInlineLabel(TextBox box) => box.GetValue(HasInlineLabelProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetIsLabelAbove(TextBox box) => box.GetValue(IsLabelAboveProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetIsLabelCutout(TextBox box) => box.GetValue(IsLabelCutoutProperty);
    public static bool GetIsLabelFloating(TextBox box) => box.GetValue(IsLabelFloatingProperty);
    public static bool GetIsPlaceholderAllowed(TextBox box) => box.GetValue(IsPlaceholderAllowedProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetIsPlaceholderVisible(TextBox box) => box.GetValue(IsPlaceholderVisibleProperty);
    public static bool GetIsRevealAvailable(TextBox box) => box.GetValue(IsRevealAvailableProperty);
    public static bool GetIsClearAvailable(TextBox box) => box.GetValue(IsClearAvailableProperty);
    public static string? GetValidationMessage(TextBox box) => box.GetValue(ValidationMessageProperty);
    public static string? GetEffectiveSupportingText(TextBox box) => box.GetValue(EffectiveSupportingTextProperty);
    public static string? GetAccessibleDescription(TextBox box) => box.GetValue(AccessibleDescriptionProperty);
    public static Thickness GetInputMargin(TextBox box) => box.GetValue(InputMarginProperty);
    public static Thickness GetPrefixMargin(TextBox box) => box.GetValue(PrefixMarginProperty);
    public static Thickness GetSuffixMargin(TextBox box) => box.GetValue(SuffixMarginProperty);

    private sealed class FieldState : IDisposable
    {
        private readonly TextBox _box;
        private TextPresenter? _presenter;
        private Border? _outline;
        private Border? _label;
        private TextBlock? _floatingLabel;
        private TextBlock? _aboveLabel;
        private TextBlock? _placeholder;
        private TextBlock? _prefix;
        private TextBlock? _suffix;
        private Panel? _fieldContainer;
        private StackPanel? _leadingSlot;
        private StackPanel? _trailingSlot;
        private Button? _clear;
        private Rect? _cutout;
        private Size _outlineSize;

        // Template decoration motion. The rendered label follows one continuous progress value,
        // exactly like the source transition; position, type scale and cutout all derive from it,
        // so expanded/minimized alignment changes interpolate instead of snapping.
        private readonly TranslateTransform _labelTransform = new();
        private readonly SolidColorBrush _labelBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _indicatorBrush = new(Colors.Transparent);
        private MaterialMotion.SpringAnimation? _labelSpring;
        private MaterialMotion.SpringAnimation? _placeholderSpring;
        private MaterialMotion.SpringAnimation? _affixSpring;
        private MaterialMotion.SpringAnimation? _thicknessSpring;
        private MaterialSpring _placeholderSpringKind = MaterialSpring.EffectsFast;
        private MaterialColorAnimation? _labelColorAnimation;
        private MaterialColorAnimation? _indicatorColorAnimation;
        private double _labelProgress = 1;
        private double _placeholderOpacity = 1;
        private double _affixOpacity = 1;
        private double _borderThickness = 1;
        private Color _labelColor = Colors.Transparent;
        private Color _indicatorColor = Colors.Transparent;
        private double _largeFontSize = 16;
        private double _largeLineHeight = 24;
        private double _largeLetterSpacing = 0.5;
        private double _smallFontSize = 12;
        private double _smallLineHeight = 16;
        private double _smallLetterSpacing = 0.4;
        private MaterialTextFieldInputPhase _phase;
        private bool _decorationReady;
        // Source BasicSecureTextField: one reveal range, restarted by each single-character
        // insertion, cleared after LAST_TYPED_CHARACTER_REVEAL_DURATION_MILLIS.
        private DispatcherTimer? _revealTimer;
        private int _revealStart = -1;
        private int _revealLength;
        // Text typed through the native input pipeline. Programmatic Text changes, commands and
        // pastes never set it, so they cannot start the reveal.
        private string? _pendingTextInput;
        private bool _selectionRevealCheckQueued;
        private IDisposable? _passwordCharOverride;
        private bool _updatingPasswordChar;
        private bool _disposed;

        private const double LabelHorizontalPadding = 4;
        private const double LabelMotionThreshold = 0.001;
        private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(1500);
        private const char DefaultObfuscationCharacter = '\u2022';

        public FieldState(TextBox box)
        {
            _box = box;
            box.PropertyChanged += OnPropertyChanged;
            box.TemplateApplied += OnTemplateApplied;
            box.DetachedFromVisualTree += OnDetached;
            box.KeyDown += OnKeyDown;
            // Tunnel before TextBox handles the event on the bubble pass, so the typed text is
            // known when the resulting Text change is observed. The bubble handler runs after
            // that native edit and discards input the field did not accept.
            box.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
            box.AddHandler(InputElement.TextInputEvent, OnTextInputHandled,
                RoutingStrategies.Bubble, handledEventsToo: true);
            UpdateLabel();
            UpdateObfuscation();
            UpdateReveal();
            UpdateClear();
            UpdateValidation();
            UpdateMargins();
        }

        private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs args)
        {
            DetachParts();
            _outline = args.NameScope.Find<Border>("PART_BorderElement");
            _label = args.NameScope.Find<Border>("LabelContainer");
            _floatingLabel = args.NameScope.Find<TextBlock>("FloatingLabel");
            _aboveLabel = args.NameScope.Find<TextBlock>("AboveLabel");
            _placeholder = args.NameScope.Find<TextBlock>("PART_Placeholder");
            _prefix = args.NameScope.Find<TextBlock>("Prefix");
            _suffix = args.NameScope.Find<TextBlock>("Suffix");
            _fieldContainer = args.NameScope.Find<Panel>("FieldContainer");
            _leadingSlot = args.NameScope.Find<StackPanel>("LeadingSlot");
            _trailingSlot = args.NameScope.Find<StackPanel>("TrailingSlot");
            _clear = args.NameScope.Find<Button>("ClearButton");
            _cutout = null;
            if (_label is not null)
            {
                // Position is animation-owned; the label is a content-sized overlay so its
                // bounds stay exactly the cutout size and never influence the field grid.
                _labelTransform.X = 0;
                _labelTransform.Y = 0;
                _label.RenderTransform = _labelTransform;
            }
            SubscribePart(_outline);
            SubscribePart(_label);
            SubscribePart(_floatingLabel);
            SubscribePart(_fieldContainer);
            SubscribePart(_leadingSlot);
            SubscribePart(_trailingSlot);
            if (_clear is not null)
                _clear.Click += OnClearClick;
            SetPresenter(args.NameScope.Find<TextPresenter>("PART_TextPresenter"));
            UpdateObfuscation();
            ReadLabelType();
            _decorationReady = false;
            UpdateLabel();
            UpdateMargins();
            _decorationReady = true;
        }

        private void SubscribePart(AvaloniaObject? part)
        {
            if (part is not null)
                part.PropertyChanged += OnPartChanged;
        }

        private void UnsubscribePart(AvaloniaObject? part)
        {
            if (part is not null)
                part.PropertyChanged -= OnPartChanged;
        }

        // The cutout depends on the outline size and on the label's animated position, both of
        // which arrive as Bounds changes. Subscribing to LayoutUpdated would recompute on every
        // layout pass of the whole window.
        private void OnPartChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property != Visual.BoundsProperty)
                return;
            if (ReferenceEquals(sender, _outline))
            {
                UpdateCutout();
                return;
            }
            // Font interpolation changes the label's measured size and the alignment insets
            // change with the icon/label slots, so the animated progress is re-applied.
            ApplyLabelProgress();
            UpdateCutout();
        }

        private void DetachParts()
        {
            UnsubscribePart(_outline);
            UnsubscribePart(_label);
            UnsubscribePart(_floatingLabel);
            UnsubscribePart(_fieldContainer);
            UnsubscribePart(_leadingSlot);
            UnsubscribePart(_trailingSlot);
            if (_outline is not null)
                _outline.Clip = null;
            if (_clear is not null)
                _clear.Click -= OnClearClick;
            DisposeDecoration();
            _outline = null;
            _label = null;
            _floatingLabel = null;
            _aboveLabel = null;
            _placeholder = null;
            _prefix = null;
            _suffix = null;
            _fieldContainer = null;
            _leadingSlot = null;
            _trailingSlot = null;
            _clear = null;
            _decorationReady = false;
        }

        private void UpdateCutout()
        {
            if (_outline is null)
                return;
            // The cutout follows the animated label for the whole float, so the gap grows with
            // the label instead of popping. The excluded rectangle only intersects the top
            // outline once the label actually reaches the border line.
            if (!GetIsLabelCutout(_box) || _label is null || !GetHasInlineLabel(_box))
            {
                if (_cutout is not null)
                    _outline.Clip = null;
                _cutout = null;
                return;
            }

            // Cut the actual outline, rather than painting the label with an assumed surface color.
            // The label container includes the spec's 4dp clear space on each side of the label.
            if (_label.TranslatePoint(default, _outline) is not { } origin)
                return;
            var size = _outline.Bounds.Size;
            var cutout = new Rect(origin, _label.Bounds.Size);
            if (_cutout == cutout && _outlineSize == size)
                return;
            _cutout = cutout;
            _outlineSize = size;
            _outline.Clip = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(size)), new RectangleGeometry(cutout));
        }

        private void UpdateMargins()
        {
            var padding = _box.Padding;
            // Affixes stay in the layout while their opacity animates, matching the source
            // placement; only the opacity changes between expanded and minimized label states.
            var hasPrefix = !string.IsNullOrEmpty(GetPrefix(_box));
            var hasSuffix = !string.IsNullOrEmpty(GetSuffix(_box));
            var hasAction = (GetShowClearButton(_box) && !_box.IsReadOnly) ||
                (GetShowPasswordReveal(_box) && _box.PasswordChar != '\0' && !_box.IsReadOnly && !_box.AcceptsReturn);
            var actionOnly = GetTrailingContent(_box) is null && _box.InnerRightContent is null;
            var right = hasAction && actionOnly ? Math.Max(0, padding.Right - 12) : padding.Right;
            // A 24dp icon with 12dp outer space occupies 36dp. Retaining 16dp inner
            // padding places the text at 52dp, matching Compose's 48dp icon slot + 4dp.
            _box.SetCurrentValue(InputMarginProperty,
                new Thickness(hasPrefix ? 0 : padding.Left, padding.Top,
                    hasSuffix ? 0 : right, padding.Bottom));
            _box.SetCurrentValue(PrefixMarginProperty,
                new Thickness(padding.Left, padding.Top, 2, padding.Bottom));
            _box.SetCurrentValue(SuffixMarginProperty,
                new Thickness(2, padding.Top, right, padding.Bottom));
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            ConcealPassword();
            ClearReveal();
            ApplyObfuscation();
        }

        // Source TextFieldImpl 2036-2139:
        //   label progress      FastSpatial
        //   placeholder opacity FastEffects, but SlowEffects for UnfocusedEmpty -> Focused
        //                       and UnfocusedNotEmpty -> UnfocusedEmpty
        //   affix opacity       FastEffects
        //   label/indicator     FastEffects colors
        //   border thickness    FastSpatial, snapping when the field is disabled
        // Every value is retargeted in place, so clearing/focus loss mid-flight preserves the
        // current position and velocity instead of restarting from a style-resting value.
        private void UpdateDecoration(bool animate)
        {
            if (_outline is null && _floatingLabel is null)
                return;

            var phase = CurrentPhase();
            var hasLabel = GetHasLabel(_box);
            var above = GetIsLabelAbove(_box);
            var showExpandedLabel = hasLabel && !above && !GetIsLabelAlwaysMinimized(_box);

            var placeholderVelocity = 0d;
            if (phase != _phase)
            {
                var previous = _phase;
                _phase = phase;
                var next = PlaceholderSpring(previous, phase);
                if (next != _placeholderSpringKind)
                {
                    // Changing the transition spec keeps analytical position and velocity.
                    if (_placeholderSpring is { } active)
                    {
                        var sampled = active.SampleState();
                        ApplyPlaceholderOpacity(sampled.Position);
                        placeholderVelocity = sampled.Velocity;
                    }
                    _placeholderSpring?.Dispose();
                    _placeholderSpring = null;
                    _placeholderSpringKind = next;
                }
            }

            var progressTarget = phase switch
            {
                MaterialTextFieldInputPhase.Focused => 1d,
                MaterialTextFieldInputPhase.UnfocusedEmpty => showExpandedLabel ? 0d : 1d,
                _ => 1d,
            };
            var placeholderTarget = phase switch
            {
                MaterialTextFieldInputPhase.Focused => 1d,
                MaterialTextFieldInputPhase.UnfocusedEmpty => showExpandedLabel ? 0d : 1d,
                _ => 0d,
            };
            var affixTarget = phase switch
            {
                MaterialTextFieldInputPhase.Focused => 1d,
                MaterialTextFieldInputPhase.UnfocusedEmpty => showExpandedLabel ? 0d : 1d,
                _ => 1d,
            };

            AnimateNumeric(ref _labelSpring, MaterialSpring.SpatialFast, _labelProgress,
                progressTarget, ApplyLabelProgressValue, animate, LabelMotionThreshold);
            AnimateNumeric(ref _placeholderSpring, _placeholderSpringKind, _placeholderOpacity,
                placeholderTarget, ApplyPlaceholderOpacity, animate, LabelMotionThreshold, placeholderVelocity);
            AnimateNumeric(ref _affixSpring, MaterialSpring.EffectsFast, _affixOpacity,
                affixTarget, ApplyAffixOpacity, animate, LabelMotionThreshold);

            var enabled = _box.IsEffectivelyEnabled;
            UpdateBorderThickness(animate && enabled);
            UpdateLabelColour(animate);
            UpdateIndicatorColour(animate && enabled);
        }

        private MaterialTextFieldInputPhase CurrentPhase()
        {
            if (_box.IsKeyboardFocusWithin)
                return MaterialTextFieldInputPhase.Focused;
            var hasText = !string.IsNullOrEmpty(_box.Text) ||
                          !string.IsNullOrEmpty(_presenter?.PreeditText);
            return hasText ? MaterialTextFieldInputPhase.UnfocusedNotEmpty
                : MaterialTextFieldInputPhase.UnfocusedEmpty;
        }

        private static MaterialSpring PlaceholderSpring(MaterialTextFieldInputPhase from,
            MaterialTextFieldInputPhase to) =>
            // Source placeholderOpacity: only these two transitions use SlowEffects.
            (from, to) switch
            {
                (MaterialTextFieldInputPhase.UnfocusedEmpty, MaterialTextFieldInputPhase.Focused) =>
                    MaterialSpring.EffectsSlow,
                (MaterialTextFieldInputPhase.UnfocusedNotEmpty, MaterialTextFieldInputPhase.UnfocusedEmpty) =>
                    MaterialSpring.EffectsSlow,
                _ => MaterialSpring.EffectsFast,
            };

        private void AnimateNumeric(ref MaterialMotion.SpringAnimation? spring, MaterialSpring kind,
            double current, double target, Action<double> apply, bool animate, double threshold,
            double initialVelocity = 0)
        {
            if (!animate)
            {
                spring?.Dispose();
                spring = null;
                apply(target);
                return;
            }
            if (spring is { } running)
            {
                running.Retarget(target);
                return;
            }
            if (Math.Abs(current - target) <= threshold && initialVelocity == 0)
            {
                apply(target);
                return;
            }
            spring = MaterialMotion.Animate(_box, current, target, apply, kind, threshold, initialVelocity);
        }

        private void ApplyLabelProgressValue(double value)
        {
            _labelProgress = value;
            ApplyLabelProgress();
        }

        private void ApplyPlaceholderOpacity(double value)
        {
            _placeholderOpacity = value;
            if (_placeholder is not null)
                _placeholder.Opacity = value;
        }

        private void ApplyAffixOpacity(double value)
        {
            _affixOpacity = value;
            if (_prefix is not null)
                _prefix.Opacity = value;
            if (_suffix is not null)
                _suffix.Opacity = value;
        }

        private void ApplyLabelProgress()
        {
            if (_floatingLabel is null)
                return;

            // The source lerps bodyLarge (expanded) to bodySmall (minimized) with labelProgress.
            _floatingLabel.FontSize = Lerp(_largeFontSize, _smallFontSize, _labelProgress);
            _floatingLabel.LineHeight = Lerp(_largeLineHeight, _smallLineHeight, _labelProgress);
            _floatingLabel.LetterSpacing = Lerp(_largeLetterSpacing, _smallLetterSpacing, _labelProgress);
            ApplyLabelGeometry();
            UpdateCutout();
        }

        // Source TextFieldImpl 1153-1181 / 1805-1852: X is lerp(expandedAlignment, minimized
        // alignment) over the same progress as Y. Keeping one progress value here is what makes
        // differing expanded/minimized alignments slide instead of jumping when IsLabelFloating
        // flips. Insets follow the leading/trailing slots and the outlined cutout difference.
        private void ApplyLabelGeometry()
        {
            if (_label is null || !GetHasInlineLabel(_box))
                return;

            var parent = _label.Parent as Control;
            var width = parent?.Bounds.Width ?? _box.Bounds.Width;
            if (width <= 0)
                return;

            var labelWidth = Math.Max(0, _label.Bounds.Width - LabelHorizontalPadding * 2);
            var leading = _leadingSlot?.Bounds.Width ?? 0;
            var trailing = _trailingSlot?.Bounds.Width ?? 0;
            var padding = _box.Padding;
            var cutout = GetIsLabelCutout(_box);
            var rtl = _box.FlowDirection == FlowDirection.RightToLeft;
            var expandedAlignment = Resolve(GetExpandedLabelAlignment(_box), rtl);
            var minimizedAlignment = Resolve(GetMinimizedLabelAlignment(_box), rtl);

            var expandedLeft = leading + padding.Left;
            var expandedRight = trailing + padding.Right;
            var minimizedLeft = cutout ? padding.Left : expandedLeft;
            var minimizedRight = cutout ? padding.Right : expandedRight;

            var startX = expandedLeft + Align(expandedAlignment, labelWidth,
                Math.Max(0, width - expandedLeft - expandedRight));
            var endX = minimizedLeft + Align(minimizedAlignment, labelWidth,
                Math.Max(0, width - minimizedLeft - minimizedRight));
            var x = Lerp(startX, endX, _labelProgress) - LabelHorizontalPadding;

            var height = parent?.Bounds.Height ?? _box.Bounds.Height;
            var labelHeight = _label.Bounds.Height;
            var expandedTop = _box.AcceptsReturn
                ? 16d
                : Math.Max(0, (height - labelHeight) / 2);
            var minimizedTop = cutout ? -labelHeight / 2 : 8d;
            var y = Lerp(expandedTop, minimizedTop, _labelProgress);

            _labelTransform.X = x;
            _labelTransform.Y = y;
        }

        private static double Align(HorizontalAlignment alignment, double size, double space) =>
            alignment switch
            {
                HorizontalAlignment.Center => (space - size) / 2,
                HorizontalAlignment.Right => space - size,
                _ => 0,
            };

        private static double Lerp(double from, double to, double progress) =>
            from + (to - from) * progress;

        private void UpdateBorderThickness(bool animate)
        {
            var target = ThicknessValue(_box.BorderThickness, GetVariant(_box));
            AnimateNumeric(ref _thicknessSpring, MaterialSpring.SpatialFast, _borderThickness,
                target, ApplyBorderThickness, animate, LabelMotionThreshold);
        }

        private static double ThicknessValue(Thickness thickness, MaterialTextFieldVariant variant) =>
            variant == MaterialTextFieldVariant.Outlined
                ? Math.Max(thickness.Left, Math.Max(thickness.Top,
                    Math.Max(thickness.Right, thickness.Bottom)))
                : thickness.Bottom;

        private void ApplyBorderThickness(double value)
        {
            _borderThickness = value;
            if (_outline is null)
                return;
            _outline.BorderThickness = GetVariant(_box) == MaterialTextFieldVariant.Outlined
                ? new Thickness(value)
                : new Thickness(0, 0, 0, value);
        }

        private void UpdateLabelColour(bool animate)
        {
            var target = GetLabelBrush(_box);
            if (target is not ISolidColorBrush solid)
            {
                _labelColorAnimation?.Dispose();
                _labelColorAnimation = null;
                SetLabelForeground(target);
                return;
            }

            if (!animate || _labelColorAnimation is null)
            {
                _labelColorAnimation?.Dispose();
                ApplyLabelColour(solid.Color);
                _labelColorAnimation = new MaterialColorAnimation(_box, solid.Color,
                    ApplyLabelColour, MaterialSpring.EffectsFast);
                return;
            }
            _labelColorAnimation.Retarget(solid.Color);
        }

        private void ApplyLabelColour(Color color)
        {
            _labelColor = color;
            _labelBrush.Color = color;
            SetLabelForeground(_labelBrush);
        }

        private void SetLabelForeground(IBrush? brush)
        {
            if (brush is null)
            {
                _floatingLabel?.ClearValue(TextBlock.ForegroundProperty);
                _aboveLabel?.ClearValue(TextBlock.ForegroundProperty);
                return;
            }
            if (_floatingLabel is not null)
                _floatingLabel.Foreground = brush;
            if (_aboveLabel is not null)
                _aboveLabel.Foreground = brush;
        }

        private void UpdateIndicatorColour(bool animate)
        {
            var target = _box.BorderBrush;
            if (target is not ISolidColorBrush solid)
            {
                _indicatorColorAnimation?.Dispose();
                _indicatorColorAnimation = null;
                if (_outline is not null)
                    _outline.BorderBrush = target;
                return;
            }

            if (!animate)
            {
                // Source animateBorderStrokeAsState remembers the target without animating
                // while the field is disabled.
                _indicatorColorAnimation?.Dispose();
                _indicatorColorAnimation = null;
                ApplyIndicatorColour(solid.Color);
                return;
            }

            if (_indicatorColorAnimation is null)
            {
                _indicatorColorAnimation = new MaterialColorAnimation(_box, _indicatorColor,
                    ApplyIndicatorColour, MaterialSpring.EffectsFast);
            }
            _indicatorColorAnimation.Retarget(solid.Color);
        }

        private void ApplyIndicatorColour(Color color)
        {
            _indicatorColor = color;
            _indicatorBrush.Color = color;
            if (_outline is not null)
                _outline.BorderBrush = _indicatorBrush;
        }

        private void ReadLabelType()
        {
            _largeFontSize = ReadResource("MaterialBodyLargeFontSize", _largeFontSize);
            _largeLineHeight = ReadResource("MaterialBodyLargeLineHeight", _largeLineHeight);
            _largeLetterSpacing = ReadResource("MaterialBodyLargeLetterSpacing", _largeLetterSpacing);
            _smallFontSize = ReadResource("MaterialBodySmallFontSize", _smallFontSize);
            _smallLineHeight = ReadResource("MaterialBodySmallLineHeight", _smallLineHeight);
            _smallLetterSpacing = ReadResource("MaterialBodySmallLetterSpacing", _smallLetterSpacing);
        }

        private double ReadResource(string key, double fallback) =>
            _box.TryFindResource(key, out var value) && value is double number ? number : fallback;

        private void DisposeDecoration()
        {
            _labelSpring?.Dispose();
            _labelSpring = null;
            _placeholderSpring?.Dispose();
            _placeholderSpring = null;
            _affixSpring?.Dispose();
            _affixSpring = null;
            _thicknessSpring?.Dispose();
            _thicknessSpring = null;
            _labelColorAnimation?.Dispose();
            _labelColorAnimation = null;
            _indicatorColorAnimation?.Dispose();
            _indicatorColorAnimation = null;
        }

        private void SetPresenter(TextPresenter? presenter)
        {
            if (_presenter == presenter)
                return;
            if (_presenter is not null)
                _presenter.PropertyChanged -= OnPresenterChanged;
            _presenter = presenter;
            if (_presenter is not null)
                _presenter.PropertyChanged += OnPresenterChanged;
            UpdateLabel();
        }

        private void OnPresenterChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == TextPresenter.PreeditTextProperty)
                UpdateLabel();
            else if (args.Property == TextPresenter.TextProperty)
            {
                // TextBox synchronizes the presenter with the real text after an edit. In
                // RevealLastTyped the presenter must show the masked display text instead; the
                // native editing state (TextBox.Text, caret, selection, undo) is untouched.
                var display = GetDisplayText(_box);
                if (!string.Equals(args.NewValue as string, display, StringComparison.Ordinal))
                    _presenter?.SetCurrentValue(TextPresenter.TextProperty, display);
            }
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            var property = args.Property;
            if (property == TextBox.PaddingProperty || property == PrefixProperty || property == SuffixProperty ||
                property == HasLabelProperty || property == IsLabelFloatingProperty ||
                property == LabelPositionProperty ||
                property == ShowPasswordRevealProperty || property == ShowClearButtonProperty ||
                property == TextBox.PasswordCharProperty || property == TextBox.IsReadOnlyProperty ||
                property == TextBox.AcceptsReturnProperty ||
                property == TrailingContentProperty || property == TextBox.InnerRightContentProperty)
            {
                UpdateMargins();
                if (property == TextBox.PaddingProperty)
                    ApplyLabelProgress();
            }

            if (property == LabelProperty || property == TextBox.PlaceholderTextProperty ||
                property == TextBox.UseFloatingPlaceholderProperty || property == TextBox.TextProperty ||
                property == TextBox.AcceptsReturnProperty || property == LabelPositionProperty ||
                property == VariantProperty ||
                property == IsLabelAlwaysMinimizedProperty ||
                property == InputElement.IsKeyboardFocusWithinProperty)
                UpdateLabel();

            if (property == Visual.FlowDirectionProperty || property == MinimizedLabelAlignmentProperty ||
                property == ExpandedLabelAlignmentProperty || property == AboveLabelAlignmentProperty)
            {
                UpdateAlignment();
                ApplyLabelProgress();
            }

            if (property == ShowPasswordRevealProperty || property == TextBox.PasswordCharProperty ||
                property == TextBox.IsReadOnlyProperty || property == TextBox.AcceptsReturnProperty ||
                property == TextBox.TextProperty || property == InputElement.IsEffectivelyEnabledProperty)
            {
                UpdateReveal();
                if (property == ShowPasswordRevealProperty && args.NewValue is false)
                    _box.SetCurrentValue(TextBox.RevealPasswordProperty, false);
            }

            if (property == TextObfuscationModeProperty)
                UpdateObfuscation();
            if (property == TextBox.PasswordCharProperty || property == TextBox.RevealPasswordProperty ||
                property == TextBox.TextProperty || property == InputElement.IsEffectivelyEnabledProperty)
                UpdateObfuscation(property == TextBox.TextProperty ? args : null);
            if (property == TextBox.SelectionStartProperty || property == TextBox.SelectionEndProperty)
                QueueSelectionRevealCheck();

            if (property == ShowClearButtonProperty || property == TextBox.TextProperty ||
                property == TextBox.IsReadOnlyProperty || property == InputElement.IsEffectivelyEnabledProperty)
                UpdateClear();

            if (property == InputElement.IsKeyboardFocusWithinProperty)
            {
                UpdateObfuscation();
                if (!_box.IsKeyboardFocusWithin)
                    ConcealPassword();
            }
            if (property == DataValidationErrors.ErrorsProperty || property == SupportingTextProperty)
                UpdateValidation();

            if (property == LabelBrushProperty && _decorationReady)
                UpdateLabelColour(true);
            if (property == TextBox.BorderBrushProperty && _decorationReady)
                UpdateIndicatorColour(_box.IsEffectivelyEnabled);
            if (property == TextBox.BorderThicknessProperty && _decorationReady)
                UpdateBorderThickness(_box.IsEffectivelyEnabled);
            if (property == InputElement.IsEffectivelyEnabledProperty)
                UpdateDecoration(_decorationReady);

            // Variant and label visibility change which part of the outline is cut out.
            if (property == VariantProperty || property == HasLabelProperty ||
                property == IsLabelFloatingProperty || property == IsLabelCutoutProperty)
                UpdateCutout();
        }

        private void OnKeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape && GetIsClearAvailable(_box))
            {
                ClearText();
                args.Handled = true;
            }
        }

        private void OnTextInput(object? sender, TextInputEventArgs args)
        {
            if (args.Handled)
                return;
            _pendingTextInput = args.Text;
        }

        private void OnTextInputHandled(object? sender, TextInputEventArgs args) =>
            _pendingTextInput = null;

        private void OnClearClick(object? sender, RoutedEventArgs args)
        {
            ClearText();
            _box.Focus();
            args.Handled = true;
        }

        private void ClearText()
        {
            // SetCurrentValue keeps a two-way Text binding in sync instead of shadowing it.
            _box.SetCurrentValue(TextBox.TextProperty, string.Empty);
            _box.CaretIndex = 0;
            _box.SelectionStart = 0;
            _box.SelectionEnd = 0;
        }

        private void UpdateLabel()
        {
            var label = GetLabel(_box);
            if (string.IsNullOrEmpty(label) && _box.UseFloatingPlaceholder)
                label = _box.PlaceholderText;
            var hasLabel = !string.IsNullOrEmpty(label);
            var position = GetLabelPosition(_box);
            var above = position == MaterialTextFieldLabelPosition.Above;
            // TextFieldLabelPosition.Above is always minimized; Inside/Cutout support
            // isAlwaysMinimized, which suppresses the expanded state for an empty unfocused field.
            var alwaysMinimized = !above && GetIsLabelAlwaysMinimized(_box);
            _box.SetCurrentValue(EffectiveLabelProperty, label);
            _box.SetCurrentValue(HasLabelProperty, hasLabel);
            _box.SetCurrentValue(IsLabelAboveProperty, above);
            _box.SetCurrentValue(HasInlineLabelProperty, hasLabel && !above);
            // Only the outlined field cuts the border, and only for its Cutout position
            // (Auto or explicit Cutout). An explicit Inside label stays inside the container.
            _box.SetCurrentValue(IsLabelCutoutProperty, hasLabel && !above &&
                GetVariant(_box) == MaterialTextFieldVariant.Outlined &&
                position != MaterialTextFieldLabelPosition.Inside);
            // Source InputPhase: Focused -> minimized; UnfocusedNotEmpty -> minimized;
            // UnfocusedEmpty -> expanded unless labelPosition.showExpandedLabel is false
            // (Above or isAlwaysMinimized). Multiline does not change this rule.
            _box.SetCurrentValue(IsLabelFloatingProperty, hasLabel &&
                (above || alwaysMinimized || _box.IsKeyboardFocusWithin ||
                 !string.IsNullOrEmpty(_box.Text) || !string.IsNullOrEmpty(_presenter?.PreeditText)));
            _box.SetCurrentValue(IsPlaceholderAllowedProperty, !hasLabel ||
                (!string.IsNullOrEmpty(GetLabel(_box)) &&
                 (above || alwaysMinimized || _box.IsKeyboardFocusWithin)));
            // UseFloatingPlaceholder promotes the placeholder text to the label. That label
            // must not also paint inside the empty input slot in the same state.
            _box.SetCurrentValue(IsPlaceholderVisibleProperty,
                !hasLabel || !string.IsNullOrEmpty(GetLabel(_box)));
            UpdateAlignment();
            UpdateDecoration(_decorationReady);
        }

        // Source TextFieldLabelPosition.Alignment: minimized/expanded for Inside and Cutout,
        // a single alignment for Above. Alignment.Start/End are flow-relative, so the resolved
        // value flips with FlowDirection and the template only binds the horizontal result.
        private void UpdateAlignment()
        {
            var rtl = _box.FlowDirection == FlowDirection.RightToLeft;
            var inline = GetIsLabelFloating(_box)
                ? GetMinimizedLabelAlignment(_box)
                : GetExpandedLabelAlignment(_box);
            _box.SetCurrentValue(EffectiveLabelAlignmentProperty, Resolve(inline, rtl));
            _box.SetCurrentValue(EffectiveAboveLabelAlignmentProperty,
                Resolve(GetAboveLabelAlignment(_box), rtl));
        }

        private static HorizontalAlignment Resolve(MaterialTextFieldLabelAlignment alignment, bool rtl) =>
            alignment switch
            {
                MaterialTextFieldLabelAlignment.Center => HorizontalAlignment.Center,
                MaterialTextFieldLabelAlignment.End => rtl ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                _ => rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };

        private TextObfuscationMode ResolveObfuscationMode() => GetTextObfuscationMode(_box) switch
        {
            // Desktop Avalonia has no platform password-visibility setting, and the source
            // documents Hidden as the desktop convention.
            TextObfuscationMode.System => TextObfuscationMode.Hidden,
            var mode => mode,
        };

        /// <summary>
        /// Keeps the source reveal model: a single typed-character insertion starts (or restarts)
        /// a 1.5s timer, any other text change or an expanded selection hides it.
        /// </summary>
        private void UpdateObfuscation(AvaloniaPropertyChangedEventArgs? textChange = null)
        {
            UpdatePasswordSemantics();
            var mode = ResolveObfuscationMode();
            var canReveal = mode == TextObfuscationMode.RevealLastTyped &&
                _box.IsEffectivelyEnabled && _box.IsKeyboardFocusWithin;
            if (textChange is not null)
            {
                if (canReveal &&
                    TryAcceptTypedInsertion(textChange, out var start, out var length))
                {
                    _revealStart = start;
                    _revealLength = length;
                    StartRevealTimer();
                }
                else
                {
                    // Always consume the pending input, even when this change cannot reveal,
                    // so a later programmatic change cannot inherit an earlier keystroke.
                    _pendingTextInput = null;
                    ClearReveal();
                }
            }
            else if (!canReveal)
            {
                ClearReveal();
            }
            ApplyObfuscation();
        }

        private void UpdatePasswordSemantics()
        {
            if (_updatingPasswordChar)
                return;
            _updatingPasswordChar = true;
            try
            {
                var mode = GetTextObfuscationMode(_box);
                var secure = mode is TextObfuscationMode.Hidden or TextObfuscationMode.RevealLastTyped;
                if (!secure && _passwordCharOverride is not null)
                {
                    var old = _passwordCharOverride;
                    _passwordCharOverride = null;
                    old.Dispose();
                }
                else if (secure && _passwordCharOverride is null && _box.PasswordChar == '\0')
                {
                    // The temporary priority value leaves the original value or binding intact.
                    _passwordCharOverride = _box.SetValue(TextBox.PasswordCharProperty,
                        DefaultObfuscationCharacter, BindingPriority.Animation);
                }
            }
            finally { _updatingPasswordChar = false; }
        }

        private void ApplyObfuscation()
        {
            var text = _box.Text;
            var mode = ResolveObfuscationMode();
            var display = text;
            var effectivePasswordChar = _box.PasswordChar;
            switch (mode)
            {
                case TextObfuscationMode.Visible:
                    effectivePasswordChar = '\0';
                    break;
                case TextObfuscationMode.Hidden:
                    // System keeps ordinary fields visible; explicit Hidden is secured above.
                    break;
                case TextObfuscationMode.RevealLastTyped:
                    effectivePasswordChar = '\0';
                    if (!_box.RevealPassword)
                        display = MaskText(text ?? string.Empty,
                            _box.PasswordChar != '\0' ? _box.PasswordChar : DefaultObfuscationCharacter);
                    break;
            }
            _box.SetCurrentValue(DisplayTextProperty, display);
            _box.SetCurrentValue(EffectivePasswordCharProperty, effectivePasswordChar);
        }

        private string MaskText(string text, char mask)
        {
            if (text.Length == 0)
                return text;
            var characters = new char[text.Length];
            Array.Fill(characters, mask);
            var start = Math.Clamp(_revealStart, 0, text.Length);
            var length = Math.Clamp(_revealLength, 0, text.Length - start);
            if (length > 0)
                text.CopyTo(start, characters, start, length);
            return new string(characters);
        }

        /// <summary>
        /// Accepts a reveal only when the Text change is the exact text the native input pipeline
        /// delivered. A programmatic assignment, paste or command changes Text without that
        /// pending input, and a multi-character input never matches a one-character insertion.
        /// </summary>
        private bool TryAcceptTypedInsertion(AvaloniaPropertyChangedEventArgs change,
            out int start, out int length)
        {
            start = 0;
            length = 0;
            var input = _pendingTextInput;
            _pendingTextInput = null;
            if (input is null || change.NewValue is not string newText ||
                !TryFindSingleInsertion(change.OldValue as string, newText, out start, out length) ||
                input.Length != length)
                return false;
            return string.CompareOrdinal(newText, start, input, 0, length) == 0;
        }

        /// <summary>
        /// Source PasswordInputTransformation accepts exactly one inserted character and ignores
        /// expanded selections. A surrogate pair counts as the single inserted glyph.
        /// </summary>
        private static bool TryFindSingleInsertion(string? oldText, string? newText,
            out int start, out int length)
        {
            start = 0;
            length = 0;
            oldText ??= string.Empty;
            newText ??= string.Empty;
            var shared = Math.Min(oldText.Length, newText.Length);
            var prefix = 0;
            while (prefix < shared && oldText[prefix] == newText[prefix])
                prefix++;
            var suffix = 0;
            while (suffix < shared - prefix &&
                oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
                suffix++;
            var inserted = newText.Length - prefix - suffix;
            if (inserted == 2 && char.IsHighSurrogate(newText[prefix]) &&
                char.IsLowSurrogate(newText[prefix + 1]))
            {
                // A single emoji/glyph is two UTF-16 chars; the source's findFollowingBreak
                // treats it as one insertion.
                start = prefix;
                length = 2;
                return true;
            }
            if (inserted != 1)
                return false;
            start = prefix;
            length = 1;
            if (start + 1 < newText.Length && char.IsHighSurrogate(newText[start]) &&
                char.IsLowSurrogate(newText[start + 1]))
                length = 2;
            else if (start > 0 && char.IsLowSurrogate(newText[start]) &&
                char.IsHighSurrogate(newText[start - 1]))
            {
                start--;
                length = 2;
            }
            return true;
        }

        private void QueueSelectionRevealCheck()
        {
            if (_disposed || _revealStart < 0 || _selectionRevealCheckQueued)
                return;
            // TextBox collapses a moved caret by assigning SelectionStart and SelectionEnd in
            // sequence, so the first assignment can briefly observe an expanded range even
            // though the native edit leaves a collapsed caret. Wait for both assignments to
            // settle; an intentional selection still hides the reveal before the next render.
            _selectionRevealCheckQueued = true;
            Dispatcher.UIThread.Post(CheckSelectionReveal, DispatcherPriority.Input);
        }

        private void CheckSelectionReveal()
        {
            _selectionRevealCheckQueued = false;
            if (_disposed || _revealStart < 0)
                return;
            // Source PasswordInputTransformation hides the reveal as soon as the buffer has an
            // expanded selection.
            if (_box.SelectionStart != _box.SelectionEnd)
            {
                ClearReveal();
                ApplyObfuscation();
            }
        }

        private void StartRevealTimer()
        {
            if (_revealTimer is null)
            {
                _revealTimer = new DispatcherTimer { Interval = RevealDuration };
                _revealTimer.Tick += OnRevealTimerTick;
            }
            // Source observeHideEvents uses collectLatest, so another insertion restarts the
            // full duration instead of shortening it.
            _revealTimer.Stop();
            _revealTimer.Start();
        }

        private void OnRevealTimerTick(object? sender, EventArgs args)
        {
            ClearReveal();
            ApplyObfuscation();
        }

        private void ClearReveal()
        {
            _revealTimer?.Stop();
            _revealStart = -1;
            _revealLength = 0;
        }

        private void UpdateReveal()
        {
            var available = GetShowPasswordReveal(_box) && _box.PasswordChar != '\0' &&
                !_box.IsReadOnly && !_box.AcceptsReturn && _box.IsEffectivelyEnabled &&
                !string.IsNullOrEmpty(_box.Text);
            _box.SetCurrentValue(IsRevealAvailableProperty, available);
            if (!available)
                ConcealPassword();
        }

        private void UpdateClear()
        {
            var available = GetShowClearButton(_box) && !_box.IsReadOnly && _box.IsEffectivelyEnabled &&
                !string.IsNullOrEmpty(_box.Text);
            _box.SetCurrentValue(IsClearAvailableProperty, available);
        }

        private void ConcealPassword()
        {
            // Opt-in UI policy does not interfere with applications that manage RevealPassword themselves.
            if (GetShowPasswordReveal(_box))
                _box.SetCurrentValue(TextBox.RevealPasswordProperty, false);
        }

        private void UpdateValidation()
        {
            var builder = new StringBuilder();
            if (DataValidationErrors.GetErrors(_box) is { } errors)
            {
                foreach (var error in errors)
                {
                    var message = error is Exception exception ? exception.Message : error?.ToString();
                    if (string.IsNullOrEmpty(message))
                        continue;
                    if (builder.Length > 0)
                        builder.AppendLine();
                    builder.Append(message);
                }
            }
            var validation = builder.Length == 0 ? null : builder.ToString();
            var support = GetSupportingText(_box);
            _box.SetCurrentValue(ValidationMessageProperty, validation);
            // Material shows the error in the supporting-text slot instead of stacking two messages.
            _box.SetCurrentValue(EffectiveSupportingTextProperty,
                string.IsNullOrEmpty(validation) ? support : validation);
            _box.SetCurrentValue(AccessibleDescriptionProperty,
                string.IsNullOrEmpty(validation) ? support :
                string.IsNullOrEmpty(support) ? validation : support + Environment.NewLine + validation);
        }

        public void Dispose()
        {
            _disposed = true;
            _box.PropertyChanged -= OnPropertyChanged;
            _passwordCharOverride?.Dispose();
            _passwordCharOverride = null;
            _box.TemplateApplied -= OnTemplateApplied;
            _box.DetachedFromVisualTree -= OnDetached;
            _box.KeyDown -= OnKeyDown;
            _box.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
            _box.RemoveHandler(InputElement.TextInputEvent, OnTextInputHandled);
            _pendingTextInput = null;
            _revealTimer?.Stop();
            if (_revealTimer is not null)
                _revealTimer.Tick -= OnRevealTimerTick;
            _revealTimer = null;
            DetachParts();
            SetPresenter(null);
            ConcealPassword();
        }

        // Source TextFieldImpl InputPhase.
        private enum MaterialTextFieldInputPhase
        {
            Focused,
            UnfocusedEmpty,
            UnfocusedNotEmpty,
        }
    }
}
