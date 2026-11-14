using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using ExpressiveAvalonia.Tokens;

namespace ExpressiveAvalonia.Controls;

/// <summary>A native menu flyout with the source scale/alpha entrance and deferred exit.</summary>
/// <remarks>
/// Items, placement, submenus, commands and keyboard remain Avalonia-native. One pair of springs
/// owns both directions, including rapid reversals. The real close still honors Closing vetoes.
/// </remarks>
public class MaterialMenuFlyout : MenuFlyout
{
    private MenuFlyoutPresenter? _presenter;
    private ScaleTransform? _scale;
    private MaterialMotion.SpringAnimation? _scaleAnimation;
    private MaterialMotion.SpringAnimation? _opacityAnimation;
    private readonly Action<TimeSpan> _checkExit;
    private bool _exiting;
    private bool _framePending;
    private bool _skipExit;
    private bool _canCancel;

    /// <summary>Creates a native menu flyout.</summary>
    public MaterialMenuFlyout() => _checkExit = CheckExit;

    /// <inheritdoc />
    protected override Control CreatePresenter() => _presenter = (MenuFlyoutPresenter)base.CreatePresenter();

    /// <inheritdoc />
    protected override void OnOpened()
    {
        base.OnOpened();
        if (_presenter is not { } presenter)
            return;
        StopAnimations();
        _scale = new ScaleTransform(0.8, 0.8);
        presenter.RenderTransform = _scale;
        presenter.RenderTransformOrigin = Origin(presenter);
        presenter.Opacity = 0;
        presenter.IsHitTestVisible = true;
        _scaleAnimation = MaterialMotion.Animate(presenter, 0.8, 1, value =>
        {
            if (_scale is { } scale)
                scale.ScaleX = scale.ScaleY = value;
        }, MaterialSpring.SpatialFast);
        _opacityAnimation = MaterialMotion.Animate(presenter, 0, 1,
            value => presenter.Opacity = value, MaterialSpring.EffectsFast);
    }

    /// <inheritdoc />
    protected override bool ShowAtCore(Control placementTarget, bool showAtPointer = false)
    {
        if (_exiting && ReferenceEquals(Target, placementTarget))
        {
            _exiting = false;
            _scaleAnimation?.Retarget(1);
            _opacityAnimation?.Retarget(1);
            if (_presenter is { } presenter)
                presenter.IsHitTestVisible = true;
            return true;
        }
        _skipExit = true;
        try { return base.ShowAtCore(placementTarget, showAtPointer); }
        finally { _skipExit = false; }
    }

    /// <inheritdoc />
    protected override bool HideCore(bool canCancel = true)
    {
        if (!IsOpen || _skipExit || !canCancel || _presenter is not { } presenter ||
            !presenter.IsAttachedToVisualTree() || MaterialMotion.GetIsReduced(presenter))
            return base.HideCore(canCancel);
        if (_exiting)
            return true;
        _exiting = true;
        _canCancel = canCancel;
        presenter.IsHitTestVisible = false;
        _scaleAnimation?.Retarget(0.8);
        _opacityAnimation?.Retarget(0);
        RequestExitFrame();
        return true;
    }

    private void RequestExitFrame()
    {
        if (_framePending)
            return;
        if (_presenter is { } presenter && TopLevel.GetTopLevel(presenter) is { } root)
        {
            _framePending = true;
            root.RequestAnimationFrame(_checkExit);
        }
        else
            CompleteExit();
    }

    private void CheckExit(TimeSpan timestamp)
    {
        _framePending = false;
        if (!_exiting)
            return;
        if (_scaleAnimation is { IsRunning: true } || _opacityAnimation is { IsRunning: true })
            RequestExitFrame();
        else
            CompleteExit();
    }

    private void CompleteExit()
    {
        _exiting = false;
        if (!base.HideCore(_canCancel))
        {
            _scaleAnimation?.Retarget(1);
            _opacityAnimation?.Retarget(1);
            if (_presenter is { } presenter)
                presenter.IsHitTestVisible = true;
        }
    }

    /// <inheritdoc />
    protected override void OnClosed()
    {
        _exiting = false;
        StopAnimations();
        if (_presenter is { } presenter)
        {
            presenter.Opacity = 1;
            presenter.RenderTransform = null;
            presenter.IsHitTestVisible = true;
        }
        _scale = null;
        base.OnClosed();
    }

    private RelativePoint Origin(Control presenter)
    {
        var root = TopLevel.GetTopLevel(presenter);
        if (root is null || Target is not { } target)
            return RelativePoint.TopLeft;
        var menuOrigin = presenter.TranslatePoint(default, root);
        var anchorOrigin = target.TranslatePoint(default, root);
        return menuOrigin is { } menuPoint && anchorOrigin is { } anchorPoint
            ? MaterialMenu.CalculatePopupTransformOrigin(new Rect(anchorPoint, target.Bounds.Size),
                new Rect(menuPoint, presenter.Bounds.Size))
            : RelativePoint.TopLeft;
    }

    private void StopAnimations()
    {
        _scaleAnimation?.Dispose();
        _scaleAnimation = null;
        _opacityAnimation?.Dispose();
        _opacityAnimation = null;
    }
}
