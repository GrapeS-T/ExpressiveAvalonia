using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Catalog page for the plain tooltip lifecycle (<see cref="MaterialToolTip"/>) and
/// <see cref="RichTooltip"/>.
/// </summary>
/// <remarks>
/// The plain section covers both source variants: a single-line tooltip that hugs its text and a
/// multi-line tooltip that wraps at 200 DIP. Hover, keyboard focus (Tab), Escape, the disabled
/// case and the 500 ms touch long press all use the real Avalonia tooltip service. The rich
/// section covers the source properties: subhead text and visibility, supporting text, actions,
/// and the secondary button.
/// </remarks>
public static class TooltipsGallery
{
    private const string MultiLine =
        "A plain tooltip wraps at 200 DIP and grows to two or more lines, while a short tip hugs its text.";

    public static Control Create()
    {
        var status = new TextBlock
        {
            Text = "No tooltip action has run yet.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        };
        void Report(string message) => status.Text = message;

        var page = new StackPanel { Spacing = 20, Margin = new Thickness(24) };
        page.Children.Add(new TextBlock { Text = "Tooltips", FontSize = 28, FontWeight = FontWeight.SemiBold });
        page.Children.Add(new TextBlock
        {
            Text = "Plain tooltips open on hover, on keyboard focus and after a touch long press. " +
                   "Press Escape or move focus away to dismiss them. Rich tooltips open from a button, " +
                   "the context key or a long press, take focus, and return it to the owner on close.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        });

        page.Children.Add(Heading("Plain tooltip · single line"));
        page.Children.Add(Row(
            WithTip(new Button { Content = "Save" }, "Save changes"),
            WithTip(new Button { Content = "Share" }, "Share the document"),
            WithTip(new TextBox { Width = 220, PlaceholderText = "Search" }, "Search the library")));

        page.Children.Add(Heading("Plain tooltip · multi line"));
        var longPress = WithTip(new Button { Content = "Touch long press" }, "Opened after 500 ms of touch press");
        MaterialToolTip.SetLongPressEnabled(longPress, true);
        var multiLine = new Button { Content = "Why?" };
        ToolTip.SetTip(multiLine, MaterialToolTip.CreateContent(MultiLine, MaterialPlainToolTipType.MultiLine));
        page.Children.Add(Row(
            multiLine,
            WithTip(new Button { Content = "Long text as a string" }, MultiLine),
            longPress,
            Hint("The factory fixes the 200 DIP multi-line frame; a plain string tip wraps on its own.")));

        page.Children.Add(Heading("Plain tooltip · disabled owner"));
        var disabled = new Button { Content = "Disabled", IsEnabled = false };
        ToolTip.SetTip(disabled, "Explains why this action is unavailable");
        ToolTip.SetShowOnDisabled(disabled, true);
        page.Children.Add(Row(disabled, new TextBlock
        {
            Text = "ShowOnDisabled keeps the explanation available; the help text is exposed to screen readers.",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8,
        }));

        page.Children.Add(Heading("Rich tooltip · subhead, supporting text and two actions"));
        var full = new RichTooltip
        {
            Title = "Publish changes",
            Content = "Publishing makes this revision visible to everyone with access to the library.",
            PrimaryActionText = "Publish",
            SecondaryActionText = "Cancel",
        };
        full.PrimaryActionCommand = new DelegateCommand(() => Report("Publish ran and closed the tooltip."));
        full.SecondaryActionCommand = new DelegateCommand(() => Report("Cancel ran and closed the tooltip."));
        var publish = new Button { Content = "Publish…", Flyout = full };
        var showAt = new Button { Content = "Open with ShowAt" };
        showAt.Click += (_, _) => full.ShowAt(showAt);
        page.Children.Add(Row(
            publish,
            showAt,
            Hint("Actions are text buttons in the primary color; both dismiss after running.")));

        page.Children.Add(Heading("Rich tooltip · subhead only, no actions"));
        var titleOnly = new RichTooltip
        {
            Title = "Keyboard shortcuts",
            Content = "Ctrl+S saves, Ctrl+Shift+P opens the command palette, Ctrl+K inserts a link.",
        };
        page.Children.Add(Row(new Button { Content = "Shortcuts", Flyout = titleOnly }));

        page.Children.Add(Heading("Rich tooltip · supporting text only"));
        var bodyOnly = new RichTooltip
        {
            Content = "A rich tooltip without a subhead and without actions uses 16/12 padding on every side.",
        };
        page.Children.Add(Row(new Button { Content = "Note", Flyout = bodyOnly }));

        page.Children.Add(Heading("Rich tooltip · custom action content"));
        var customActions = new RichTooltip
        {
            Title = "Rate this result",
            Content = "Your feedback tunes the recommendations for this workspace.",
        };
        var stars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (var index = 1; index <= 5; index++)
        {
            var value = index;
            var star = new Button { Content = $"{value}★", MinHeight = 40, Padding = new Thickness(12, 10) };
            MaterialAssist.SetButtonVariant(star, MaterialButtonVariant.Text);
            MaterialAssist.SetContainerInset(star, default);
            AutomationProperties.SetName(star, $"Rate {value} of 5");
            star.Click += (_, _) =>
            {
                Report($"Rated {value} of 5.");
                customActions.Hide();
            };
            stars.Children.Add(star);
        }
        customActions.Actions = stars;
        page.Children.Add(Row(new Button { Content = "Rate…", Flyout = customActions }));

        page.Children.Add(Heading("Rich tooltip · context flyout"));
        var contextOwner = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#14000000")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24, 16),
            Child = new TextBlock { Text = "Right-click, press the context key or long press this surface." },
        };
        contextOwner.ContextFlyout = new RichTooltip
        {
            Title = "Document actions",
            Content = "This rich tooltip is attached as a context flyout, so the context key and touch long press open it.",
            PrimaryActionText = "Rename",
            SecondaryActionText = "Delete",
            PrimaryActionCommand = new DelegateCommand(() => Report("Rename ran from the context flyout.")),
            SecondaryActionCommand = new DelegateCommand(() => Report("Delete ran from the context flyout.")),
        };
        page.Children.Add(contextOwner);

        page.Children.Add(status);
        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.Medium,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.8,
    };

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    private static Button WithTip(Button button, string tip)
    {
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static TextBox WithTip(TextBox textBox, string tip)
    {
        ToolTip.SetTip(textBox, tip);
        return textBox;
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
