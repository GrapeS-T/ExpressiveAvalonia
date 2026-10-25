using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExpressiveAvalonia.Controls;

/// <summary>The Material app bar arrangements from the design system.</summary>
public enum MaterialAppBarConfiguration
{
    /// <summary>64-unit bar with a left-aligned title between the leading and trailing slots.</summary>
    Small,
    /// <summary>64-unit bar with a centred title, typically paired with a leading menu icon.</summary>
    SmallCentered,
    /// <summary>64-unit bar whose middle slot is an image thumbnail instead of a title.</summary>
    SmallImage,
    /// <summary>64-unit bar whose middle slot is a search field.</summary>
    Search,
    /// <summary>112-unit bar: leading/trailing row above a 28-unit headline.</summary>
    Medium,
    /// <summary>120-unit bar: leading/trailing row above a 36-unit headline.</summary>
    Large
}

/// <summary>How the app bar container reacts to scrolled content.</summary>
public enum MaterialAppBarElevation
{
    /// <summary>Container-less bar drawn directly on the surface.</summary>
    Flat,
    /// <summary>Surface container background; a level 2 shadow is added once content scrolls under it.</summary>
    OnScroll
}

/// <summary>
/// A Material app bar with small, medium, large, image and search arrangements, leading and
/// trailing slots, and an overflow menu for actions that do not fit.
/// </summary>
/// <remarks>
/// The bar lays out a 48-unit leading target, a flexible middle slot and a trailing row that
/// contains the inline actions, optional trailing content, the avatar and the overflow button.
/// Medium and large arrangements move the title into a second row below the leading/trailing row.
/// Set <see cref="ScrollSource"/> to a <see cref="ScrollViewer"/> so that
/// <see cref="MaterialAppBarElevation.OnScroll"/> raises <see cref="IsScrolled"/> (and the
/// container shadow) as soon as the content scrolls under the bar.
/// </remarks>
public class MaterialAppBar : MaterialBarBase
{
    public static readonly StyledProperty<MaterialAppBarConfiguration> ConfigurationProperty =
        AvaloniaProperty.Register<MaterialAppBar, MaterialAppBarConfiguration>(
            nameof(Configuration), MaterialAppBarConfiguration.Small);

    public static readonly StyledProperty<MaterialAppBarElevation> ElevationProperty =
        AvaloniaProperty.Register<MaterialAppBar, MaterialAppBarElevation>(
            nameof(Elevation), MaterialAppBarElevation.Flat);

    /// <summary>Headline. A string is rendered as text; any control is hosted as-is.</summary>
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Title));

    /// <summary>Supporting text below the headline, shown when <see cref="IsSubtitleVisible"/>.</summary>
    public static readonly StyledProperty<object?> SubtitleProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Subtitle));

    public static readonly StyledProperty<bool> IsSubtitleVisibleProperty =
        AvaloniaProperty.Register<MaterialAppBar, bool>(nameof(IsSubtitleVisible), true);

    /// <summary>Text alignment of the headline and supporting text.</summary>
    public static readonly StyledProperty<TextAlignment> TitleAlignmentProperty =
        AvaloniaProperty.Register<MaterialAppBar, TextAlignment>(nameof(TitleAlignment), TextAlignment.Left);

    /// <summary>Arbitrary content for the 48-unit leading slot; replaces <see cref="LeadingIcon"/>.</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(LeadingContent));

    /// <summary>Convenience icon for a leading icon button that invokes <see cref="LeadingCommand"/>.</summary>
    public static readonly StyledProperty<Geometry?> LeadingIconProperty =
        AvaloniaProperty.Register<MaterialAppBar, Geometry?>(nameof(LeadingIcon));

    public static readonly StyledProperty<ICommand?> LeadingCommandProperty =
        AvaloniaProperty.Register<MaterialAppBar, ICommand?>(nameof(LeadingCommand));

    /// <summary>Accessible name and tooltip of the leading icon button.</summary>
    public static readonly StyledProperty<string?> LeadingTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(nameof(LeadingText));

    /// <summary>Arbitrary content placed after the inline actions, before the avatar.</summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(TrailingContent));

    /// <summary>Image shown in the middle slot by <see cref="MaterialAppBarConfiguration.SmallImage"/>.</summary>
    public static readonly StyledProperty<object?> ThumbnailProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(Thumbnail));

    /// <summary>Image or control rendered as a 32-unit avatar in the trailing row.</summary>
    public static readonly StyledProperty<object?> AvatarContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(AvatarContent));

    /// <summary>Monogram text rendered as a 32-unit avatar on the primary color.</summary>
    public static readonly StyledProperty<string?> AvatarTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(nameof(AvatarText));

    /// <summary>Replaces the built-in search field in <see cref="MaterialAppBarConfiguration.Search"/>.</summary>
    public static readonly StyledProperty<object?> SearchContentProperty =
        AvaloniaProperty.Register<MaterialAppBar, object?>(nameof(SearchContent));

    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<MaterialAppBar, string>(nameof(SearchPlaceholder), "Search");

    /// <summary>Text of the built-in search field.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<MaterialAppBar, string?>(
            nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Scroll viewer that drives <see cref="IsScrolled"/> for elevation on scroll.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollSourceProperty =
        AvaloniaProperty.Register<MaterialAppBar, ScrollViewer?>(nameof(ScrollSource));

    public static readonly DirectProperty<MaterialAppBar, bool> IsScrolledProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsScrolled), bar => bar.IsScrolled, (bar, value) => bar.IsScrolled = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsSmallTitleLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsSmallTitleLayout), bar => bar.IsSmallTitleLayout, (bar, value) => bar.IsSmallTitleLayout = value);

    /// <summary>True when the middle slot shows the inline title instead of a thumbnail.</summary>
    public static readonly DirectProperty<MaterialAppBar, bool> HasSmallTitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasSmallTitle), bar => bar.HasSmallTitle, (bar, value) => bar.HasSmallTitle = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsExtendedLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsExtendedLayout), bar => bar.IsExtendedLayout, (bar, value) => bar.IsExtendedLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> IsSearchLayoutProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(IsSearchLayout), bar => bar.IsSearchLayout, (bar, value) => bar.IsSearchLayout = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasSubtitleProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasSubtitle), bar => bar.HasSubtitle, (bar, value) => bar.HasSubtitle = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasThumbnailProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasThumbnail), bar => bar.HasThumbnail, (bar, value) => bar.HasThumbnail = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasAvatarProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasAvatar), bar => bar.HasAvatar, (bar, value) => bar.HasAvatar = value);

    public static readonly DirectProperty<MaterialAppBar, bool> HasLeadingIconProperty =
        AvaloniaProperty.RegisterDirect<MaterialAppBar, bool>(
            nameof(HasLeadingIcon), bar => bar.HasLeadingIcon, (bar, value) => bar.HasLeadingIcon = value);

    private bool _isScrolled;
    private bool _isSmallTitleLayout = true;
    private bool _hasSmallTitle = true;
    private bool _isExtendedLayout;
    private bool _isSearchLayout;
    private bool _hasSubtitle;
    private bool _hasThumbnail;
    private bool _hasAvatar;
    private bool _hasLeadingIcon;
    private Panel? _searchHost;
    private Border? _searchField;
    private TextBox? _searchBox;
    private IDisposable? _searchBackgroundBinding;

    static MaterialAppBar()
    {
        ConfigurationProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        SubtitleProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        IsSubtitleVisibleProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        ThumbnailProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        AvatarContentProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        AvatarTextProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        LeadingIconProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        LeadingContentProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateLayoutFlags());
        ScrollSourceProperty.Changed.AddClassHandler<MaterialAppBar>((bar, args) =>
            bar.OnScrollSourceChanged(args.GetOldValue<ScrollViewer?>(), args.GetNewValue<ScrollViewer?>()));
        ElevationProperty.Changed.AddClassHandler<MaterialAppBar>((bar, _) => bar.UpdateSearchBackground());
    }

    /// <summary>Raised when Enter is pressed in the built-in search field.</summary>
    public event EventHandler? SearchSubmitted;

    /// <summary>Raised when Escape is pressed in an empty built-in search field.</summary>
    public event EventHandler? SearchDismissed;

    public MaterialAppBar() => UpdateLayoutFlags();

    public MaterialAppBarConfiguration Configuration
    {
        get => GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public MaterialAppBarElevation Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool IsSubtitleVisible
    {
        get => GetValue(IsSubtitleVisibleProperty);
        set => SetValue(IsSubtitleVisibleProperty, value);
    }

    public TextAlignment TitleAlignment
    {
        get => GetValue(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public Geometry? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    public ICommand? LeadingCommand
    {
        get => GetValue(LeadingCommandProperty);
        set => SetValue(LeadingCommandProperty, value);
    }

    public string? LeadingText
    {
        get => GetValue(LeadingTextProperty);
        set => SetValue(LeadingTextProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public object? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    public object? AvatarContent
    {
        get => GetValue(AvatarContentProperty);
        set => SetValue(AvatarContentProperty, value);
    }

    public string? AvatarText
    {
        get => GetValue(AvatarTextProperty);
        set => SetValue(AvatarTextProperty, value);
    }

    public object? SearchContent
    {
        get => GetValue(SearchContentProperty);
        set => SetValue(SearchContentProperty, value);
    }

    public string SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public ScrollViewer? ScrollSource
    {
        get => GetValue(ScrollSourceProperty);
        set => SetValue(ScrollSourceProperty, value);
    }

    /// <summary>
    /// Whether the attached scroll viewer has scrolled away from its origin. When
    /// <see cref="ScrollSource"/> is set this is maintained by the bar; it can also be driven
    /// directly by a host that implements its own scroll tracking.
    /// </summary>
    public bool IsScrolled
    {
        get => _isScrolled;
        set
        {
            if (SetAndRaise(IsScrolledProperty, ref _isScrolled, value))
                PseudoClasses.Set(":scrolled", value);
        }
    }

    /// <summary>True for the single-row small arrangements (small, small-centred).</summary>
    public bool IsSmallTitleLayout
    {
        get => _isSmallTitleLayout;
        private set => SetAndRaise(IsSmallTitleLayoutProperty, ref _isSmallTitleLayout, value);
    }

    /// <summary>True when the middle slot shows the inline title instead of a thumbnail.</summary>
    public bool HasSmallTitle
    {
        get => _hasSmallTitle;
        private set => SetAndRaise(HasSmallTitleProperty, ref _hasSmallTitle, value);
    }

    /// <summary>True for the two-row medium and large arrangements.</summary>
    public bool IsExtendedLayout
    {
        get => _isExtendedLayout;
        private set => SetAndRaise(IsExtendedLayoutProperty, ref _isExtendedLayout, value);
    }

    public bool IsSearchLayout
    {
        get => _isSearchLayout;
        private set => SetAndRaise(IsSearchLayoutProperty, ref _isSearchLayout, value);
    }

    public bool HasSubtitle
    {
        get => _hasSubtitle;
        private set => SetAndRaise(HasSubtitleProperty, ref _hasSubtitle, value);
    }

    public bool HasThumbnail
    {
        get => _hasThumbnail;
        private set => SetAndRaise(HasThumbnailProperty, ref _hasThumbnail, value);
    }

    public bool HasAvatar
    {
        get => _hasAvatar;
        private set => SetAndRaise(HasAvatarProperty, ref _hasAvatar, value);
    }

    public bool HasLeadingIcon
    {
        get => _hasLeadingIcon;
        private set => SetAndRaise(HasLeadingIconProperty, ref _hasLeadingIcon, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _searchHost = e.NameScope.Find<Panel>("PART_DefaultSearchHost");
        if (_searchHost is not null)
        {
            var field = EnsureSearchField();
            if (!_searchHost.Children.Contains(field))
            {
                _searchHost.Children.Clear();
                _searchHost.Children.Add(field);
            }
            UpdateSearchBackground();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The subscription is dropped on detach, so restore it (idempotently) on re-attach.
        if (ScrollSource is { } scroll)
        {
            scroll.ScrollChanged -= OnScrollChanged;
            scroll.ScrollChanged += OnScrollChanged;
            UpdateScrolled(scroll.Offset);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (ScrollSource is { } scroll) scroll.ScrollChanged -= OnScrollChanged;
    }

    private void UpdateLayoutFlags()
    {
        var configuration = Configuration;
        IsSmallTitleLayout = configuration is MaterialAppBarConfiguration.Small or MaterialAppBarConfiguration.SmallCentered;
        IsExtendedLayout = configuration is MaterialAppBarConfiguration.Medium or MaterialAppBarConfiguration.Large;
        IsSearchLayout = configuration == MaterialAppBarConfiguration.Search;
        HasThumbnail = configuration == MaterialAppBarConfiguration.SmallImage && Thumbnail is not null;
        HasSmallTitle = IsSmallTitleLayout && !HasThumbnail;
        HasSubtitle = IsSubtitleVisible && Subtitle is not null;
        HasAvatar = AvatarContent is not null || !string.IsNullOrEmpty(AvatarText);
        HasLeadingIcon = LeadingIcon is not null && LeadingContent is null;
    }

    private void OnScrollSourceChanged(ScrollViewer? oldValue, ScrollViewer? newValue)
    {
        if (oldValue is not null)
            oldValue.ScrollChanged -= OnScrollChanged;
        if (newValue is not null)
        {
            newValue.ScrollChanged += OnScrollChanged;
            UpdateScrolled(newValue.Offset);
        }
        else
        {
            UpdateScrolled(default);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scroll)
            UpdateScrolled(scroll.Offset);
    }

    private void UpdateScrolled(Vector offset) => IsScrolled = offset.X > 0.5 || offset.Y > 0.5;

    private Control EnsureSearchField()
    {
        if (_searchField is not null)
            return _searchField;

        _searchBox = new TextBox
        {
            MinWidth = 0,
            MinHeight = 56,
            Padding = new Thickness(20, 16),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.Bind(TextBox.TextProperty, new Binding(nameof(SearchText))
        {
            Source = this,
            Mode = BindingMode.TwoWay
        });
        _searchBox.Bind(TextBox.PlaceholderTextProperty, new Binding(nameof(SearchPlaceholder)) { Source = this });
        _searchBox.Bind(AutomationProperties.NameProperty, new Binding(nameof(SearchPlaceholder)) { Source = this });
        _searchBox.KeyDown += OnSearchKeyDown;

        _searchField = new Border
        {
            Height = 56,
            CornerRadius = new CornerRadius(28),
            ClipToBounds = true,
            Child = _searchBox
        };
        return _searchField;
    }

    private void UpdateSearchBackground()
    {
        if (_searchField is null)
            return;
        // Flat search sits on the surface container; the elevated variant uses the next
        // container tone, matching the design's two fills.
        var key = Elevation == MaterialAppBarElevation.OnScroll
            ? "MaterialSurfaceContainerHighBrush"
            : "MaterialSurfaceContainerBrush";
        _searchBackgroundBinding?.Dispose();
        _searchBackgroundBinding = _searchField.Bind(Border.BackgroundProperty, _searchField.GetResourceObservable(key));
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchSubmitted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchText))
                SearchText = string.Empty;
            else
                SearchDismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
