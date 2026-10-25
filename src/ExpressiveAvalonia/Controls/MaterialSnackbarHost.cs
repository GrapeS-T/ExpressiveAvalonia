using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// Bottom-centred overlay that queues <see cref="MaterialSnackbar"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Material shows one snackbar at a time: a second request waits in a FIFO queue and appears
/// after the current snackbar has finished its exit transition. <see cref="Show"/> returns a task
/// that completes with the <see cref="MaterialSnackbarCloseReason"/> when that snackbar is gone.
/// </para>
/// <para>
/// The host can be placed in a window (usually in a grid overlay) or created automatically in the
/// top level's overlay layer with <see cref="GetHost"/> / <see cref="Show(TopLevel, MaterialSnackbar)"/>.
/// It restores keyboard focus when a snackbar that owns focus is dismissed, so keyboard users are
/// never dropped onto a removed element.
/// </para>
/// </remarks>
public sealed class MaterialSnackbarHost : TemplatedControl
{
    private static readonly ConditionalWeakTable<TopLevel, MaterialSnackbarHost> Hosts = new();

    private readonly Queue<MaterialSnackbar> _queue = new();
    private readonly Dictionary<MaterialSnackbar, TaskCompletionSource<MaterialSnackbarCloseReason>> _pending = new();
    private StackPanel? _items;
    private MaterialSnackbar? _current;
    private bool _currentShown;
    private IInputElement? _previousFocus;

    /// <summary>The snackbar currently on screen, or null when the host is idle.</summary>
    public MaterialSnackbar? Current => _current;

    /// <summary>Number of snackbars waiting behind <see cref="Current"/>.</summary>
    public int PendingCount => _queue.Count;

    /// <summary>
    /// Returns the host attached to <paramref name="topLevel"/>, creating it in the top level's
    /// overlay layer on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The top level has no overlay layer yet (for example before it is opened). Place a
    /// <see cref="MaterialSnackbarHost"/> in the window instead.
    /// </exception>
    public static MaterialSnackbarHost GetHost(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (Hosts.TryGetValue(topLevel, out var host) && host.GetVisualParent() is not null)
            return host;

        var layer = FindOverlayLayer(topLevel)
            ?? throw new InvalidOperationException(
                "MaterialSnackbarHost needs a TopLevel with a VisualLayerManager. " +
                "Place a MaterialSnackbarHost in the window's visual tree instead.");

        host = new MaterialSnackbarHost
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        // OverlayLayer is a Canvas: it arranges children at their desired size, so the host must
        // track the layer size explicitly instead of relying on Stretch.
        host.Bind(Layoutable.WidthProperty, new Avalonia.Data.Binding("Bounds.Width") { Source = layer });
        host.Bind(Layoutable.HeightProperty, new Avalonia.Data.Binding("Bounds.Height") { Source = layer });
        layer.Children.Add(host);
        Hosts.AddOrUpdate(topLevel, host);
        return host;
    }

    /// <summary>Shows <paramref name="snackbar"/> in the top level's overlay host.</summary>
    public static Task<MaterialSnackbarCloseReason> Show(TopLevel topLevel, MaterialSnackbar snackbar) =>
        GetHost(topLevel).Show(snackbar);

    /// <summary>
    /// Queues a snackbar and completes when it has closed. The same instance may only be shown
    /// once at a time; the returned task is shared if it is already current or queued.
    /// </summary>
    public Task<MaterialSnackbarCloseReason> Show(MaterialSnackbar snackbar)
    {
        ArgumentNullException.ThrowIfNull(snackbar);

        if (_pending.TryGetValue(snackbar, out var existing))
            return existing.Task;

        if (snackbar.GetVisualParent() is not null && !ReferenceEquals(snackbar.GetVisualParent(), _items))
            throw new InvalidOperationException(
                "The snackbar is already attached to a visual tree. Remove it or create a new instance.");

        var completion = new TaskCompletionSource<MaterialSnackbarCloseReason>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Add(snackbar, completion);

        if (_current is null)
        {
            _current = snackbar;
            AttachCurrent();
        }
        else
        {
            _queue.Enqueue(snackbar);
        }

        return completion.Task;
    }

    /// <summary>Dismisses the visible snackbar; queued snackbars continue afterwards.</summary>
    public void DismissCurrent(MaterialSnackbarCloseReason reason = MaterialSnackbarCloseReason.Programmatic)
    {
        if (_current is not { } current)
            return;

        if (_currentShown)
        {
            current.Dismiss(reason);
            return;
        }

        // Attached but not shown yet: drop it and continue with the queue.
        DetachCurrent(current, reason);
        _current = null;
        _previousFocus = null;
        Advance();
    }

    /// <summary>Drops every queued snackbar and dismisses the visible one.</summary>
    public void DismissAll(MaterialSnackbarCloseReason reason = MaterialSnackbarCloseReason.Programmatic)
    {
        while (_queue.Count > 0)
        {
            var queued = _queue.Dequeue();
            if (_pending.Remove(queued, out var completion))
                completion.TrySetResult(reason);
        }

        if (_current is not { } current)
            return;

        if (_currentShown)
        {
            current.Dismiss(reason);
            return;
        }

        // The current snackbar was attached but its Show post has not run yet: drop it outright.
        DetachCurrent(current, reason);
        _current = null;
        _previousFocus = null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _items = e.NameScope.Find<StackPanel>("PART_Items");
        AttachCurrent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Complete waiters so callers are not left hanging when the window goes away.
        while (_queue.Count > 0)
        {
            var queued = _queue.Dequeue();
            if (_pending.Remove(queued, out var completion))
                completion.TrySetResult(MaterialSnackbarCloseReason.Programmatic);
        }

        if (_current is not null && _pending.Remove(_current, out var currentCompletion))
        {
            currentCompletion.TrySetResult(MaterialSnackbarCloseReason.Programmatic);
            _current.Dismiss(MaterialSnackbarCloseReason.Programmatic);
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void AttachCurrent()
    {
        if (_current is null || _items is null)
            return;
        if (ReferenceEquals(_current.GetVisualParent(), _items))
            return;

        // A re-applied template produces a new PART_Items; move the snackbar to the new host.
        if (_current.GetVisualParent() is Panel previous)
        {
            previous.Children.Remove(_current);
            _currentShown = true; // Keep the current snackbar open; only its parent changed.
        }
        else if (_current.GetVisualParent() is not null)
        {
            return;
        }
        else
        {
            _previousFocus ??= TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            _currentShown = false;
        }

        _current.Closing -= OnCurrentClosing;
        _current.Closed -= OnCurrentClosed;
        _current.Closing += OnCurrentClosing;
        _current.Closed += OnCurrentClosed;
        _items.Children.Add(_current);

        if (_currentShown)
            return;

        var snackbar = _current;
        // One layout pass lets the hidden initial style apply before :open starts the transition.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (ReferenceEquals(_current, snackbar))
                {
                    _currentShown = true;
                    snackbar.Show();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void OnCurrentClosing(object? sender, MaterialSnackbarCloseEventArgs e)
    {
        if (sender is not MaterialSnackbar snackbar)
            return;

        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (!IsFocusWithin(snackbar, focus))
            return;

        if (_previousFocus is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
        _previousFocus = null;
    }

    private void OnCurrentClosed(object? sender, MaterialSnackbarCloseEventArgs e)
    {
        if (sender is not MaterialSnackbar snackbar)
            return;

        DetachCurrent(snackbar, e.Reason);

        if (ReferenceEquals(_current, snackbar))
            _current = null;
        _currentShown = false;
        _previousFocus = null;
        Advance();
    }

    private void Advance()
    {
        if (_current is null && _queue.Count > 0)
        {
            _current = _queue.Dequeue();
            AttachCurrent();
        }
    }

    private void DetachCurrent(MaterialSnackbar snackbar, MaterialSnackbarCloseReason reason)
    {
        snackbar.Closing -= OnCurrentClosing;
        snackbar.Closed -= OnCurrentClosed;
        _items?.Children.Remove(snackbar);

        if (_pending.Remove(snackbar, out var completion))
            completion.TrySetResult(reason);
    }

    private static bool IsFocusWithin(Visual root, IInputElement? focus) =>
        focus is Visual visual && (ReferenceEquals(visual, root) || root.IsVisualAncestorOf(visual));

    /// <summary>
    /// Resolves the top level's overlay layer. A plain <see cref="TopLevel"/> only creates its
    /// overlay layer once something asks for it, so enable the layer on the window's
    /// <see cref="VisualLayerManager"/> when it is still disabled.
    /// </summary>
    private static OverlayLayer? FindOverlayLayer(TopLevel topLevel)
    {
        var layer = OverlayLayer.GetOverlayLayer(topLevel);
        if (layer is not null)
            return layer;

        var manager = (topLevel.Content as Visual)?.GetSelfAndVisualDescendants()
            .OfType<VisualLayerManager>()
            .FirstOrDefault();
        if (manager is null || manager.EnableOverlayLayer)
            return null;

        manager.EnableOverlayLayer = true;
        return OverlayLayer.GetOverlayLayer(topLevel);
    }
}
