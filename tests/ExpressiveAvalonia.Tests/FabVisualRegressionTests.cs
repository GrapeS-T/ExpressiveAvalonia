using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

public class FabVisualRegressionTests
{
    [AvaloniaFact]
    public void FabIconPreservesMaterialViewport()
    {
        var icon = new PathIcon { Data = Geometry.Parse("M 5,5 L 19,5 L 19,19 Z") };
        var fab = new MaterialFab { Content = icon };
        var window = new Window { Width = 300, Height = 200, Content = fab };
        window.Show();
        try
        {
            window.UpdateLayout();
            var path = icon.GetVisualDescendants().OfType<Path>().Single();
            var bounds = path.RenderedGeometry!.Bounds;
            var topLeft = path.TranslatePoint(bounds.TopLeft, fab)!.Value;
            var bottomRight = path.TranslatePoint(bounds.BottomRight, fab)!.Value;
            Assert.Equal(14, bottomRight.X - topLeft.X, 3);
            Assert.Equal(14, bottomRight.Y - topLeft.Y, 3);
            Assert.Equal(fab.Bounds.Width / 2, (topLeft.X + bottomRight.X) / 2, 3);
            Assert.Equal(fab.Bounds.Height / 2, (topLeft.Y + bottomRight.Y) / 2, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExpressiveIconLabelSpacingUsesSizeTokens()
    {
        var fab = new MaterialExtendedFab
        {
            Icon = Geometry.Parse("M 0,0 L 24,0 L 24,24 Z"),
            Label = "Action",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var window = new Window { Width = 600, Height = 400, Content = fab };
        window.Show();
        try
        {
            foreach (var (size, gap) in new[]
            {
                (MaterialExtendedFabSize.Small, 8d),
                (MaterialExtendedFabSize.Medium, 12d),
                (MaterialExtendedFabSize.Large, 16d)
            })
            {
                fab.Size = size;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var label = fab.GetVisualDescendants().OfType<TextBlock>()
                    .Single(element => element.Name == "PART_Label");
                var icon = fab.GetVisualDescendants().OfType<PathIcon>()
                    .Single(element => element.Name == "PART_Icon");
                var iconRight = icon.TranslatePoint(new Point(icon.Bounds.Width, 0), fab)!.Value.X;
                var labelLeft = label.TranslatePoint(default, fab)!.Value.X;
                Assert.Equal(gap, labelLeft - iconRight, 3);
            }
        }
        finally { window.Close(); }
    }
}
