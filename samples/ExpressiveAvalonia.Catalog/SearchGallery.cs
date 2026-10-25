using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;

namespace ExpressiveAvalonia.Catalog;

/// <summary>
/// Gallery for the search family: the search bar anatomy and states, the flat and on-scroll
/// container treatments, the centered building block, the avatar slot, and the docked and
/// full-screen search views in both the expressive and baseline layouts with live suggestions,
/// keyboard navigation and dismissal.
/// </summary>
public static class SearchGallery
{
    private static readonly Suggestion[] Catalogue =
    [
        new("Search bar", "Pill field with leading and trailing actions"),
        new("Search view", "Docked and full-screen layouts with suggestions"),
        new("Navigation rail", "Vertical destinations with a selected indicator"),
        new("Navigation bar", "Three to five bottom destinations"),
        new("Bottom app bar", "Actions anchored to the bottom edge"),
        new("Range slider", "Two independently focusable endpoints"),
        new("Rich tooltip", "Actionable content hosted by a native flyout"),
        new("Circular progress", "Determinate and indeterminate indicators"),
        new("Segmented button", "Connected single-choice segments"),
        new("Text field", "Filled and outlined containers"),
        new("Menu", "Dropdown and context menus"),
        new("Chips", "Assist, filter, input and suggestion chips"),
    ];

    public static Control Create()
    {
        var panel = new StackPanel { Spacing = 24, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Search", FontSize = 24, LineHeight = 32 });
        panel.Children.Add(new TextBlock
        {
            Text = "Search bar states and container treatments, then the docked and full-screen "
                 + "search views with live filtering. Enter searches, Down moves into the "
                 + "suggestions, Up returns to the field, and Escape clears before it dismisses.",
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(BarSection());
        panel.Children.Add(Section("Docked search view",
            "Expressive: field card and suggestion card 2 DIP apart, with the voice action (Input text). "
            + "Baseline: one rounded container with a divider, hint only (Supporting text).",
            Row(ViewDemo(MaterialSearchViewMode.Docked, MaterialSearchViewVariant.Expressive,
                    MaterialSearchFieldConfiguration.InputText, autoFocus: true),
                ViewDemo(MaterialSearchViewMode.Docked, MaterialSearchViewVariant.Baseline,
                    MaterialSearchFieldConfiguration.SupportingText, autoFocus: false))));

        panel.Children.Add(Section("Full-screen search view",
            "Expressive: 12 inset field card over a scrim. Baseline: flat 72-high header and a full-width list.",
            Row(FullScreenDemo(MaterialSearchViewVariant.Expressive, MaterialSearchFieldConfiguration.InputText),
                FullScreenDemo(MaterialSearchViewVariant.Baseline, MaterialSearchFieldConfiguration.SupportingText))));

        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private static Control BarSection()
    {
        var status = new TextBlock { Text = "No interaction yet", Opacity = 0.75, TextWrapping = TextWrapping.Wrap };

        MaterialSearchBar Bar()
        {
            var bar = new MaterialSearchBar { Width = 360, PlaceholderText = "Search product" };
            bar.SearchRequested += (_, _) => status.Text = $"Search: \"{bar.Text}\"";
            bar.ClearRequested += (_, _) => status.Text = "Query cleared";
            bar.DismissRequested += (_, _) => status.Text = "Dismiss requested (Escape on an empty query)";
            bar.TrailingIconInvoked += (_, _) => status.Text = $"Search action: \"{bar.Text}\"";
            bar.SecondaryIconInvoked += (_, _) => status.Text = "Search tools action";
            return bar;
        }

        var avatar = Bar();
        avatar.ShowAvatar = true;
        avatar.AvatarInitial = "A";
        avatar.Text = "Input text";

        var centered = Bar();
        centered.Alignment = MaterialSearchBarAlignment.Centered;
        centered.ShowLeadingIcon = false;
        centered.ShowTrailingIcon = false;

        var flat = Bar();
        flat.ShowLeadingIcon = false;
        flat.ShowTrailingIcon = false;
        flat.Bind(TemplatedControl.BackgroundProperty, flat.GetResourceObservable("MaterialSurfaceContainerBrush"));

        var onScroll = Bar();
        onScroll.ShowLeadingIcon = false;
        onScroll.ShowTrailingIcon = false;
        onScroll.Bind(TemplatedControl.BackgroundProperty, onScroll.GetResourceObservable("MaterialSurfaceContainerHighestBrush"));

        var assist = Bar();
        assist.ShowSecondaryIcon = true;

        var disabled = Bar();
        disabled.Text = "Input text";
        disabled.IsEnabled = false;

        return Section("Search bar",
            "Enabled, avatar, centered, flat and on-scroll containers, second trailing action and disabled",
            Row(Bar(), avatar, centered, flat, onScroll, assist, disabled),
            status);
    }

    private static Control ViewDemo(MaterialSearchViewMode mode, MaterialSearchViewVariant variant,
        MaterialSearchFieldConfiguration configuration, bool autoFocus)
    {
        var status = new TextBlock { Text = "Idle", Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        var reopen = new Button { Content = "Open the search view", IsVisible = false };

        var items = new ObservableCollection<Suggestion>();
        var view = new MaterialSearchView
        {
            Mode = mode,
            Variant = variant,
            Configuration = configuration,
            AutoFocus = autoFocus,
            Suggestions = items,
            ItemTemplate = SuggestionTemplate(),
            PlaceholderText = "Search components",
            Width = 360,
        };

        void Filter()
        {
            var query = view.Text?.Trim() ?? string.Empty;
            var matches = query.Length == 0
                ? Catalogue.Take(4)
                : Catalogue.Where(item =>
                        item.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.Supporting.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(6);
            items.Clear();
            foreach (var match in matches)
                items.Add(match);
        }

        view.QueryChanged += (_, _) =>
        {
            Filter();
            status.Text = $"Query \"{view.Text}\" · {items.Count} suggestions";
        };
        view.SearchRequested += (_, _) => status.Text = $"Search requested: \"{view.Text}\"";
        view.ClearRequested += (_, _) => status.Text = "Cleared";
        view.VoiceInputRequested += (_, _) => status.Text = "Voice input requested";
        view.SuggestionActivated += (_, e) =>
            status.Text = $"Suggestion activated: {(e.Suggestion as Suggestion)?.Label}";
        view.Dismissed += (_, e) =>
        {
            status.Text = $"Dismissed: {e.Reason}";
            reopen.IsVisible = true;
        };
        reopen.Click += (_, _) =>
        {
            reopen.IsVisible = false;
            view.Open();
            status.Text = "Opened";
        };

        Filter();

        var host = new Border
        {
            Width = 400,
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(20),
            Child = new StackPanel
            {
                Spacing = 12,
                Children = { view, reopen, status },
            },
        };
        host.Bind(Border.BackgroundProperty, host.GetResourceObservable("MaterialSurfaceContainerBrush"));

        return host;
    }

    private static Control FullScreenDemo(MaterialSearchViewVariant variant,
        MaterialSearchFieldConfiguration configuration)
    {
        var status = new TextBlock { Text = "Idle", Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
        var reopen = new Button { Content = "Open the search view", IsVisible = false };
        var items = new ObservableCollection<Suggestion>(Catalogue.Take(5));
        var view = new MaterialSearchView
        {
            Mode = MaterialSearchViewMode.FullScreen,
            Variant = variant,
            Configuration = configuration,
            AutoFocus = false,
            Suggestions = items,
            ItemTemplate = SuggestionTemplate(),
            PlaceholderText = "Search components",
        };

        view.QueryChanged += (_, _) =>
        {
            var query = view.Text?.Trim() ?? string.Empty;
            var matches = query.Length == 0
                ? Catalogue.Take(5)
                : Catalogue.Where(item => item.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(8);
            items.Clear();
            foreach (var match in matches)
                items.Add(match);
            status.Text = $"Query \"{view.Text}\" · {items.Count} suggestions";
        };
        view.SearchRequested += (_, _) => status.Text = $"Search requested: \"{view.Text}\"";
        view.Dismissed += (_, e) =>
        {
            status.Text = $"Dismissed: {e.Reason}";
            reopen.IsVisible = true;
        };
        reopen.Click += (_, _) =>
        {
            reopen.IsVisible = false;
            view.Open();
            status.Text = "Opened";
        };

        var surface = new Border
        {
            Width = 420,
            Height = 480,
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Child = view,
        };
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("MaterialSurfaceBrush"));

        return new StackPanel { Spacing = 12, Children = { surface, reopen, status } };
    }

    private static IDataTemplate SuggestionTemplate() =>
        new FuncDataTemplate<Suggestion>((item, _) => item is null ? null : SuggestionRow(item));

    private static Control SuggestionRow(Suggestion item)
    {
        var initial = new TextBlock
        {
            Text = item.Initial,
            FontSize = 16,
            LineHeight = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var monogram = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Child = initial,
        };
        monogram.Bind(Border.BackgroundProperty, monogram.GetResourceObservable("MaterialPrimaryContainerBrush"));
        initial.Bind(TextBlock.ForegroundProperty, initial.GetResourceObservable("MaterialOnPrimaryContainerBrush"));

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 16,
            LineHeight = 24,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable("MaterialOnSurfaceBrush"));

        var supporting = new TextBlock
        {
            Text = item.Supporting,
            FontSize = 14,
            LineHeight = 20,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        supporting.Bind(TextBlock.ForegroundProperty, supporting.GetResourceObservable("MaterialOnSurfaceVariantBrush"));

        var content = new StackPanel { Spacing = 0, Children = { label, supporting } };
        var contentHost = new Border { Padding = new Thickness(16, 0, 0, 0), Child = content };
        Grid.SetColumn(contentHost, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { monogram, contentHost },
        };
    }

    private static Control Section(string title, string caption, params Control[] content)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(new TextBlock { Text = title, FontSize = 20, LineHeight = 28 });
        if (!string.IsNullOrEmpty(caption))
            section.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        foreach (var control in content)
            section.Children.Add(control);
        return section;
    }

    private static Control Row(params Control[] controls)
    {
        var wrap = new WrapPanel { ItemSpacing = 16, LineSpacing = 16 };
        foreach (var control in controls)
            wrap.Children.Add(control);
        return wrap;
    }

    private sealed record Suggestion(string Label, string Supporting)
    {
        public string Initial => Label.Length > 0 ? Label[..1] : "?";
    }
}
