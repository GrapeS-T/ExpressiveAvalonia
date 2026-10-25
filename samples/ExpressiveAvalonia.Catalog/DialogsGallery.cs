using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Gallery for the dialog family: basic dialog, list dialog and scrollable list dialog.
/// </summary>
/// <remarks>
/// The inline previews are ordinary <see cref="MaterialDialog"/> instances, so the same template
/// is shown without a modal host. The launch buttons present fresh instances with
/// <see cref="MaterialDialog.ShowAsync(TopLevel, MaterialDialog)"/> and report the returned result.
/// </remarks>
public static class DialogsGallery
{
    private const string SupportingCopy =
        "A dialog is a type of modal window that appears in front of app content to provide critical information, or prompt for a decision to be made.";

    public static Control Create()
    {
        var status = new TextBlock
        {
            Text = "No dialog has been closed yet. Escape dismisses, Enter activates the default action and focus returns to the opener.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 760,
        };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);
        status.Bind(TextBlock.ForegroundProperty, status.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var page = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        page.Children.Add(new TextBlock
        {
            Text = "Dialogs",
            FontSize = 32,
            FontWeight = FontWeight.Medium,
        });
        page.Children.Add(new TextBlock
        {
            Text = "Material 3 dialog family from the source design packages: basic dialog, list dialog and scrollable list dialog. "
                 + "The overlay blocks the page, Tab and Shift+Tab cycle inside the dialog, Escape and scrim clicks dismiss it, "
                 + "Enter activates the default action, and closing restores focus to the element that opened it.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 820,
        });

        var launchers = new WrapPanel { ItemSpacing = 12, LineSpacing = 12 };
        launchers.Children.Add(Launch("Open basic dialog", status, () => BasicDialog(status)));
        launchers.Children.Add(Launch("Open icon dialog", status, () => IconDialog(status)));
        launchers.Children.Add(Launch("Open list dialog", status, () => ListDialog(status, 3, false, threeLine: false)));
        launchers.Children.Add(Launch("Open scrollable list dialog", status, () => ListDialog(status, 9, true, threeLine: false)));
        launchers.Children.Add(Launch("Open non-dismissible dialog", status, () => NonDismissibleDialog(status)));
        launchers.Children.Add(Launch("Open text field dialog", status, () => InputDialog(status)));
        page.Children.Add(launchers);
        page.Children.Add(status);

        var previews = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };
        previews.Children.Add(Preview("Basic dialog · no icon", BasicDialog(status)));
        previews.Children.Add(Preview("Basic dialog · icon and divider", IconDialog(status)));
        previews.Children.Add(Preview("List dialog · checkbox rows", ListDialog(status, 3, false, threeLine: false)));
        previews.Children.Add(Preview("Scrollable list dialog · 9 rows", ListDialog(status, 9, true, threeLine: false)));
        previews.Children.Add(Preview("List dialog · three-line rows", ListDialog(status, 3, false, threeLine: true)));
        previews.Children.Add(Preview("Content slot · text field", InputDialog(status)));
        page.Children.Add(previews);

        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static Button Launch(string label, TextBlock status, Func<MaterialDialog> factory)
    {
        var button = new Button { Content = label };
        button.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(button) is not { } owner)
                return;
            var dialog = factory();
            var result = await MaterialDialog.ShowAsync(owner, dialog);
            status.Text = $"Last result: {result ?? "(dismissed)"} · {label} · focus returned to the opener.";
        };
        return button;
    }

    private static Control Preview(string caption, MaterialDialog dialog)
    {
        var label = new TextBlock { Text = caption };
        MaterialTypography.SetStyle(label, MaterialTypeStyle.LabelLarge);
        label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                label,
                dialog,
            },
        };
    }

    private static MaterialDialog BasicDialog(TextBlock status)
    {
        var dialog = new MaterialDialog
        {
            Title = "Basic dialog title",
            SupportingText = SupportingCopy,
            Width = 312,
        };
        dialog.AddAction(PreviewAction("Action 2", "secondary", status, isCancel: true));
        dialog.AddAction(PreviewAction("Action 1", "primary", status, isDefault: true));
        return dialog;
    }

    private static MaterialDialog IconDialog(TextBlock status)
    {
        var dialog = new MaterialDialog
        {
            Title = "Basic dialog title",
            SupportingText = SupportingCopy,
            Icon = MaterialDialog.CreateIcon(),
            ShowDivider = true,
            Width = 312,
        };
        dialog.AddAction(PreviewAction("Action 2", "secondary", status, isCancel: true));
        dialog.AddAction(PreviewAction("Action 1", "primary", status, isDefault: true));
        return dialog;
    }

    private static MaterialDialog ListDialog(TextBlock status, int count, bool scrollable, bool threeLine)
    {
        var dialog = new MaterialDialog
        {
            Title = "Dialog title",
            SupportingText = SupportingCopy,
            Icon = MaterialDialog.CreateIcon(),
            List = CreateList(count, scrollable, threeLine),
            Width = 312,
        };
        dialog.AddAction(PreviewAction("Action 2", "secondary", status, isCancel: true));
        dialog.AddAction(PreviewAction("Action 1", "primary", status, isDefault: true));
        return dialog;
    }

    private static MaterialDialog InputDialog(TextBlock status)
    {
        var field = new TextBox { PlaceholderText = "Type a name", Margin = new Thickness(0) };
        var dialog = new MaterialDialog
        {
            Title = "Rename item",
            SupportingText = "The body slot accepts any control. Enter activates the default action while the field has focus.",
            Content = field,
            Width = 312,
        };
        dialog.AddAction(new MaterialDialogAction("Cancel", "cancelled")
        {
            IsCancel = true,
            Command = new DelegateCommand(() => status.Text = "Rename cancelled."),
        });
        // CloseOnInvoke is false: the command decides when to close and with which result.
        dialog.AddAction(new MaterialDialogAction("Save", "saved")
        {
            IsDefault = true,
            CloseOnInvoke = false,
            Command = new DelegateCommand(() =>
            {
                var result = $"saved '{field.Text}'";
                status.Text = $"Save invoked · result '{result}'.";
                dialog.Close(result);
            }),
        });
        return dialog;
    }

    private static MaterialDialog NonDismissibleDialog(TextBlock status)
    {
        var dialog = new MaterialDialog
        {
            Title = "Confirm update",
            SupportingText = "Escape and scrim clicks are disabled for this dialog. Choose an action to continue.",
            IsDismissible = false,
            CloseOnScrimClick = false,
            Width = 312,
        };
        dialog.AddAction(PreviewAction("Later", "later", status));
        dialog.AddAction(PreviewAction("Update", "updated", status, isDefault: true));
        return dialog;
    }

    private static MaterialDialogList CreateList(int count, bool scrollable, bool threeLine)
    {
        var list = new MaterialDialogList { IsScrollable = scrollable };
        for (var index = 0; index < count; index++)
        {
            var item = new MaterialDialogListItem
            {
                Initial = "A",
                Overline = threeLine ? "Overline" : null,
                Headline = "List item",
                SupportingText = threeLine ? "Supporting line text" : null,
                TrailingText = "100+",
                IsSelected = true,
            };
            MaterialList.SetLineCount(item, threeLine ? 3 : 1);
            list.Items.Add(item);
        }
        return list;
    }

    private static MaterialDialogAction PreviewAction(
        string text,
        object result,
        TextBlock status,
        bool isDefault = false,
        bool isCancel = false)
    {
        return new MaterialDialogAction(text, result)
        {
            IsDefault = isDefault,
            IsCancel = isCancel,
            Command = new DelegateCommand(() => status.Text = $"Action '{text}' invoked · result '{result}'."),
        };
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
