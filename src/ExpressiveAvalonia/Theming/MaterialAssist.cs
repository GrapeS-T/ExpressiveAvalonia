using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Theming;

/// <summary>The Material color treatment of a button.</summary>
public enum MaterialButtonVariant { Filled, Tonal, Outlined, Elevated, Text }

/// <summary>The five Material Expressive control sizes.</summary>
public enum MaterialControlSize { ExtraSmall, Small, Medium, Large, ExtraLarge }

/// <summary>The resting silhouette of a Material Expressive button.</summary>
public enum MaterialButtonShape { Round, Square }

/// <summary>Material presentation options for standard Avalonia controls.</summary>
public sealed class MaterialAssist : AvaloniaObject
{
    private MaterialAssist() { }

    public static readonly AttachedProperty<MaterialButtonVariant> ButtonVariantProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialButtonVariant>(
            "ButtonVariant", MaterialButtonVariant.Filled);

    public static readonly AttachedProperty<MaterialControlSize> ControlSizeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialControlSize>(
            "ControlSize", MaterialControlSize.Small);

    public static readonly AttachedProperty<MaterialButtonShape> ButtonShapeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, MaterialButtonShape>(
            "ButtonShape", MaterialButtonShape.Round);

    public static readonly AttachedProperty<bool> IsIconButtonProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Control, bool>("IsIconButton");

    // Reserves the 48-DIP interactive target without enlarging the visual container.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<Thickness> ContainerInsetProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, Thickness>("ContainerInset");

    // These template plumbing properties leave the consumer's CornerRadius untouched.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<bool> AnimateShapeProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, bool>("AnimateShape");

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static readonly AttachedProperty<CornerRadius> AnimatedCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, CornerRadius>("AnimatedCornerRadius");

    private static readonly AttachedProperty<ButtonShapeAnimation?> ShapeAnimationProperty =
        AvaloniaProperty.RegisterAttached<MaterialAssist, Button, ButtonShapeAnimation?>("ShapeAnimation");

    static MaterialAssist()
    {
        AnimateShapeProperty.Changed.AddClassHandler<Button>((button, _) =>
        {
            if (GetAnimateShape(button))
            {
                if (button.GetValue(ShapeAnimationProperty) is null)
                    button.SetValue(ShapeAnimationProperty, new ButtonShapeAnimation(button));
            }
            else
            {
                button.GetValue(ShapeAnimationProperty)?.Dispose();
                button.ClearValue(ShapeAnimationProperty);
                button.ClearValue(AnimatedCornerRadiusProperty);
            }
        });
    }

    public static MaterialButtonVariant GetButtonVariant(Control control) => control.GetValue(ButtonVariantProperty);
    public static void SetButtonVariant(Control control, MaterialButtonVariant value) => control.SetValue(ButtonVariantProperty, value);
    public static MaterialControlSize GetControlSize(Control control) => control.GetValue(ControlSizeProperty);
    public static void SetControlSize(Control control, MaterialControlSize value) => control.SetValue(ControlSizeProperty, value);
    public static MaterialButtonShape GetButtonShape(Control control) => control.GetValue(ButtonShapeProperty);
    public static void SetButtonShape(Control control, MaterialButtonShape value) => control.SetValue(ButtonShapeProperty, value);
    public static bool GetIsIconButton(Control control) => control.GetValue(IsIconButtonProperty);
    public static void SetIsIconButton(Control control, bool value) => control.SetValue(IsIconButtonProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Thickness GetContainerInset(Button button) => button.GetValue(ContainerInsetProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetContainerInset(Button button, Thickness value) => button.SetValue(ContainerInsetProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetAnimateShape(Button button) => button.GetValue(AnimateShapeProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimateShape(Button button, bool value) => button.SetValue(AnimateShapeProperty, value);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CornerRadius GetAnimatedCornerRadius(Button button) => button.GetValue(AnimatedCornerRadiusProperty);
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetAnimatedCornerRadius(Button button, CornerRadius value) => button.SetValue(AnimatedCornerRadiusProperty, value);

    private sealed class ButtonShapeAnimation : IDisposable
    {
        private readonly Button _button;
        private MaterialMotion.SpringAnimation? _spring;
        private double _progress;

        public ButtonShapeAnimation(Button button)
        {
            _button = button;
            button.PropertyChanged += OnPropertyChanged;
            button.AttachedToVisualTree += OnAttached;
            button.DetachedFromVisualTree += OnDetached;
            Update(false);
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args) => Update(false);

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _spring?.Dispose();
            _spring = null;
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Button.IsPressedProperty || args.Property == ToggleButton.IsCheckedProperty)
                Update(true);
            else if (args.Property == MaterialMotion.IsReducedProperty ||
                     args.Property == Button.CornerRadiusProperty || args.Property == Button.BoundsProperty ||
                     args.Property == ControlSizeProperty || args.Property == ButtonShapeProperty ||
                     args.Property == IsIconButtonProperty || args.Property == ContainerInsetProperty)
                Update(false);
        }

        private void Update(bool animate)
        {
            var inset = GetContainerInset(_button);
            var height = Math.Max(0, (_button.Bounds.Height > 0 ? _button.Bounds.Height : _button.MinHeight) - inset.Top - inset.Bottom);
            var width = Math.Max(0, (_button.Bounds.Width > 0 ? _button.Bounds.Width : _button.MinWidth) - inset.Left - inset.Right);
            var limit = Math.Min(width, height) / 2;
            var square = GetControlSize(_button) switch
            {
                MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 12d,
                MaterialControlSize.Medium => 16d,
                _ => 28d,
            };
            var targetRadius = Math.Min(_button.CornerRadius.TopLeft, limit);
            if (_button.IsPressed)
                targetRadius = GetControlSize(_button) switch
                {
                    MaterialControlSize.ExtraSmall or MaterialControlSize.Small => 8d,
                    MaterialControlSize.Medium => 12d,
                    _ => 16d,
                };
            else if (_button is ToggleButton { IsChecked: true })
                targetRadius = GetButtonShape(_button) == MaterialButtonShape.Round ? square : limit;
            var target = Math.Min(targetRadius, limit) - Math.Min(_button.CornerRadius.TopLeft, limit);
            if (!animate || !_button.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(_button))
            {
                _spring?.Dispose();
                _spring = null;
                Apply(target);
            }
            else if (_spring is not null)
            {
                _spring.Retarget(target);
            }
            else
            {
                _spring = MaterialMotion.Animate(_button, _progress, target, Apply);
            }
        }

        private void Apply(double progress)
        {
            _progress = progress;
            var inset = GetContainerInset(_button);
            var height = Math.Max(0, (_button.Bounds.Height > 0 ? _button.Bounds.Height : _button.MinHeight) - inset.Top - inset.Bottom);
            var width = Math.Max(0, (_button.Bounds.Width > 0 ? _button.Bounds.Width : _button.MinWidth) - inset.Left - inset.Right);
            var limit = Math.Min(width, height) / 2;
            var resting = _button.CornerRadius;
            double Corner(double value) => Math.Clamp(Math.Min(value, limit) + progress, 0, limit);
            _button.SetCurrentValue(AnimatedCornerRadiusProperty, new CornerRadius(
                Corner(resting.TopLeft), Corner(resting.TopRight),
                Corner(resting.BottomRight), Corner(resting.BottomLeft)));
        }

        public void Dispose()
        {
            _spring?.Dispose();
            _button.PropertyChanged -= OnPropertyChanged;
            _button.AttachedToVisualTree -= OnAttached;
            _button.DetachedFromVisualTree -= OnDetached;
        }
    }
}
