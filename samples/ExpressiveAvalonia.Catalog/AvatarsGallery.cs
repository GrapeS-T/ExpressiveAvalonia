using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Catalog page for the avatar, thumbnail and brand-mark family:
/// <see cref="MaterialAvatar"/>, <see cref="MaterialThumbnail"/> and <see cref="MaterialBrandMark"/>.
/// </summary>
/// <remarks>
/// The page covers the source packages assigned to the avatar family: Generic avatar
/// (three styles plus the image style), the thirty 3D Avatars, .Building Blocks/Monogram,
/// image-Thumbnail, video-Thumbnail, Colourful logo and Favicon. Every control is the real one;
/// interactive avatars and thumbnails respond to pointer, Space and Enter and report clicks in
/// the status line at the top.
/// </remarks>
public static class AvatarsGallery
{
    public static Control Create()
    {
        var status = new TextBlock
        {
            Text = "No activation yet.",
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        };
        var activations = 0;

        void Activated(string what)
        {
            activations++;
            status.Text = $"Activated {what} ({activations} total).";
        }

        var page = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        page.Children.Add(new TextBlock { Text = "Avatars, thumbnails and brand marks", FontSize = 28 });
        page.Children.Add(status);

        page.Children.Add(Caption("Generic avatar - three source styles"));
        var styles = Row();
        foreach (var kind in new[] { MaterialAvatarKind.Placeholder, MaterialAvatarKind.Check, MaterialAvatarKind.Monogram })
        {
            var avatar = new MaterialAvatar { Kind = kind, Letter = "A", VerticalAlignment = VerticalAlignment.Center };
            styles.Children.Add(Labeled(kind.ToString(), avatar));
        }
        // The check style also reacts to IsChecked, which the source leaves static.
        var toggle = new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Check,
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var toggleButton = new Button { Content = "Toggle check", VerticalAlignment = VerticalAlignment.Center };
        toggleButton.Click += (_, _) => toggle.IsChecked = !toggle.IsChecked;
        styles.Children.Add(Labeled("Check (IsChecked=false)", toggle));
        styles.Children.Add(toggleButton);
        page.Children.Add(styles);

        page.Children.Add(Caption("Generic avatar - sizes (source component is 40 dp)"));
        var sizes = Row();
        foreach (var size in new[] { 24d, 32d, 40d, 56d, 96d })
        {
            sizes.Children.Add(Labeled($"{size} dp", new MaterialAvatar
            {
                Kind = MaterialAvatarKind.Placeholder,
                Size = size,
                VerticalAlignment = VerticalAlignment.Center,
            }));
        }
        page.Children.Add(sizes);

        page.Children.Add(Caption("Generic avatar - monograms"));
        var monograms = Row();
        foreach (var (letter, label) in new[] { ("A", "Letter=A (source default)"), ("AL", "Two letters"), ("", "Empty") })
        {
            monograms.Children.Add(Labeled(label, new MaterialAvatar
            {
                Kind = MaterialAvatarKind.Monogram,
                Letter = letter,
                VerticalAlignment = VerticalAlignment.Center,
            }));
        }
        page.Children.Add(monograms);

        page.Children.Add(Caption("3D Avatars / 1..30 - embedded source images"));
        var grid = new WrapPanel { ItemSpacing = 12, LineSpacing = 12 };
        foreach (var info in MaterialAvatars.All)
        {
            var avatar = new MaterialAvatar
            {
                Kind = MaterialAvatarKind.Image,
                Source = info.Image,
                Size = 56,
                IsInteractive = true,
            };
            AutomationProperties.SetName(avatar, info.Name);
            ToolTip.SetTip(avatar, $"{info.Name} ({info.SourceId})");
            avatar.Click += (_, _) => Activated(info.Name);
            grid.Children.Add(Labeled($"{info.Index}. {info.SourceId}", avatar, 96));
        }
        page.Children.Add(grid);

        page.Children.Add(Caption("Interactive avatar - Click, Command, Space and Enter"));
        var interactiveStatus = new TextBlock { Text = "Press the avatar or focus it and press Space/Enter." };
        var interactive = new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Monogram,
            Letter = "M",
            Size = 64,
            IsInteractive = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(interactive, "Open profile");
        interactive.Click += (_, _) => Activated("the monogram avatar");
        var commandAvatar = new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Placeholder,
            Size = 64,
            IsInteractive = true,
            Command = new ActionCommand(() => Activated("the placeholder avatar command")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(commandAvatar, "Open placeholder profile");
        var disabledAvatar = new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Placeholder,
            Size = 64,
            IsInteractive = true,
            IsEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(disabledAvatar, "Unavailable profile");
        var interactiveRow = Row();
        interactiveRow.Children.Add(interactive);
        interactiveRow.Children.Add(commandAvatar);
        interactiveRow.Children.Add(disabledAvatar);
        interactiveRow.Children.Add(interactiveStatus);
        page.Children.Add(interactiveRow);

        page.Children.Add(Caption(".Building Blocks/image-Thumbnail (56 x 56)"));
        var imageThumbnails = Row();
        imageThumbnails.Children.Add(Labeled("Source image", new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Image,
            Source = MaterialAvatars.GetByIndex(2).Image,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        imageThumbnails.Children.Add(Labeled("Source placeholder (no Source)", new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Image,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        imageThumbnails.Children.Add(Labeled("Rounded + 96 dp", new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Image,
            Source = MaterialAvatars.GetByIndex(5).Image,
            Width = 96,
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Center,
        }));
        page.Children.Add(imageThumbnails);

        page.Children.Add(Caption(".Building Blocks/video-Thumbnail (114 x 64)"));
        var videoThumbnails = Row();
        var video = new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Video,
            Source = MaterialAvatars.GetByIndex(9).Image,
            IsInteractive = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(video, "Play video");
        video.Click += (_, _) => Activated("the video thumbnail");
        videoThumbnails.Children.Add(Labeled("Source image, interactive", video));
        videoThumbnails.Children.Add(Labeled("Source placeholder (no Source)", new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Video,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        videoThumbnails.Children.Add(Labeled("Rounded", new MaterialThumbnail
        {
            Kind = MaterialThumbnailKind.Video,
            Source = MaterialAvatars.GetByIndex(12).Image,
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center,
        }));
        page.Children.Add(videoThumbnails);

        page.Children.Add(Caption(".Building Blocks/Monogram and brand marks (18 x 18)"));
        var marks = Row();
        marks.Children.Add(Labeled("Monogram avatar", new MaterialAvatar
        {
            Kind = MaterialAvatarKind.Monogram,
            Letter = "M",
            VerticalAlignment = VerticalAlignment.Center,
        }));
        marks.Children.Add(Labeled("Colourful logo 18 dp", new MaterialBrandMark
        {
            Kind = MaterialBrandMarkKind.ColourfulLogo,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        marks.Children.Add(Labeled("Colourful logo 48 dp", new MaterialBrandMark
        {
            Kind = MaterialBrandMarkKind.ColourfulLogo,
            Size = 48,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        marks.Children.Add(Labeled("Favicon 18 dp", new MaterialBrandMark
        {
            Kind = MaterialBrandMarkKind.Favicon,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        marks.Children.Add(Labeled("Favicon 48 dp", new MaterialBrandMark
        {
            Kind = MaterialBrandMarkKind.Favicon,
            Size = 48,
            VerticalAlignment = VerticalAlignment.Center,
        }));
        page.Children.Add(marks);

        return new ScrollViewer
        {
            Content = page,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static StackPanel Row() => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 16,
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.Medium,
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static Control Labeled(string label, Control content, double width = double.NaN)
    {
        var panel = new StackPanel { Spacing = 6, Width = width };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        return panel;
    }

    private sealed class ActionCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
