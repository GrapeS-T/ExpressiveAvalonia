using System;
using System.Collections.Generic;
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
/// The examples family gallery: the fourteen exported screens, the adaptive layout grid, the
/// on-screen keyboard, the status bar and device frame, the content/slot/general-item building
/// blocks, and the color-role schematic.
/// </summary>
/// <remarks>
/// Each page exercises the control the way the source composes it: real breakpoints, real key
/// insertion into a text field, real theme-variant swatches. Nothing is a screenshot: the screens
/// are built from the same controls an application would use, so they resize, focus, scroll and
/// follow light/dark and reduced-motion changes.
/// </remarks>
public static class ExamplesGallery
{
    /// <summary>Builds the examples family gallery.</summary>
    public static Control Create()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Screens", Content = new DeferredGallery(() => ScreensPage()) });
        tabs.Items.Add(new TabItem { Header = "Layout grid", Content = new DeferredGallery(() => LayoutPage()) });
        tabs.Items.Add(new TabItem { Header = "Keyboard", Content = new DeferredGallery(() => KeyboardPage()) });
        tabs.Items.Add(new TabItem { Header = "Building blocks", Content = new DeferredGallery(() => BlocksPage()) });
        tabs.Items.Add(new TabItem { Header = "Device chrome", Content = new DeferredGallery(() => ChromePage()) });
        tabs.Items.Add(new TabItem { Header = "Color schematic", Content = new DeferredGallery(() => SchematicPage()) });
        return tabs;
    }

    private static StackPanel Page() => new() { Spacing = 16, Margin = new Thickness(24) };

    private static Control Section(string caption, string? description, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        var title = new TextBlock { Text = caption };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleMedium);
        stack.Children.Add(title);
        if (description is not null)
        {
            var text = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, MaxWidth = 760 };
            MaterialTypography.SetStyle(text, MaterialTypeStyle.BodyMedium);
            text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
            stack.Children.Add(text);
        }
        stack.Children.Add(content);
        return stack;
    }

    private static Control Scroll(Control content) => new ScrollViewer
    {
        Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
    };

    // ---- screens ------------------------------------------------------------------------

    private static Control ScreensPage()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Phone", Content = new DeferredGallery(() => PhoneScreens()) });
        tabs.Items.Add(new TabItem { Header = "Browser", Content = new DeferredGallery(() => WebScreens()) });
        return tabs;
    }

    private static Control PhoneScreens()
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Margin = new Thickness(24) };
        void Add(string name, Func<Control> build)
        {
            var column = new StackPanel { Spacing = 8 };
            var label = new TextBlock { Text = name };
            MaterialTypography.SetStyle(label, MaterialTypeStyle.LabelLarge);
            column.Children.Add(label);
            column.Children.Add(build());
            strip.Children.Add(column);
        }

        Add("Home", ExamplesScreens.HomeMobile);
        Add("Upcoming", ExamplesScreens.UpcomingMobile);
        Add("Detailed view", ExamplesScreens.DetailedMobile);
        Add("Reviews", ExamplesScreens.ReviewsMobile);
        Add("Library", ExamplesScreens.LibraryMobile);
        Add("Gallery", ExamplesScreens.GalleryMobile);
        Add("Messaging", ExamplesScreens.MessagingMobile);

        return new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static Control WebScreens()
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Margin = new Thickness(24) };
        void Add(string name, Func<Control> build)
        {
            var column = new StackPanel { Spacing = 8 };
            var label = new TextBlock { Text = name };
            MaterialTypography.SetStyle(label, MaterialTypeStyle.LabelLarge);
            column.Children.Add(label);
            var surface = new Border
            {
                Width = 905,
                Height = 680,
                ClipToBounds = true,
                Child = build()
            };
            surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("MaterialSurfaceBrush"));
            column.Children.Add(surface);
            strip.Children.Add(column);
        }

        Add("Home", ExamplesScreens.HomeWeb);
        Add("Upcoming", ExamplesScreens.UpcomingWeb);
        Add("Detailed view", ExamplesScreens.DetailedWeb);
        Add("Reviews", ExamplesScreens.ReviewsWeb);
        Add("Library", ExamplesScreens.LibraryWeb);
        Add("Gallery", ExamplesScreens.GalleryWeb);
        Add("Messaging", ExamplesScreens.MessagingWeb);

        return new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    // ---- layout grid --------------------------------------------------------------------

    private static Control LayoutPage()
    {
        var panel = Page();

        var sizeText = new TextBlock();
        MaterialTypography.SetStyle(sizeText, MaterialTypeStyle.TitleSmall);
        var regionText = new TextBlock();
        MaterialTypography.SetStyle(regionText, MaterialTypeStyle.BodyMedium);
        regionText.Bind(TextBlock.ForegroundProperty,
            regionText.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var live = new MaterialLayoutGrid
        {
            Height = 320,
            RequestedNavigation = MaterialNavigationRegion.NavigationRegion,
            AutoNavigation = true,
            NavigationContent = CompactNavigation(),
            ExpandedNavigationContent = ExpandedNavigation()
        };
        live.SizeClassChanged += (_, e) =>
        {
            sizeText.Text = $"{e.NewClass} ({WidthRange(e.NewClass)})";
            regionText.Text = $"Resolved navigation: {live.NavigationRegion}";
        };

        var widthSlider = new Slider { Minimum = 320, Maximum = 1680, Value = 900, Width = 520 };
        var host = new Border { Child = live, HorizontalAlignment = HorizontalAlignment.Left };
        host.Bind(Border.BoxShadowProperty, host.GetResourceObservable("MaterialElevationLevel1Shadow"));
        widthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty) host.Width = widthSlider.Value;
        };
        host.Width = widthSlider.Value;

        var navChoice = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var region in Enum.GetValues<MaterialNavigationRegion>())
        {
            var button = new Button { Content = region.ToString() };
            MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Tonal);
            MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
            button.Click += (_, _) =>
            {
                live.AutoNavigation = false;
                live.RequestedNavigation = region;
                regionText.Text = $"Forced navigation: {live.NavigationRegion}";
            };
            navChoice.Children.Add(button);
        }
        var auto = new Button { Content = "Adapt automatically" };
        MaterialAssist.SetButtonVariant(auto, MaterialButtonVariant.Filled);
        MaterialAssist.SetControlSize(auto, MaterialControlSize.Small);
        auto.Click += (_, _) =>
        {
            live.AutoNavigation = true;
            live.RequestedNavigation = MaterialNavigationRegion.NavigationRegion;
            regionText.Text = $"Resolved navigation: {live.NavigationRegion}";
        };
        navChoice.Children.Add(auto);

        var sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        sliderRow.Children.Add(new TextBlock { Text = "Window width", VerticalAlignment = VerticalAlignment.Center });
        sliderRow.Children.Add(widthSlider);

        panel.Children.Add(Section("Adaptive layout",
            "The grid measures its own width against the Material breakpoints 600, 840, 1200 and 1600 dp. " +
            "Below 840 dp the navigation region becomes a bottom bar; from 840 dp it is a 72 dp rail; at " +
            "extra-large widths an expanded column replaces the rail when one is supplied.",
            new StackPanel { Spacing = 8, Children = { sliderRow, navChoice, sizeText, regionText, host } }));

        panel.Children.Add(Section("All eleven source variants",
            "Each preview is a live layout grid pinned to the width of its source window-size class and to " +
            "its source navigation mode. The extra-large expanded variant only appears when the expanded " +
            "column is available.",
            VariantMatrix()));
        return Scroll(panel);
    }

    private static string WidthRange(MaterialWindowSizeClass sizeClass) => sizeClass switch
    {
        MaterialWindowSizeClass.Compact => "0-599 dp",
        MaterialWindowSizeClass.Medium => "600-839 dp",
        MaterialWindowSizeClass.Expanded => "840-1199 dp",
        MaterialWindowSizeClass.Large => "1200-1599 dp",
        _ => "1600+ dp"
    };

    private static Control CompactNavigation()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        foreach (var (label, icon) in new[] { ("Home", "stars"), ("Search", "search"), ("Library", "folder") })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(ExamplesScreens.Glyph(icon, 20));
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return panel;
    }

    private static Control ExpandedNavigation()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        var title = new TextBlock { Text = "Navigation drawer" };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleMedium);
        panel.Children.Add(title);
        foreach (var label in new[] { "Home", "Upcoming", "Library", "Gallery", "Messaging" })
        {
            var item = new TextBlock { Text = label, Margin = new Thickness(0, 6) };
            MaterialTypography.SetStyle(item, MaterialTypeStyle.LabelLarge);
            panel.Children.Add(item);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return panel;
    }

    private static Control VariantMatrix()
    {
        var wrap = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
        // Window-size class representative widths, exactly as the source names them.
        var classes = new (MaterialWindowSizeClass Class, double Width, string Name)[]
        {
            (MaterialWindowSizeClass.Compact, 360, "Compact 0-599dp"),
            (MaterialWindowSizeClass.Medium, 700, "Medium 600-839dp"),
            (MaterialWindowSizeClass.Expanded, 900, "Expanded 840-1199dp"),
            (MaterialWindowSizeClass.Large, 1300, "Large 1200-1599dp"),
            (MaterialWindowSizeClass.ExtraLarge, 1700, "Extra-large 1600+dp")
        };
        var regions = new (MaterialNavigationRegion Region, string Name)[]
        {
            (MaterialNavigationRegion.None, "None"),
            (MaterialNavigationRegion.NavigationRegion, "Navigation region"),
            (MaterialNavigationRegion.NavigationExpanded, "Navigation expanded")
        };

        foreach (var (sizeClass, width, name) in classes)
        {
            foreach (var (region, regionName) in regions)
            {
                // The source has no compact/medium expanded variant, and no large expanded variant.
                if (region == MaterialNavigationRegion.NavigationExpanded &&
                    sizeClass != MaterialWindowSizeClass.ExtraLarge)
                    continue;

                var grid = new MaterialLayoutGrid
                {
                    Width = width,
                    Height = 320,
                    AutoNavigation = false,
                    RequestedNavigation = region,
                    NavigationContent = CompactNavigation(),
                    ExpandedNavigationContent = ExpandedNavigation(),
                    Content = VariantContent()
                };
                var preview = new Viewbox
                {
                    Width = 300,
                    Height = 190,
                    Stretch = Stretch.Uniform,
                    Child = grid
                };
                var card = new StackPanel { Spacing = 6 };
                card.Children.Add(preview);
                var caption = new TextBlock { Text = $"{name} \u2022 {regionName}" };
                MaterialTypography.SetStyle(caption, MaterialTypeStyle.LabelSmall);
                card.Children.Add(caption);
                wrap.Children.Add(card);
            }
        }
        return wrap;
    }

    private static Control VariantContent()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
        var title = new TextBlock { Text = "Content region" };
        MaterialTypography.SetStyle(title, MaterialTypeStyle.TitleSmall);
        panel.Children.Add(title);
        for (var i = 0; i < 4; i++)
        {
            var bar = new Border { Height = 24, CornerRadius = new CornerRadius(6) };
            bar.Bind(Border.BackgroundProperty, bar.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
            panel.Children.Add(bar);
        }
        panel.Bind(Avalonia.Controls.Panel.BackgroundProperty, panel.GetResourceObservable("MaterialSurfaceBrush"));
        return panel;
    }

    // ---- keyboard -----------------------------------------------------------------------

    private static Control KeyboardPage()
    {
        var panel = Page();

        var input = new TextBox
        {
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            PlaceholderText = "Type with the keyboard below or your hardware keys"
        };
        MaterialTextField.SetLabel(input, "Message");
        MaterialTextField.SetSupportingText(input, "Insertion, selection replacement and backspace all use the TextBox caret");

        var status = new TextBlock { Text = "Ready." };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);
        status.Bind(TextBlock.ForegroundProperty, status.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);

        var live = new MaterialKeyboard
        {
            Width = 412,
            HorizontalAlignment = HorizontalAlignment.Left,
            InputTarget = input,
            Configuration = MaterialKeyboardConfiguration.Base,
            Presentation = MaterialKeyboardStyle.Portrait
        };
        live.TextEntered += (_, e) => status.Text = $"Inserted {Describe(e.Text)}";
        live.ChromeInvoked += (_, e) => status.Text = $"Chrome action: {e.Action}";
        live.ReturnPressed += (_, _) => status.Text = "Return pressed (target does not accept newlines)";
        live.Dismissed += (_, _) => status.Text = "Keyboard dismissed; press Show to reopen";

        var controls = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        void Add(string label, Action action)
        {
            var button = new Button { Content = label };
            MaterialAssist.SetButtonVariant(button, MaterialButtonVariant.Tonal);
            MaterialAssist.SetControlSize(button, MaterialControlSize.Small);
            button.Click += (_, _) => action();
            controls.Children.Add(button);
        }
        Add("Letters", () => live.Layer = MaterialKeyboardLayer.Letters);
        Add("Symbols", () => live.Layer = MaterialKeyboardLayer.Symbols);
        Add("Emoji", () => live.Layer = MaterialKeyboardLayer.Emoji);
        Add("Toggle shift", () => live.IsShifted = !live.IsShifted);
        Add("Caps lock", () =>
        {
            live.IsCapsLocked = !live.IsCapsLocked;
            live.IsShifted = live.IsCapsLocked;
        });
        Add("Press Q", () => live.PressKey("Q"));
        Add("Press space", () => live.PressKey("space"));
        Add("Hide", live.Dismiss);
        Add("Show", live.Show);

        var stage = new StackPanel { Spacing = 8 };
        stage.Children.Add(input);
        stage.Children.Add(controls);
        stage.Children.Add(status);
        stage.Children.Add(live);

        panel.Children.Add(Section("Live keyboard",
            "Keys are real buttons: pointer, Space and Enter activation, focus visuals and automation come from " +
            "Avalonia. Shift double-tap locks caps, arrow keys move focus between keys, Backspace repeats while " +
            "held, and the emoji layer inserts surrogate pairs. Chrome keys report through ChromeInvoked because " +
            "GIF, settings, translation and voice input belong to the host system.",
            stage));

        panel.Children.Add(Section("All twelve source variants",
            "Four configurations by three presentations. Portrait rows are 46 dp with a gesture bar, landscape " +
            "rows are 34 dp, and the floating card is draggable by its chrome bar and dismisses on Escape.",
            KeyboardMatrix()));
        return Scroll(panel);
    }

    private static string Describe(string text) => text switch
    {
        " " => "a space",
        "\n" or "\r\n" => "a newline",
        _ => $"\"{text}\""
    };

    private static Control KeyboardMatrix()
    {
        var tabs = new TabControl();
        foreach (var configuration in Enum.GetValues<MaterialKeyboardConfiguration>())
        {
            tabs.Items.Add(new TabItem { Header = configuration.ToString(), Content = new DeferredGallery(() =>
            {
            var strip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 24,
                Margin = new Thickness(16)
            };
            foreach (var presentation in Enum.GetValues<MaterialKeyboardStyle>())
            {
                var keyboard = new MaterialKeyboard
                {
                    Configuration = configuration,
                    Presentation = presentation,
                    Width = presentation switch
                    {
                        MaterialKeyboardStyle.Landscape => 868,
                        MaterialKeyboardStyle.Floating => 342,
                        _ => 412
                    }
                };
                var column = new StackPanel { Spacing = 8 };
                var label = new TextBlock { Text = presentation.ToString() };
                MaterialTypography.SetStyle(label, MaterialTypeStyle.LabelLarge);
                column.Children.Add(label);
                var preview = new Viewbox
                {
                    Width = 380,
                    Height = 300,
                    Stretch = Stretch.Uniform,
                    Child = keyboard
                };
                column.Children.Add(preview);
                strip.Children.Add(column);
            }
            var scroll = new ScrollViewer
            {
                Content = strip,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
                return scroll;
            }) });
        }
        return tabs;
    }

    // ---- building blocks ----------------------------------------------------------------

    private static Control BlocksPage()
    {
        var panel = Page();

        var contentBlock = new MaterialContentBlock
        {
            Overline = "Overline",
            Label = "Label text",
            SupportingText = "Supporting line text, lorem ipsum dolor",
            Width = 400,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(Section("Building blocks/Content (local-000221)",
            "Overline, label and supporting text. The source toggles each line independently.",
            new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    contentBlock,
                    Toggle("Show overline", true, value => contentBlock.ShowOverline = value),
                    Toggle("Show supporting text", true, value => contentBlock.ShowSupportingText = value)
                }
            }));

        var compactBlock = new MaterialContentBlock
        {
            Label = "Label",
            SupportingText = "Supporting text",
            ShowOverline = false,
            ShowSupportingText = false,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(Section(".Building Blocks/Content (local-000218)",
            "Label and supporting text without an overline; the supporting line starts hidden, matching the source default.",
            new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    compactBlock,
                    Toggle("Show supporting text", false, value => compactBlock.ShowSupportingText = value)
                }
            }));

        var emptySlot = new MaterialSlotHost
        {
            Width = 360,
            Height = 240,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var filledSlot = new MaterialSlotHost
        {
            Width = 360,
            Height = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new Border
            {
                CornerRadius = new CornerRadius(12),
                Child = ExamplesScreens.Artwork()
            }
        };
        var slotToggle = new Button { Content = "Fill the empty slot" };
        MaterialAssist.SetButtonVariant(slotToggle, MaterialButtonVariant.Tonal);
        MaterialAssist.SetControlSize(slotToggle, MaterialControlSize.Small);
        slotToggle.Click += (_, _) =>
        {
            if (emptySlot.Content is null)
            {
                emptySlot.Content = ExamplesScreens.Artwork();
                slotToggle.Content = "Empty the slot again";
            }
            else
            {
                emptySlot.Content = null;
                slotToggle.Content = "Fill the empty slot";
            }
        };
        panel.Children.Add(Section("Shared slot component (local-004633)",
            "The dashed safety zone, swap glyph and replacement instructions are the component's empty state. " +
            "Assign Content and the prompt disappears; the slot keeps whatever control an application supplies.",
            new StackPanel
            {
                Spacing = 12,
                Orientation = Orientation.Horizontal,
                Children = { emptySlot, filledSlot, slotToggle }
            }));

        var general = ExamplesScreens.Tile("Label", 316, 204, "Supporting Text");
        var plain = ExamplesScreens.Tile("Label", 316, 204);
        plain.ShowCarouselText = false;
        var selected = ExamplesScreens.Tile("Label", 316, 204, "Supporting Text", selected: true);
        var invokeStatus = new TextBlock { Text = "Activate a tile with the pointer, Enter or Space." };
        MaterialTypography.SetStyle(invokeStatus, MaterialTypeStyle.BodySmall);
        general.Invoked += (_, _) => invokeStatus.Text = "Tile invoked.";
        selected.Invoked += (_, _) => invokeStatus.Text = "Selected tile invoked.";
        panel.Children.Add(Section("Building blocks/General item (local-000261)",
            "Artwork with an optional scrim caption. The tile is focusable and reports Invoked on pointer " +
            "release, Enter and Space; the selected variant adds the primary outline.",
            new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children = { general, plain, selected }
                    },
                    invokeStatus
                }
            }));
        return Scroll(panel);
    }

    private static Control Toggle(string label, bool initial, Action<bool> onChanged)
    {
        var box = new CheckBox { Content = label, IsChecked = initial };
        box.IsCheckedChanged += (_, _) => onChanged(box.IsChecked == true);
        return box;
    }

    // ---- chrome -------------------------------------------------------------------------

    private static Control ChromePage()
    {
        var panel = Page();

        var live = new MaterialStatusBar { ShowCameraCutout = true, Width = 412, HorizontalAlignment = HorizontalAlignment.Left };
        var fixedTime = new MaterialStatusBar
        {
            ShowCameraCutout = true,
            TimeText = "9:41",
            ShowClock = false,
            Width = 412,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var noCutout = new MaterialStatusBar
        {
            ShowCameraCutout = false,
            TimeText = "12:00",
            ShowClock = false,
            Width = 412,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(Section("Status bar (local-000697)",
            "The clock ticks while the bar is on screen and stops when it is detached. The camera cutout is the " +
            "source boolean property; the wifi, signal and battery glyphs are the exported vectors.",
            new StackPanel { Spacing = 12, Children = { live, fixedTime, noCutout } }));

        var full = new MaterialDeviceFrame
        {
            Width = 260,
            Height = 420,
            TopContent = new TextBlock { Text = "Pinned top content", Margin = new Thickness(16, 12) },
            BottomContent = new TextBlock { Text = "Pinned bottom content", Margin = new Thickness(16, 12) },
            Content = Filler()
        };
        var noStatus = new MaterialDeviceFrame
        {
            Width = 260,
            Height = 420,
            ShowStatusBar = false,
            Content = Filler()
        };
        var noGesture = new MaterialDeviceFrame
        {
            Width = 260,
            Height = 420,
            ShowGestureBar = false,
            Content = Filler()
        };
        var fixedHeight = new MaterialDeviceFrame
        {
            Width = 260,
            Height = 420,
            IsScrollable = false,
            Content = Filler()
        };
        panel.Children.Add(Section("Device frame (local-001331)",
            "An 8 dp outline around a 28 dp screen. TopContent and BottomContent stay pinned while Content " +
            "scrolls, which is how the source screens keep their app bar and navigation bar in place. Turn " +
            "scrolling off for surfaces that must lay out at their natural height.",
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 24,
                Children = { full, noStatus, noGesture, fixedHeight }
            }));
        return Scroll(panel);
    }

    private static Control Filler()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        for (var i = 0; i < 12; i++)
        {
            var row = new Border { Height = 40, CornerRadius = new CornerRadius(8) };
            row.Bind(Border.BackgroundProperty, row.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));
            panel.Children.Add(row);
        }
        return panel;
    }

    // ---- schematic ----------------------------------------------------------------------

    private static Control SchematicPage()
    {
        var panel = Page();
        var status = new TextBlock { Text = "Select a swatch to copy its hex value." };
        MaterialTypography.SetStyle(status, MaterialTypeStyle.BodyMedium);
        status.Bind(TextBlock.ForegroundProperty, status.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var schematic = new MaterialSchematicGroup
        {
            ShowDarkPreview = true,
            ShowFixedColors = true,
            SwatchWidth = 180
        };
        schematic.SwatchCopied += (_, e) =>
            status.Text = $"Copied {e.Role} ({e.Token}) as {e.Hex}";
        panel.Children.Add(Section("Color-role schematic (local-004522)",
            "Every Material role with its source token label, in the current scheme and in a dark preview. " +
            "Swatch colors are resource bindings, so a seed or contrast change repaints them in place.",
            new StackPanel { Spacing = 12, Children = { status, schematic } }));
        return Scroll(panel);
    }
}
