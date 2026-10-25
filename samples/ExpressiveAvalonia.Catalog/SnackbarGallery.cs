using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Catalog page for <see cref="MaterialSnackbar"/> and <see cref="MaterialSnackbarHost"/>.
/// </summary>
/// <remarks>
/// The buttons reproduce the ten exported source configurations (three action configurations by
/// two line counts by two close-affordance settings, where "longer action" only exists in the
/// two-line layout). Every snackbar is 344 DIP wide, the source component width. Interact with the
/// action and close affordances to exercise the hover, focus and pressed state layers.
/// </remarks>
public static class SnackbarGallery
{
    private const string ShortText = "Snackbar supporting text";
    private const string LongText =
        "This message is deliberately long so it wraps, reaches the two-line limit and is trimmed with an ellipsis";

    private sealed record SnackbarCase(
        string Name,
        string Text,
        string? Action,
        MaterialSnackbarLineCount Lines,
        bool Close,
        MaterialSnackbarActionPlacement Placement);

    private static readonly SnackbarCase[] Cases =
    [
        new("Text only, 1 line", ShortText, null, MaterialSnackbarLineCount.OneLine, false, MaterialSnackbarActionPlacement.Inline),
        new("Text only, 1 line + close", ShortText, null, MaterialSnackbarLineCount.OneLine, true, MaterialSnackbarActionPlacement.Inline),
        new("Text only, 2 lines", ShortText, null, MaterialSnackbarLineCount.TwoLines, false, MaterialSnackbarActionPlacement.Inline),
        new("Text only, 2 lines + close", ShortText, null, MaterialSnackbarLineCount.TwoLines, true, MaterialSnackbarActionPlacement.Inline),
        new("Action, 1 line", ShortText, "Action", MaterialSnackbarLineCount.OneLine, false, MaterialSnackbarActionPlacement.Inline),
        new("Action, 1 line + close", ShortText, "Action", MaterialSnackbarLineCount.OneLine, true, MaterialSnackbarActionPlacement.Inline),
        new("Action, 2 lines", ShortText, "Action", MaterialSnackbarLineCount.TwoLines, false, MaterialSnackbarActionPlacement.Inline),
        new("Action, 2 lines + close", ShortText, "Action", MaterialSnackbarLineCount.TwoLines, true, MaterialSnackbarActionPlacement.Inline),
        new("Longer action, 2 lines", ShortText, "Longer action", MaterialSnackbarLineCount.TwoLines, false, MaterialSnackbarActionPlacement.Below),
        new("Longer action, 2 lines + close", ShortText, "Longer action", MaterialSnackbarLineCount.TwoLines, true, MaterialSnackbarActionPlacement.Below),
    ];

    public static Control Create()
    {
        var status = new TextBlock
        {
            Text = "No snackbar has closed yet.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        };

        var host = new MaterialSnackbarHost { Name = "SnackbarHost" };

        var page = new StackPanel { Spacing = 16, Margin = new Thickness(24) };
        page.Children.Add(new TextBlock
        {
            Text = "Snackbar",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
        });
        page.Children.Add(new TextBlock
        {
            Text = "Material 3 snackbar on the inverse surface. Each button replays one exported source " +
                   "configuration; snackbars queue one at a time in the host below. Action and close " +
                   "support hover, focus and pressed state layers, Escape dismisses a focused snackbar, " +
                   "and automatic dismissal pauses while keyboard focus is inside it.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        var buttons = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var item in Cases)
        {
            var caseButton = new Button { Content = item.Name };
            caseButton.Click += (_, _) => ShowCase(host, item, status);
            buttons.Children.Add(caseButton);
        }

        var longMessage = new Button { Content = "Long message (trimmed)" };
        longMessage.Click += (_, _) =>
        {
            var snackbar = CreateSnackbar(new SnackbarCase(
                "Long", LongText, null, MaterialSnackbarLineCount.TwoLines, true,
                MaterialSnackbarActionPlacement.Inline));
            Show(host, snackbar, status);
        };
        buttons.Children.Add(longMessage);

        var keepOpen = new Button { Content = "Keep open to inspect states" };
        keepOpen.Click += (_, _) =>
        {
            var snackbar = CreateSnackbar(
                new SnackbarCase("Inspect", ShortText, "Action", MaterialSnackbarLineCount.OneLine, true,
                    MaterialSnackbarActionPlacement.Inline));
            snackbar.Duration = Timeout.InfiniteTimeSpan;
            Show(host, snackbar, status);
        };
        buttons.Children.Add(keepOpen);

        var overlay = new Button { Content = "Show in window overlay" };
        overlay.Click += (_, _) => ShowInOverlay(overlay, status);
        buttons.Children.Add(overlay);

        page.Children.Add(buttons);
        page.Children.Add(CreateDemoSurface(host));
        page.Children.Add(status);
        return new ScrollViewer
        {
            Content = page,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control CreateDemoSurface(MaterialSnackbarHost host)
    {
        var hint = new TextBlock
        {
            Text = "Snackbars appear at the bottom of this surface.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.6,
        };
        hint.Bind(TextBlock.ForegroundProperty, hint.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var surface = new Border { CornerRadius = new CornerRadius(12) };
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("MaterialSurfaceContainerBrush"));
        surface.Child = hint;

        var demo = new Grid { Height = 280, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        demo.Children.Add(surface);
        demo.Children.Add(host);
        return demo;
    }

    private static void ShowCase(MaterialSnackbarHost host, SnackbarCase item, TextBlock status)
    {
        var snackbar = CreateSnackbar(item);
        // Defaults: 4 s for a plain one-line message, 10 s for two lines or an action.
        Show(host, snackbar, status);
    }

    private static MaterialSnackbar CreateSnackbar(SnackbarCase item) => new()
    {
        Text = item.Text,
        ActionContent = item.Action,
        LineCount = item.Lines,
        IsCloseButtonVisible = item.Close,
        ActionPlacement = item.Placement,
        Width = 344,
    };

    private static void Show(MaterialSnackbarHost host, MaterialSnackbar snackbar, TextBlock status)
    {
        snackbar.ActionInvoked += (_, _) => status.Text = "Action invoked.";
        snackbar.Closed += (_, e) => status.Text = $"Snackbar closed: {e.Reason}.";
        _ = host.Show(snackbar);
    }

    private static void ShowInOverlay(Control anchor, TextBlock status)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (topLevel is null)
        {
            status.Text = "The window is not attached yet; try again once it is open.";
            return;
        }

        var snackbar = CreateSnackbar(new SnackbarCase(
            "Overlay", ShortText, "Undo", MaterialSnackbarLineCount.OneLine, true,
            MaterialSnackbarActionPlacement.Inline));
        snackbar.ActionInvoked += (_, _) => status.Text = "Overlay action invoked.";
        snackbar.Closed += (_, e) => status.Text = $"Overlay snackbar closed: {e.Reason}.";

        try
        {
            _ = MaterialSnackbarHost.Show(topLevel, snackbar);
        }
        catch (InvalidOperationException exception)
        {
            // The window has no overlay layer yet; the embedded host still works.
            status.Text = exception.Message;
        }
    }
}
