using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class XrBadgeStatusTests
{
    [AvaloniaFact]
    public void BarAndRailBadgeStatusFollowsVisibleBadgeAndPreservesLocalOverride()
    {
        var bar = new XrNavigationBarItem { Label = "Inbox", Badge = XrBadgeKind.Large, BadgeText = "3" };
        var rail = new XrNavigationRailItem { Label = "Inbox", Badge = XrBadgeKind.Large, BadgeText = "3" };
        var window = new Window { Content = new StackPanel { Children = { bar, rail } } };
        window.Resources["MaterialNavigationBarBadgeDotText"] = "New notification";
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.Equal("3", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("3", AutomationProperties.GetItemStatus(rail));
            bar.BadgeText = rail.BadgeText = "8";
            Assert.Equal("8", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("8", AutomationProperties.GetItemStatus(rail));
            bar.Badge = rail.Badge = XrBadgeKind.None;
            Assert.Null(AutomationProperties.GetItemStatus(bar));
            Assert.Null(AutomationProperties.GetItemStatus(rail));
            bar.Badge = rail.Badge = XrBadgeKind.Small;
            Assert.Equal("New notification", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("New notification", AutomationProperties.GetItemStatus(rail));
            window.Resources["MaterialNavigationBarBadgeDotText"] = "Notification";
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(rail));
            AutomationProperties.SetItemStatus(bar, "Notification");
            AutomationProperties.SetItemStatus(rail, "Notification");
            bar.Badge = rail.Badge = XrBadgeKind.Large;
            bar.BadgeText = rail.BadgeText = "12";
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(rail));
            bar.Badge = rail.Badge = XrBadgeKind.None;
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(bar));
            Assert.Equal("Notification", AutomationProperties.GetItemStatus(rail));
        }
        finally { window.Close(); }
    }
}
