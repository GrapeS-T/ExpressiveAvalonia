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
/// Gallery for the sheet family: standard and modal side sheets (with and without the back
/// affordance) and standard and modal bottom sheets with drag-to-dismiss.
/// </summary>
public static class SheetsGallery
{
    public static Control Create()
    {
        var status = new TextBlock
        {
            Text = "No sheet opened. Use a button below.",
            Margin = new Thickness(32, 20, 32, 0),
            FontSize = 14,
            Foreground = Brushes.Gray
        };

        var page = new StackPanel { Margin = new Thickness(32, 20, 32, 32), Spacing = 20 };
        page.Children.Add(new TextBlock { Text = "Sheets", FontSize = 32, FontWeight = FontWeight.SemiBold });
        page.Children.Add(new TextBlock
        {
            Text = "Every sheet is hosted in the same overlay. Modal sheets block the page, cycle Tab " +
                   "focus and dismiss on Escape or a scrim click; standard sheets leave the page interactive. " +
                   "Bottom sheets also dismiss by dragging the handle down.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 720
        });

        var sideStandard = BuildSideSheet(modal: false, withBack: false, status);
        var sideModal = BuildSideSheet(modal: true, withBack: true, status);
        var bottomStandard = BuildBottomSheet(modal: false, status);
        var bottomModal = BuildBottomSheet(modal: true, status);
        var sheets = new MaterialSheet[] { sideStandard, sideModal, bottomStandard, bottomModal };

        foreach (var sheet in sheets)
            Track(sheet, status);

        var buttons = new WrapPanel { ItemSpacing = 12, LineSpacing = 12 };
        buttons.Children.Add(OpenButton("Standard side sheet", MaterialButtonVariant.Outlined, sheets, sideStandard, status));
        buttons.Children.Add(OpenButton("Modal side sheet (back)", MaterialButtonVariant.Filled, sheets, sideModal, status));
        buttons.Children.Add(OpenButton("Standard bottom sheet", MaterialButtonVariant.Outlined, sheets, bottomStandard, status));
        buttons.Children.Add(OpenButton("Modal bottom sheet", MaterialButtonVariant.Filled, sheets, bottomModal, status));
        page.Children.Add(buttons);

        page.Children.Add(new TextBlock
        {
            Text = "Modal side sheet: Back is handled inside the sheet and reports through BackRequested. " +
                   "Bottom sheets: drag the handle past 96 units or fling down to dismiss.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 720,
            Foreground = Brushes.Gray
        });

        var content = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        content.Children.Add(status);
        content.Children.Add(new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

        var root = new Grid();
        root.Children.Add(content);
        foreach (var sheet in sheets)
            root.Children.Add(sheet);
        return root;
    }

    private static Button OpenButton(string label, MaterialButtonVariant variant,
        MaterialSheet[] sheets, MaterialSheet target, TextBlock status)
    {
        var button = new Button { Content = label };
        MaterialAssist.SetButtonVariant(button, variant);
        MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
        button.Click += (_, _) =>
        {
            foreach (var sheet in sheets)
            {
                if (!ReferenceEquals(sheet, target))
                    sheet.IsOpen = false;
            }

            target.IsOpen = true;
            status.Text = $"{label}: opening";
        };
        return button;
    }

    private static void Track(MaterialSheet sheet, TextBlock status)
    {
        var name = sheet switch
        {
            MaterialSideSheet { IsModal: true } => "Modal side sheet",
            MaterialSideSheet => "Standard side sheet",
            MaterialBottomSheet { IsModal: true } => "Modal bottom sheet",
            _ => "Standard bottom sheet"
        };
        sheet.Opening += (_, _) => status.Text = $"{name}: opening";
        sheet.Opened += (_, _) => status.Text = $"{name}: open";
        sheet.Closing += (_, _) => status.Text = $"{name}: closing";
        sheet.Closed += (_, _) => status.Text = $"{name}: closed";
    }

    private static MaterialSideSheet BuildSideSheet(bool modal, bool withBack, TextBlock status)
    {
        var step = new TextBlock
        {
            Text = "Content slot",
            FontSize = 20,
            Foreground = Brushes.Gray
        };

        var sheet = new MaterialSideSheet
        {
            Headline = withBack ? "Filters" : "Details",
            IsModal = modal,
            ShowBackButton = withBack,
            ShowCloseButton = true,
            Side = MaterialSheetSide.End,
            Content = SideContent(step)
        };

        if (withBack)
        {
            sheet.BackRequested += (_, e) =>
            {
                step.Text = "Back handled inside the sheet";
                status.Text = "Modal side sheet: BackRequested handled without closing.";
                e.Handled = true;
            };
        }

        sheet.Actions = SheetActions(sheet, status, "Save");
        return sheet;
    }

    private static Control SideContent(TextBlock step)
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24, 0, 24, 24) };
        panel.Children.Add(step);
        panel.Children.Add(new TextBlock
        {
            Text = "The content slot scrolls independently of the header and the actions. " +
                   "Focus starts inside the sheet and returns to the invoking button when it closes.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        var query = new TextBox { PlaceholderText = "Search" };
        MaterialTextField.SetLabel(query, "Query");
        MaterialTextField.SetSupportingText(query, "Tab moves through the sheet content");
        panel.Children.Add(query);

        for (var i = 1; i <= 6; i++)
        {
            var option = new CheckBox { Content = $"Option {i}" };
            AutomationProperties.SetName(option, $"Option {i}");
            panel.Children.Add(option);
        }

        return panel;
    }

    private static MaterialBottomSheet BuildBottomSheet(bool modal, TextBlock status)
    {
        var sheet = new MaterialBottomSheet
        {
            IsModal = modal,
            ShowDragHandle = true
        };
        // The source bottom sheet has no header text or action area; only the drag handle and
        // the content slot. Actions stay available for consumers that opt in.
        sheet.Content = BottomContent(status, sheet);
        return sheet;
    }

    private static Control BottomContent(TextBlock status, MaterialBottomSheet sheet)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24, 4, 24, 24) };
        panel.Children.Add(new TextBlock { Text = "Content slot", FontSize = 20, Foreground = Brushes.Gray });
        panel.Children.Add(new TextBlock
        {
            Text = "The sheet hugs its content up to the available height; longer content scrolls " +
                   "inside the sheet. Drag the handle down to dismiss.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        for (var i = 1; i <= 10; i++)
            panel.Children.Add(new TextBlock { Text = $"List item {i}" });

        var close = new Button { Content = "Close from content", HorizontalAlignment = HorizontalAlignment.Left };
        MaterialAssist.SetButtonVariant(close, MaterialButtonVariant.Text);
        MaterialAssist.SetControlSize(close, MaterialControlSize.Small);
        close.Click += (_, _) =>
        {
            status.Text = "Closed from the content slot";
            sheet.IsOpen = false;
        };
        panel.Children.Add(close);
        return panel;
    }

    private static Control SheetActions(MaterialSheet sheet, TextBlock status, string primaryLabel)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var primary = new Button { Content = primaryLabel };
        MaterialAssist.SetButtonVariant(primary, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(primary, MaterialControlSize.Small);
        primary.Click += (_, _) =>
        {
            status.Text = $"{primaryLabel} pressed; closing";
            sheet.IsOpen = false;
        };

        var secondary = new Button { Content = "Cancel" };
        MaterialAssist.SetButtonVariant(secondary, MaterialButtonVariant.Outlined);
        MaterialAssist.SetControlSize(secondary, MaterialControlSize.Small);
        secondary.Click += (_, _) => sheet.IsOpen = false;

        row.Children.Add(primary);
        row.Children.Add(secondary);
        return row;
    }
}
