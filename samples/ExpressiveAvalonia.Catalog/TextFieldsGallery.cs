using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;
// System.IO is an implicit using here, so the shape type needs an explicit alias.
using Path = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Gallery for the text field source family: Filled/Outlined styles, every state, the
/// label/placeholder/input-text configurations, leading/trailing icons, prefix/suffix,
/// password reveal, the clear action and the two "Menu with Text field" compositions.
/// </summary>
public static class TextFieldsGallery
{
    private const string SearchGeometry =
        "M39.6 45L33.3 38.7C32.8 39.1 32.225 39.4167 31.575 39.65C30.925 39.8833 30.2333 40 29.5 40" +
        "C27.6833 40 26.1458 39.3708 24.8875 38.1125C23.6292 36.8542 23 35.3167 23 33.5" +
        "C23 31.6833 23.6292 30.1458 24.8875 28.8875C26.1458 27.6292 27.6833 27 29.5 27" +
        "C31.3167 27 32.8542 27.6292 34.1125 28.8875C35.3708 30.1458 36 31.6833 36 33.5" +
        "C36 34.2333 35.8833 34.925 35.65 35.575C35.4167 36.225 35.1 36.8 34.7 37.3L41 43.6L39.6 45Z" +
        "M29.5 38C30.75 38 31.8125 37.5625 32.6875 36.6875C33.5625 35.8125 34 34.75 34 33.5" +
        "C34 32.25 33.5625 31.1875 32.6875 30.3125C31.8125 29.4375 30.75 29 29.5 29" +
        "C28.25 29 27.1875 29.4375 26.3125 30.3125C25.4375 31.1875 25 32.25 25 33.5" +
        "C25 34.75 25.4375 35.8125 26.3125 36.6875C27.1875 37.5625 28.25 38 29.5 38Z";
    private const string StarGeometry =
        "M12,3 L14.8,8.7 L21,9.6 L16.5,14 L17.6,20.2 L12,17.3 L6.4,20.2 L7.5,14 L3,9.6 L9.2,8.7 Z";

    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Text fields",
            FontSize = 24,
            LineHeight = 32,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Filled and Outlined containers, enabled / hovered / focused / error / disabled, "
                 + "label, placeholder and input-text configurations, leading and trailing content.",
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(Section("Filled", "Label, input text, placeholder and supporting text",
            Row(
                Field(MaterialTextFieldVariant.Filled, "Label", null, "Supporting text"),
                Field(MaterialTextFieldVariant.Filled, "Label", "Input", "Supporting text"),
                Error(Field(MaterialTextFieldVariant.Filled, "Label", "Input", "Supporting text")),
                Disabled(Field(MaterialTextFieldVariant.Filled, "Label", "Input", "Supporting text")))));

        panel.Children.Add(Section("Outlined", "Same states with the notched outline",
            Row(
                Field(MaterialTextFieldVariant.Outlined, "Label", null, "Supporting text"),
                Field(MaterialTextFieldVariant.Outlined, "Label", "Input", "Supporting text"),
                Error(Field(MaterialTextFieldVariant.Outlined, "Label", "Input", "Supporting text")),
                Disabled(Field(MaterialTextFieldVariant.Outlined, "Label", "Input", "Supporting text")))));

        panel.Children.Add(Section("Text configurations",
            "Label text keeps the label resting, Input text floats it above the value, Placeholder text "
            + "shows the placeholder at rest and Label with placeholder floats the label on focus",
            Row(
                Field(MaterialTextFieldVariant.Outlined, "Label", null, null),
                Field(MaterialTextFieldVariant.Outlined, "Label", "Input", null),
                PlaceholderOnly(MaterialTextFieldVariant.Outlined),
                LabelWithPlaceholder(MaterialTextFieldVariant.Filled))));

        panel.Children.Add(Section("Anatomy",
            "Leading and trailing icons, prefix and suffix, password reveal, clear action and multiline",
            Row(
                WithIcons(MaterialTextFieldVariant.Filled),
                WithIcons(MaterialTextFieldVariant.Outlined),
                Affixed(MaterialTextFieldVariant.Outlined),
                Password(),
                Clearable())));

        panel.Children.Add(Section("Focus and hover",
            "Click or Tab into either field to see the 3dp focus indicator, the floating label and the "
            + "hover outline; reduced motion collapses both transitions",
            Row(
                Focused(Field(MaterialTextFieldVariant.Outlined, "Label", "Input", "Focused by keyboard or pointer")),
                Field(MaterialTextFieldVariant.Filled, "Label", "Input", "Hover to see the indicator color"))));

        panel.Children.Add(Section("Multiline",
            "AcceptsReturn keeps the label floated and grows the field",
            Row(
                Multiline(MaterialTextFieldVariant.Filled),
                Multiline(MaterialTextFieldVariant.Outlined))));

        panel.Children.Add(Section("Menu with Text field - Example 1",
            "Outlined field without a leading icon; the menu filters while typing (Down/Up, Enter, Escape, click)",
            MenuExample(showSearchIcon: false)));

        panel.Children.Add(Section("Menu with Text field - Example 2",
            "Outlined field with the leading search icon and trailing clear action",
            MenuExample(showSearchIcon: true)));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control Section(string title, string caption, params Control[] content)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(new TextBlock { Text = title, FontSize = 20, LineHeight = 28 });
        if (!string.IsNullOrEmpty(caption))
            section.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        foreach (var control in content)
            section.Children.Add(control);
        return section;
    }

    private static Control Row(params Control[] controls)
    {
        var wrap = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
        foreach (var control in controls)
            wrap.Children.Add(control);
        return wrap;
    }

    private static TextBox Field(MaterialTextFieldVariant variant, string? label, string? text, string? supporting)
    {
        var box = new TextBox
        {
            Width = 240,
            Text = text ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        MaterialTextField.SetVariant(box, variant);
        MaterialTextField.SetLabel(box, label);
        MaterialTextField.SetSupportingText(box, supporting);
        return box;
    }

    private static Control Focused(TextBox box)
    {
        box.Loaded += (_, _) => box.Focus();
        return box;
    }

    private static Control Error(TextBox box)
    {
        DataValidationErrors.SetErrors(box, new object[] { "Enter a valid email address" });
        return box;
    }

    private static Control Disabled(TextBox box)
    {
        box.IsEnabled = false;
        return box;
    }

    private static Control PlaceholderOnly(MaterialTextFieldVariant variant)
    {
        var box = Field(variant, null, null, "Placeholder shown at rest");
        box.PlaceholderText = "Placeholder";
        return box;
    }

    private static Control LabelWithPlaceholder(MaterialTextFieldVariant variant)
    {
        var box = Field(variant, "Label", null, "Focus to float the label over the placeholder");
        box.PlaceholderText = "Placeholder";
        return box;
    }

    private static Control WithIcons(MaterialTextFieldVariant variant)
    {
        var box = Field(variant, "Label", "Input", "Supporting text");
        MaterialTextField.SetLeadingContent(box, Icon(SearchGeometry));
        MaterialTextField.SetTrailingContent(box, Icon(StarGeometry));
        return box;
    }

    private static Control Affixed(MaterialTextFieldVariant variant)
    {
        var box = Field(variant, "Label", "user", "Prefix and suffix");
        MaterialTextField.SetPrefix(box, "@");
        MaterialTextField.SetSuffix(box, ".com");
        return box;
    }

    private static Control Password()
    {
        var box = Field(MaterialTextFieldVariant.Outlined, "Password", "secret", "Reveal keeps native masking");
        box.PasswordChar = '\u2022';
        MaterialTextField.SetShowPasswordReveal(box, true);
        return box;
    }

    private static Control Clearable()
    {
        var box = Field(MaterialTextFieldVariant.Filled, "Label", "Clear me", "Clear action and Escape");
        MaterialTextField.SetShowClearButton(box, true);
        return box;
    }

    private static Control Multiline(MaterialTextFieldVariant variant)
    {
        var box = Field(variant, "Label", "First line\nSecond line", "Multiline supporting text");
        box.AcceptsReturn = true;
        box.Height = 112;
        box.TextWrapping = TextWrapping.Wrap;
        return box;
    }

    private static Control MenuExample(bool showSearchIcon)
    {
        var status = new TextBlock { Text = "No item invoked yet", Opacity = 0.75 };
        var menu = new MaterialMenuTextField
        {
            Width = 240,
            Label = "Label",
            PlaceholderText = "Input",
            SupportingText = "Supporting text",
            ShowSearchIcon = showSearchIcon,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        menu.Items.Add(new MaterialMenuTextFieldItem("Menu item", "Supporting text", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Inbox", "12 unread", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Drafts", "3 drafts", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Sent", "Yesterday", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Archive", "Keep for later", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Trash", "Deleted items", Icon(StarGeometry)));
        menu.Items.Add(new MaterialMenuTextFieldItem("Unavailable", "Disabled row", Icon(StarGeometry)) { IsEnabled = false });
        menu.Items.Add(new MaterialMenuTextFieldItem("Settings", "Preferences", Icon(StarGeometry)));
        menu.ItemInvoked += (_, e) =>
            status.Text = $"Invoked: {e.Item.Label}{(e.Item.Tag is null ? string.Empty : " · " + e.Item.Tag)}";
        var stack = new StackPanel { Spacing = 8, Width = 240 };
        stack.Children.Add(menu);
        stack.Children.Add(status);
        return stack;
    }

    private static Path Icon(string data)
    {
        var icon = new Path
        {
            Data = Geometry.Parse(data),
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
        };
        icon.Bind(Shape.FillProperty, icon.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        return icon;
    }
}
