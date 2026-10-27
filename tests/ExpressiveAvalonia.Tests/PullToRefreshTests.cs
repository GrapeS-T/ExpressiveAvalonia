using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Consumer contracts for PullToRefreshDefaults.IndicatorBox: indicator travel is independent
/// from the pull trigger threshold, and replacing the indicator does not reset the pull state.
/// </summary>
public class PullToRefreshTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void IndicatorBoxTravelUsesMaxDistance()
    {
        var indicator = new MaterialRefreshIndicatorBox
        {
            MaxDistance = 200,
            DistanceFraction = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var window = Show(indicator);
        try
        {
            var translation = Assert.IsType<TranslateTransform>(indicator.RenderTransform);
            Assert.Equal(60d, translation.Y); // 0.5 * 200 - 40

            indicator.DistanceFraction = 1;
            Assert.Equal(160d, translation.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BuiltInIndicatorTravelDoesNotFollowThreshold()
    {
        var refresh = new MaterialPullToRefresh
        {
            Width = 300,
            Height = 300,
            Threshold = 160,
            MaxDistance = 80,
            IsRefreshing = true,
            Content = new Border(),
        };
        var window = Show(refresh);
        try
        {
            var box = refresh.GetVisualDescendants().OfType<MaterialRefreshIndicatorBox>().Single();
            var translation = Assert.IsType<TranslateTransform>(box.RenderTransform);
            Assert.Equal(1d, refresh.DistanceFraction);
            Assert.Equal(40d, translation.Y); // 1 * 80 - 40

            refresh.Threshold = 40;
            window.UpdateLayout();
            Assert.Equal(1d, refresh.DistanceFraction);
            Assert.Equal(40d, translation.Y);

            refresh.MaxDistance = 120;
            window.UpdateLayout();
            Assert.Equal(120d, box.MaxDistance);
            Assert.Equal(80d, translation.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TemplateOnlyIndicatorRealizesWithoutReusingOwnerAsVisualContent()
    {
        var template = new FuncDataTemplate<MaterialPullToRefresh>((_, _) =>
            new Border { Name = "TemplateIndicator" });
        var refresh = new MaterialPullToRefresh
        {
            Width = 300,
            Height = 300,
            Content = new Border(),
            IndicatorTemplate = template,
        };
        var window = Show(refresh);
        try
        {
            var marker = refresh.GetVisualDescendants().OfType<Border>()
                .Single(candidate => candidate.Name == "TemplateIndicator");
            Assert.True(marker.IsVisible);
            Assert.False(refresh.GetVisualDescendants().OfType<MaterialRefreshIndicatorBox>().Single().IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReplacingIndicatorKeepsPullFractionAndTravel()
    {
        var refresh = new MaterialPullToRefresh
        {
            Width = 300,
            Height = 300,
            Threshold = 160,
            MaxDistance = 80,
            IsRefreshing = true,
            Content = new Border(),
        };
        var custom = new Border { Name = "CustomIndicator" };
        var window = Show(refresh);
        try
        {
            var builtIn = refresh.GetVisualDescendants().OfType<MaterialRefreshIndicatorBox>().Single();
            var translation = Assert.IsType<TranslateTransform>(builtIn.RenderTransform);
            Assert.Equal(1d, refresh.DistanceFraction);
            Assert.Equal(40d, translation.Y);

            refresh.IndicatorContent = custom;
            window.UpdateLayout();
            Assert.False(builtIn.IsVisible);
            Assert.Single(refresh.GetVisualDescendants().OfType<Border>(),
                candidate => candidate.Name == "CustomIndicator");
            Assert.Equal(1d, refresh.DistanceFraction);

            refresh.IndicatorContent = null;
            refresh.IndicatorType = MaterialRefreshIndicator.Loading;
            window.UpdateLayout();
            Assert.True(builtIn.IsVisible);
            Assert.Equal(1d, refresh.DistanceFraction);
            Assert.Equal(32d, translation.Y); // loading container is 48: 80 - 48
        }
        finally { window.Close(); }
    }
}