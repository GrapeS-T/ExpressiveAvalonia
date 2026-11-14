using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class BadgeStatusTests
{
    [AvaloniaFact]
    public void BadgeStatusFollowsContentVisibilityLocalizationAndAnchorReplacement()
    {
        var anchor = new Button { Content = "Inbox" };
        var badge = new MaterialBadge { Text = "3" };
        var box = new MaterialBadgedBox { Content = anchor, Badge = badge };
        var window = new Window { Width = 300, Height = 200, Content = box };
        window.Resources["MaterialNavigationBarBadgeDotText"] = "Новое уведомление";
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.Equal("3", AutomationProperties.GetItemStatus(anchor));
            badge.Text = "8";
            Assert.Equal("8", AutomationProperties.GetItemStatus(anchor));
            badge.Size = MaterialBadgeSize.Small;
            Assert.Equal("Новое уведомление", AutomationProperties.GetItemStatus(anchor));
            window.Resources["MaterialNavigationBarBadgeDotText"] = "Notification";
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(anchor));
            badge.IsVisible = false;
            Assert.Null(AutomationProperties.GetItemStatus(anchor));
            badge.IsVisible = true;
            var replacement = new Button { Content = "Mail" };
            box.Content = replacement;
            window.UpdateLayout();
            Assert.Null(AutomationProperties.GetItemStatus(anchor));
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(replacement));
            window.Content = null;
            Assert.Null(AutomationProperties.GetItemStatus(replacement));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExplicitAnchorStatusAndBadgeAccessibilityViewSurviveUpdates()
    {
        var anchor = new Button { Content = "Inbox" };
        var badge = new MaterialBadge { Text = "3" };
        AutomationProperties.SetItemStatus(anchor, "3");
        AutomationProperties.SetAccessibilityView(badge, AccessibilityView.Content);
        var box = new MaterialBadgedBox { Content = anchor, Badge = badge };
        var window = new Window { Width = 300, Height = 200, Content = box };
        window.Show();
        window.UpdateLayout();
        try
        {
            badge.Text = "9";
            Assert.Equal("3", AutomationProperties.GetItemStatus(anchor));
            Assert.Equal(AccessibilityView.Content, AutomationProperties.GetAccessibilityView(badge));
            window.Content = null;
            Assert.Equal("3", AutomationProperties.GetItemStatus(anchor));
        }
        finally { window.Close(); }
    }
}
