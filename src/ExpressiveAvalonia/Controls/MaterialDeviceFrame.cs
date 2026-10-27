using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// The mobile device surface used by the example screens: a rounded screen, an optional status
/// bar, a scrollable content region and an optional gesture bar.
/// </summary>
/// <remarks>
/// <para>
/// This is a desktop composition for catalog examples, not an official Material composable. It
/// imitates device chrome; it is not the operating system's status bar and does not read real
/// system state.
/// </para>
/// <para>
/// Source anatomy (Device frame): an 8 DIP outline around a 28 DIP radius screen, a 52 DIP status
/// bar, the screen content, and a 24 DIP gesture bar whose 108x4 handle is centred. Content scrolls
/// inside the screen instead of growing the frame, which is what the source screens do. Turn
/// <see cref="IsScrollable"/> off for surfaces that must lay out at their natural height.
/// </para>
/// </remarks>
public sealed class MaterialDeviceFrame : ContentControl
{
    /// <summary>Shows the status bar strip.</summary>
    public static readonly StyledProperty<bool> ShowStatusBarProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, bool>(nameof(ShowStatusBar), true);

    /// <summary>Shows the bottom gesture bar.</summary>
    public static readonly StyledProperty<bool> ShowGestureBarProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, bool>(nameof(ShowGestureBar), true);

    /// <summary>Forwarded to the status bar camera cutout.</summary>
    public static readonly StyledProperty<bool> ShowCameraCutoutProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, bool>(nameof(ShowCameraCutout), true);

    /// <summary>Forwarded clock override; null shows the live wall clock.</summary>
    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, string?>(nameof(TimeText));

    /// <summary>Outer outline thickness; the source uses 8 DIP.</summary>
    public static readonly StyledProperty<double> FrameThicknessProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, double>(nameof(FrameThickness), 8,
            validate: static value => double.IsFinite(value) && value >= 0);

    /// <summary>Screen corner radius; the source uses 28 DIP.</summary>
    public static readonly StyledProperty<CornerRadius> ScreenCornerRadiusProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, CornerRadius>(nameof(ScreenCornerRadius),
            new CornerRadius(28));

    /// <summary>Wraps content in a vertical ScrollViewer when true (the source screen behaviour).</summary>
    public static readonly StyledProperty<bool> IsScrollableProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, bool>(nameof(IsScrollable), true);

    /// <summary>Pinned content between the status bar and the scrolling body, normally an app bar.</summary>
    public static readonly StyledProperty<object?> TopContentProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, object?>(nameof(TopContent));

    /// <summary>Template for <see cref="TopContent"/>.</summary>
    public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> TopContentTemplateProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, Avalonia.Controls.Templates.IDataTemplate?>(nameof(TopContentTemplate));

    /// <summary>Pinned content above the gesture bar, normally a navigation bar.</summary>
    public static readonly StyledProperty<object?> BottomContentProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, object?>(nameof(BottomContent));

    /// <summary>Template for <see cref="BottomContent"/>.</summary>
    public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> BottomContentTemplateProperty =
        AvaloniaProperty.Register<MaterialDeviceFrame, Avalonia.Controls.Templates.IDataTemplate?>(nameof(BottomContentTemplate));

    private Grid? _screenHost;
    private ScrollViewer? _scroller;
    private ContentPresenter? _presenter;
    private MaterialStatusBar? _statusBar;
    private Control? _gestureBar;
    private Border? _screen;
    private ContentPresenter? _topHost;
    private ContentPresenter? _bottomHost;

    static MaterialDeviceFrame()
    {
        ShowStatusBarProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateChrome());
        ShowGestureBarProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateChrome());
        ShowCameraCutoutProperty.Changed.AddClassHandler<MaterialDeviceFrame>(static (frame, _) =>
        {
            if (frame._statusBar is not null) frame._statusBar.ShowCameraCutout = frame.ShowCameraCutout;
        });
        TimeTextProperty.Changed.AddClassHandler<MaterialDeviceFrame>(static (frame, _) =>
        {
            if (frame._statusBar is not null) frame._statusBar.TimeText = frame.TimeText;
        });
        FrameThicknessProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateScreenClip());
        ScreenCornerRadiusProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateScreenClip());
        IsScrollableProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateContentHost());
        TopContentProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateChrome());
        BottomContentProperty.Changed.AddClassHandler<MaterialDeviceFrame>(
            static (frame, _) => frame.UpdateChrome());
    }

    public MaterialDeviceFrame()
    {
        Template = new FuncControlTemplate<MaterialDeviceFrame>((frame, _) => frame.Build());
        AutomationProperties.SetName(this, "Device frame");
    }

    public bool ShowStatusBar { get => GetValue(ShowStatusBarProperty); set => SetValue(ShowStatusBarProperty, value); }
    public bool ShowGestureBar { get => GetValue(ShowGestureBarProperty); set => SetValue(ShowGestureBarProperty, value); }
    public bool ShowCameraCutout { get => GetValue(ShowCameraCutoutProperty); set => SetValue(ShowCameraCutoutProperty, value); }
    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }
    public double FrameThickness { get => GetValue(FrameThicknessProperty); set => SetValue(FrameThicknessProperty, value); }
    public CornerRadius ScreenCornerRadius { get => GetValue(ScreenCornerRadiusProperty); set => SetValue(ScreenCornerRadiusProperty, value); }
    public bool IsScrollable { get => GetValue(IsScrollableProperty); set => SetValue(IsScrollableProperty, value); }
    public object? TopContent { get => GetValue(TopContentProperty); set => SetValue(TopContentProperty, value); }
    public Avalonia.Controls.Templates.IDataTemplate? TopContentTemplate { get => GetValue(TopContentTemplateProperty); set => SetValue(TopContentTemplateProperty, value); }
    public object? BottomContent { get => GetValue(BottomContentProperty); set => SetValue(BottomContentProperty, value); }
    public Avalonia.Controls.Templates.IDataTemplate? BottomContentTemplate { get => GetValue(BottomContentTemplateProperty); set => SetValue(BottomContentTemplateProperty, value); }

    /// <summary>The status bar instance, available after the template is applied.</summary>
    public MaterialStatusBar? StatusBar => _statusBar;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateScreenClip();
    }

    private Control Build()
    {
        _statusBar = new MaterialStatusBar
        {
            ShowCameraCutout = ShowCameraCutout,
            TimeText = TimeText
        };

        _presenter = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        _presenter[!ContentPresenter.ContentProperty] = this[!ContentProperty];
        _presenter[!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty];

        _screenHost = new Grid();
        UpdateContentHost();

        _gestureBar = BuildGestureBar();

        var topHost = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _topHost = topHost;
        topHost[!ContentPresenter.ContentProperty] = this[!TopContentProperty];
        topHost[!ContentPresenter.ContentTemplateProperty] = this[!TopContentTemplateProperty];

        var bottomHost = new ContentPresenter
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _bottomHost = bottomHost;
        bottomHost[!ContentPresenter.ContentProperty] = this[!BottomContentProperty];
        bottomHost[!ContentPresenter.ContentTemplateProperty] = this[!BottomContentTemplateProperty];

        var rows = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto")
        };
        rows.Children.Add(_statusBar);
        Grid.SetRow(topHost, 1);
        rows.Children.Add(topHost);
        Grid.SetRow(_screenHost, 2);
        rows.Children.Add(_screenHost);
        Grid.SetRow(bottomHost, 3);
        rows.Children.Add(bottomHost);
        Grid.SetRow(_gestureBar, 4);
        rows.Children.Add(_gestureBar);

        _screen = new Border
        {
            CornerRadius = ScreenCornerRadius,
            ClipToBounds = true,
            Child = rows
        };
        _screen.Bind(Border.BackgroundProperty, _screen.GetResourceObservable("MaterialSurfaceBrush"));
        _screen.SizeChanged += (_, _) => UpdateScreenClip();

        var frame = new Border
        {
            BorderThickness = new Thickness(FrameThickness),
            CornerRadius = new CornerRadius(ScreenCornerRadius.TopLeft + FrameThickness),
            Child = _screen
        };
        frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("MaterialOutlineVariantBrush"));
        frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable("MaterialSurfaceBrush"));

        UpdateChrome();
        return frame;
    }

    private Control BuildGestureBar()
    {
        var handle = new Border
        {
            Width = 108,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        handle.Bind(Border.BackgroundProperty, handle.GetResourceObservable("MaterialOnSurfaceBrush"));

        var bar = new Grid { Height = 24 };
        bar.Children.Add(handle);
        bar.Bind(Panel.BackgroundProperty, bar.GetResourceObservable("MaterialSurfaceContainerBrush"));
        return bar;
    }

    private void UpdateChrome()
    {
        if (_statusBar is not null) _statusBar.IsVisible = ShowStatusBar;
        if (_gestureBar is not null) _gestureBar.IsVisible = ShowGestureBar;
        if (_topHost is not null) _topHost.IsVisible = TopContent is not null;
        if (_bottomHost is not null) _bottomHost.IsVisible = BottomContent is not null;
    }

    private void UpdateContentHost()
    {
        if (_screenHost is null || _presenter is null) return;
        if (IsScrollable)
        {
            _scroller ??= new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            if (!ReferenceEquals(_scroller.Content, _presenter)) _scroller.Content = _presenter;
            if (_screenHost.Children.Count == 1 && ReferenceEquals(_screenHost.Children[0], _scroller)) return;
            _screenHost.Children.Clear();
            _screenHost.Children.Add(_scroller);
        }
        else
        {
            if (_scroller is not null) _scroller.Content = null;
            if (_screenHost.Children.Count == 1 && ReferenceEquals(_screenHost.Children[0], _presenter)) return;
            _screenHost.Children.Clear();
            _screenHost.Children.Add(_presenter);
        }
    }

    private void UpdateScreenClip()
    {
        if (_screen is null) return;
        var size = _screen.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var radius = ScreenCornerRadius.TopLeft;
        _screen.Clip = new RectangleGeometry(new Rect(size), radius, radius);
    }
}
