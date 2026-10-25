using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ExpressiveAvalonia.Theming;

/// <summary>The Material text field container treatment.</summary>
public enum MaterialTextFieldVariant
{
    Filled,
    Outlined,
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
/// trailing actions. Enabled, hovered, focused, error and disabled states come from the native
/// control's own pseudo-classes.
/// </remarks>
public sealed class MaterialTextField : AvaloniaObject
{
    public static readonly AttachedProperty<MaterialTextFieldVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, MaterialTextFieldVariant>("Variant");
    public static readonly AttachedProperty<string?> LabelProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, string?>("Label");
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
    /// <summary>Shows a trailing clear action while the field has text. Escape also clears.</summary>
    public static readonly AttachedProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("ShowClearButton");

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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsLabelFloatingProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsLabelFloating");
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> IsPlaceholderAllowedProperty =
        AvaloniaProperty.RegisterAttached<MaterialTextField, TextBox, bool>("IsPlaceholderAllowed", true);
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
    public static bool GetShowClearButton(TextBox box) => box.GetValue(ShowClearButtonProperty);
    public static void SetShowClearButton(TextBox box, bool value) => box.SetValue(ShowClearButtonProperty, value);
    public static bool GetIsEnabled(TextBox box) => box.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(TextBox box, bool value) => box.SetValue(IsEnabledProperty, value);
    public static string? GetEffectiveLabel(TextBox box) => box.GetValue(EffectiveLabelProperty);
    public static bool GetHasLabel(TextBox box) => box.GetValue(HasLabelProperty);
    public static bool GetIsLabelFloating(TextBox box) => box.GetValue(IsLabelFloatingProperty);
    public static bool GetIsPlaceholderAllowed(TextBox box) => box.GetValue(IsPlaceholderAllowedProperty);
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
        private Button? _clear;
        private Rect? _cutout;
        private Size _outlineSize;

        public FieldState(TextBox box)
        {
            _box = box;
            box.PropertyChanged += OnPropertyChanged;
            box.TemplateApplied += OnTemplateApplied;
            box.DetachedFromVisualTree += OnDetached;
            box.KeyDown += OnKeyDown;
            UpdateLabel();
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
            _clear = args.NameScope.Find<Button>("ClearButton");
            _cutout = null;
            if (_outline is not null)
                _outline.PropertyChanged += OnPartChanged;
            if (_label is not null)
                _label.PropertyChanged += OnPartChanged;
            if (_clear is not null)
                _clear.Click += OnClearClick;
            SetPresenter(args.NameScope.Find<TextPresenter>("PART_TextPresenter"));
            UpdateCutout();
        }

        // The cutout depends on the outline size and on the label's position, both of which
        // arrive as Bounds changes. Subscribing to LayoutUpdated would recompute on every
        // layout pass of the whole window.
        private void OnPartChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Visual.BoundsProperty)
                UpdateCutout();
        }

        private void DetachParts()
        {
            if (_outline is not null)
            {
                _outline.PropertyChanged -= OnPartChanged;
                _outline.Clip = null;
            }
            if (_label is not null)
                _label.PropertyChanged -= OnPartChanged;
            if (_clear is not null)
                _clear.Click -= OnClearClick;
            _outline = null;
            _label = null;
            _clear = null;
        }

        private void UpdateCutout()
        {
            if (_outline is null)
                return;
            if (GetVariant(_box) != MaterialTextFieldVariant.Outlined ||
                !GetIsLabelFloating(_box) || _label is null || !_label.IsVisible)
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
            var showAffixes = !GetHasLabel(_box) || GetIsLabelFloating(_box);
            var hasPrefix = showAffixes && !string.IsNullOrEmpty(GetPrefix(_box));
            var hasSuffix = showAffixes && !string.IsNullOrEmpty(GetSuffix(_box));
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
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            var property = args.Property;
            if (property == TextBox.PaddingProperty || property == PrefixProperty || property == SuffixProperty ||
                property == HasLabelProperty || property == IsLabelFloatingProperty ||
                property == ShowPasswordRevealProperty || property == ShowClearButtonProperty ||
                property == TextBox.PasswordCharProperty || property == TextBox.IsReadOnlyProperty ||
                property == TextBox.AcceptsReturnProperty ||
                property == TrailingContentProperty || property == TextBox.InnerRightContentProperty)
                UpdateMargins();

            if (property == LabelProperty || property == TextBox.PlaceholderTextProperty ||
                property == TextBox.UseFloatingPlaceholderProperty || property == TextBox.TextProperty ||
                property == TextBox.AcceptsReturnProperty ||
                property == InputElement.IsKeyboardFocusWithinProperty)
                UpdateLabel();

            if (property == ShowPasswordRevealProperty || property == TextBox.PasswordCharProperty ||
                property == TextBox.IsReadOnlyProperty || property == TextBox.AcceptsReturnProperty ||
                property == TextBox.TextProperty || property == InputElement.IsEffectivelyEnabledProperty)
            {
                UpdateReveal();
                if (property == ShowPasswordRevealProperty && args.NewValue is false)
                    _box.SetCurrentValue(TextBox.RevealPasswordProperty, false);
            }

            if (property == ShowClearButtonProperty || property == TextBox.TextProperty ||
                property == TextBox.IsReadOnlyProperty || property == InputElement.IsEffectivelyEnabledProperty)
                UpdateClear();

            if (property == InputElement.IsKeyboardFocusWithinProperty && !_box.IsKeyboardFocusWithin)
                ConcealPassword();
            if (property == DataValidationErrors.ErrorsProperty || property == SupportingTextProperty)
                UpdateValidation();

            // Variant and label visibility change which part of the outline is cut out.
            if (property == VariantProperty || property == HasLabelProperty || property == IsLabelFloatingProperty)
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
            _box.SetCurrentValue(EffectiveLabelProperty, label);
            _box.SetCurrentValue(HasLabelProperty, hasLabel);
            _box.SetCurrentValue(IsLabelFloatingProperty, hasLabel &&
                (_box.AcceptsReturn || _box.IsKeyboardFocusWithin || !string.IsNullOrEmpty(_box.Text) ||
                 !string.IsNullOrEmpty(_presenter?.PreeditText)));
            _box.SetCurrentValue(IsPlaceholderAllowedProperty, !hasLabel ||
                (!string.IsNullOrEmpty(GetLabel(_box)) && _box.IsKeyboardFocusWithin));
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
            _box.PropertyChanged -= OnPropertyChanged;
            _box.TemplateApplied -= OnTemplateApplied;
            _box.DetachedFromVisualTree -= OnDetached;
            _box.KeyDown -= OnKeyDown;
            DetachParts();
            SetPresenter(null);
            ConcealPassword();
        }
    }
}
