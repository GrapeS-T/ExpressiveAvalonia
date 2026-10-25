using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Catalogue page for the shape family: <see cref="MaterialShape"/>, <see cref="MaterialDivider"/>,
/// <see cref="MaterialScrim"/>, <see cref="MaterialFocusIndicator"/> and
/// <see cref="MaterialStateLayer"/>.
/// </summary>
public static class ShapesGallery
{
    public static Control Create()
    {
        var pages = new TabControl();
        pages.Items.Add(new TabItem { Header = "Shapes", Content = new DeferredGallery(() => Shapes()) });
        pages.Items.Add(new TabItem { Header = "Dividers", Content = new DeferredGallery(() => Dividers()) });
        pages.Items.Add(new TabItem { Header = "Scrim", Content = new DeferredGallery(() => Scrim()) });
        pages.Items.Add(new TabItem { Header = "Focus indicator", Content = new DeferredGallery(() => FocusIndicator()) });
        pages.Items.Add(new TabItem { Header = "State layers", Content = new DeferredGallery(() => StateLayers()) });
        return pages;
    }

    private static StackPanel Panel() => new() { Spacing = 16, Margin = new Thickness(24) };

    private static Control Scroll(Control content) => new ScrollViewer
    {
        Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeight.Medium,
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.Parse("#49454F")),
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 760,
    };

    // ------------------------------------------------------------------ shapes

    private static Control Shapes()
    {
        var panel = Panel();
        panel.Children.Add(Heading("Shape Set (local-004595)"));
        panel.Children.Add(Caption(
            "35 named geometries with verbatim source path data. The slider resizes every shape; " +
            "Stretch=Uniform keeps each source aspect ratio."));

        var named = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        var shapes = new List<MaterialShape>();
        foreach (var kind in Enum.GetValues<MaterialShapeKind>())
        {
            if (MaterialShapeGeometries.IsExpressive(kind))
                continue;

            var shape = new MaterialShape { Kind = kind, Width = 64, Height = 64 };
            shapes.Add(shape);
            named.Children.Add(Tile(shape, kind.ToString()));
        }

        var size = new Slider
        {
            Minimum = 32,
            Maximum = 96,
            Value = 64,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 8,
            IsSnapToTickEnabled = true,
        };
        size.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty)
                return;
            foreach (var shape in shapes)
            {
                shape.Width = size.Value;
                shape.Height = size.Value;
            }
        };
        panel.Children.Add(size);
        panel.Children.Add(named);

        panel.Children.Add(Heading(".Shape (local-004585)"));
        panel.Children.Add(Caption(
            "Nine numbered source geometries. The source paints them with the primary-container " +
            "role, so they look different from the named set by default."));
        var numbered = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        foreach (var kind in Enum.GetValues<MaterialShapeKind>())
        {
            if (!MaterialShapeGeometries.IsExpressive(kind))
                continue;
            numbered.Children.Add(Tile(new MaterialShape { Kind = kind, Width = 64, Height = 64 }, kind.ToString()));
        }
        panel.Children.Add(numbered);

        panel.Children.Add(Heading("Overrides"));
        panel.Children.Add(Caption(
            "MaterialShape is an Avalonia Shape: Fill, Stroke, StrokeThickness and Stretch are the " +
            "stock properties, and Stretch=None reproduces the source's untransformed path."));
        var overrides = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        overrides.Children.Add(new MaterialShape
        {
            Kind = MaterialShapeKind.Burst,
            Width = 72,
            Height = 72,
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.Parse("#6750A4")),
            StrokeThickness = 3,
        });
        overrides.Children.Add(new MaterialShape
        {
            Kind = MaterialShapeKind.Heart,
            Width = 120,
            Height = 72,
            Stretch = Stretch.None,
            Fill = new SolidColorBrush(Color.Parse("#7D5260")),
        });
        overrides.Children.Add(new MaterialShape
        {
            Kind = MaterialShapeKind.Flower,
            Width = 72,
            Height = 72,
            Fill = new SolidColorBrush(Color.Parse("#386A20")),
        });
        panel.Children.Add(overrides);
        return Scroll(panel);
    }

    private static Control Tile(MaterialShape shape, string label)
    {
        shape.HorizontalAlignment = HorizontalAlignment.Center;
        var tile = new StackPanel { Spacing = 4, Width = 88 };
        tile.Children.Add(shape);
        tile.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return tile;
    }

    // ---------------------------------------------------------------- dividers

    private static Control Dividers()
    {
        var panel = Panel();
        panel.Children.Add(Heading("Live divider"));
        panel.Children.Add(Caption(
            "Variant selects the source inset; Orientation switches to the vertical packages. " +
            "Subheader reproduces Horizontal/Divider with subhead (use MiddleInset for the exact " +
            "16 dp line and label insets of the 320 dp source frame)."));

        var divider = new MaterialDivider { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        var frame = new Border
        {
            Child = divider,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var variants = new ComboBox
        {
            ItemsSource = Enum.GetValues<MaterialDividerVariant>(),
            SelectedIndex = 0,
            Width = 220,
        };
        variants.SelectionChanged += (_, _) =>
        {
            if (variants.SelectedItem is MaterialDividerVariant variant)
                divider.Variant = variant;
        };

        var subheader = new TextBox { PlaceholderText = "Subheader", Width = 220 };
        subheader.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                divider.Subheader = string.IsNullOrEmpty(subheader.Text) ? null : subheader.Text;
        };

        var vertical = new ToggleSwitch { Content = "Vertical" };
        vertical.PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleSwitch.IsCheckedProperty)
                return;
            var isVertical = vertical.IsChecked == true;
            divider.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;
            divider.Width = isVertical ? double.NaN : 320;
            divider.Height = isVertical ? 120 : double.NaN;
            frame.Width = isVertical ? 40 : double.NaN;
            frame.Height = isVertical ? 120 : double.NaN;
        };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        controls.Children.Add(variants);
        controls.Children.Add(subheader);
        controls.Children.Add(vertical);
        panel.Children.Add(controls);
        panel.Children.Add(frame);

        panel.Children.Add(Heading("Source variants"));
        panel.Children.Add(Caption(
            "Horizontal: Full-width, Inset (16 dp leading), Middle-inset (16 dp both ends), " +
            "Divider with subhead. Vertical uses the same 16 dp insets and no subheader."));

        var horizontal = new StackPanel { Spacing = 12, Width = 320 };
        foreach (var variant in Enum.GetValues<MaterialDividerVariant>())
        {
            horizontal.Children.Add(new TextBlock { Text = "Horizontal/" + variant, FontSize = 12 });
            horizontal.Children.Add(new MaterialDivider { Variant = variant });
        }
        horizontal.Children.Add(new TextBlock { Text = "Horizontal/Divider with subhead", FontSize = 12 });
        horizontal.Children.Add(new MaterialDivider
        {
            Variant = MaterialDividerVariant.MiddleInset,
            Subheader = "Subheader",
        });

        var verticalRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 32,
            Height = 120,
        };
        foreach (var variant in Enum.GetValues<MaterialDividerVariant>())
        {
            var column = new StackPanel { Spacing = 8, Width = 96 };
            column.Children.Add(new TextBlock { Text = "Vertical/" + variant, FontSize = 12 });
            column.Children.Add(new MaterialDivider
            {
                Orientation = Orientation.Vertical,
                Variant = variant,
                Height = 88,
            });
            verticalRow.Children.Add(column);
        }

        var examples = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 40 };
        examples.Children.Add(horizontal);
        examples.Children.Add(verticalRow);
        panel.Children.Add(examples);

        panel.Children.Add(Heading("In a list"));
        panel.Children.Add(Caption("Full-width dividers between rows, middle-inset before a section subheader."));
        var list = new StackPanel { Width = 320 };
        list.Children.Add(new TextBlock { Text = "Inbox", Margin = new Thickness(16, 8) });
        list.Children.Add(new MaterialDivider());
        list.Children.Add(new TextBlock { Text = "Drafts", Margin = new Thickness(16, 8) });
        list.Children.Add(new MaterialDivider());
        list.Children.Add(new MaterialDivider
        {
            Variant = MaterialDividerVariant.MiddleInset,
            Subheader = "More labels",
        });
        list.Children.Add(new TextBlock { Text = "Sent", Margin = new Thickness(16, 8) });
        panel.Children.Add(list);
        return Scroll(panel);
    }

    // ------------------------------------------------------------------- scrim

    private static Control Scrim()
    {
        var panel = Panel();
        panel.Children.Add(Heading("Scrim"));
        panel.Children.Add(Caption(
            "The scrim covers this 260 dp surface at 32% of the scrim role. Click it or press " +
            "Escape to dismiss; the status line shows the dismissal request. Turn off " +
            "Dismissible to require an explicit action from the host."));

        var status = new TextBlock { Text = "Scrim closed.", FontSize = 13 };

        var surface = new Border
        {
            Height = 260,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.Parse("#FEF7FF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CAC4D0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
        };

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        content.Children.Add(new TextBlock { Text = "Modal content behind the scrim", FontSize = 16 });
        content.Children.Add(new Button { Content = "Underlying action" });
        content.Children.Add(new TextBlock
        {
            Text = "While the scrim is open this button cannot be clicked and the scrim holds focus.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
        });

        var scrim = new MaterialScrim();
        scrim.DismissRequested += (_, _) => status.Text = "DismissRequested handled by the gallery.";

        var host = new Panel();
        host.Children.Add(content);
        host.Children.Add(scrim);
        surface.Child = host;

        var open = new Button { Content = "Open scrim" };
        open.Click += (_, _) =>
        {
            scrim.IsOpen = true;
            status.Text = "Scrim open.";
        };

        var dismissible = new ToggleSwitch { Content = "Dismissible", IsChecked = true };
        dismissible.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleSwitch.IsCheckedProperty)
                scrim.IsDismissEnabled = dismissible.IsChecked == true;
        };

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        actions.Children.Add(open);
        actions.Children.Add(dismissible);
        panel.Children.Add(actions);
        panel.Children.Add(surface);
        panel.Children.Add(status);
        return Scroll(panel);
    }

    // --------------------------------------------------------- focus indicator

    private static Control FocusIndicator()
    {
        var panel = Panel();
        panel.Children.Add(Heading("Focus indicator"));
        panel.Children.Add(Caption(
            "A 3 dp outside ring in the secondary role. FocusTarget mirrors the decorated " +
            "control's keyboard focus, so Tab moves the ring between the two buttons below."));

        var first = new Button { Content = "Tab to me", CornerRadius = new CornerRadius(20) };
        var firstIndicator = new MaterialFocusIndicator
        {
            FocusTarget = first,
            CornerRadius = new CornerRadius(20),
            Margin = new Thickness(-3),
        };
        var firstHost = new Grid { Width = 160, Height = 56 };
        firstHost.Children.Add(first);
        firstHost.Children.Add(firstIndicator);

        var second = new Button { Content = "Then me", CornerRadius = new CornerRadius(20) };
        var secondIndicator = new MaterialFocusIndicator
        {
            FocusTarget = second,
            CornerRadius = new CornerRadius(20),
            Margin = new Thickness(-3),
        };
        var secondHost = new Grid { Width = 160, Height = 56 };
        secondHost.Children.Add(second);
        secondHost.Children.Add(secondIndicator);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(firstHost);
        row.Children.Add(secondHost);
        panel.Children.Add(row);

        panel.Children.Add(Heading("Forced state"));
        panel.Children.Add(Caption(
            "IsActive shows the ring without focus, which is how a static preview or a " +
            "non-focusable custom surface demonstrates the indicator."));
        var forced = new Grid { Width = 120, Height = 120 };
        forced.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EADDFF")),
            CornerRadius = new CornerRadius(16),
        });
        forced.Children.Add(new MaterialFocusIndicator
        {
            IsActive = true,
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(-3),
        });
        panel.Children.Add(forced);
        return Scroll(panel);
    }

    // ------------------------------------------------------------ state layers

    private static Control StateLayers()
    {
        var panel = Panel();
        panel.Children.Add(Heading("State layers"));
        panel.Children.Add(Caption(
            "The five source states. Opacities come from the Material state-layer tokens: " +
            "hover 8%, focus 10%, pressed 10%, dragged 16% over a surface fill with elevation 5."));

        var states = new WrapPanel { ItemSpacing = 20, LineSpacing = 12 };
        foreach (var state in Enum.GetValues<MaterialStateLayerState>())
        {
            var layer = new MaterialStateLayer
            {
                State = state,
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(8),
            };
            var host = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FEF7FF")),
                CornerRadius = new CornerRadius(8),
                Child = layer,
            };
            var tile = new StackPanel { Spacing = 6 };
            tile.Children.Add(host);
            tile.Children.Add(new TextBlock { Text = state.ToString(), FontSize = 12 });
            states.Children.Add(tile);
        }
        panel.Children.Add(states);

        panel.Children.Add(Heading("Interactive layer"));
        panel.Children.Add(Caption(
            "IsInteractive tracks the pointer and keyboard itself: hover, then press, then focus " +
            "with Tab. IsDragging overrides everything with the dragged state."));

        var interactive = new MaterialStateLayer
        {
            IsInteractive = true,
            Focusable = true,
            Width = 160,
            Height = 48,
            CornerRadius = new CornerRadius(8),
        };
        var interactiveHost = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FEF7FF")),
            CornerRadius = new CornerRadius(8),
            Child = interactive,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var effective = new TextBlock { Text = "EffectiveState: Enabled", FontSize = 13 };
        interactive.PropertyChanged += (_, e) =>
        {
            if (e.Property == MaterialStateLayer.EffectiveStateProperty)
                effective.Text = "EffectiveState: " + interactive.EffectiveState;
        };

        var dragging = new ToggleSwitch { Content = "IsDragging" };
        dragging.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleSwitch.IsCheckedProperty)
                interactive.IsDragging = dragging.IsChecked == true;
        };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        controls.Children.Add(interactiveHost);
        controls.Children.Add(dragging);
        panel.Children.Add(controls);
        panel.Children.Add(effective);
        return Scroll(panel);
    }
}
