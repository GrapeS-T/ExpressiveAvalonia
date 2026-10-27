using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
/// Material shows one snackbar at a time: a second request waits in a FIFO queue. The task
/// returned by <see cref="Show"/> completes with the <see cref="MaterialSnackbarCloseReason"/> as
/// soon as the snackbar starts closing, matching the source continuation which resumes on
/// performAction/dismiss rather than after the fade. The dismissed surface stays in the overlay
/// until its exit springs settle, so the next item can animate in while the previous fades out.
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
    private readonly List<MaterialSnackbar> _retiring = [];
    private readonly Dictionary<MaterialSnackbar, TaskCompletionSource<MaterialSnackbarCloseReason>> _pending = new();
    private Panel? _items;
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
    /// Queues a snackbar and completes when it starts closing. The same instance may only be
    /// shown once at a time; the returned task is shared if it is already current or queued.
    /// </summary>
    public Task<MaterialSnackbarCloseReason> Show(MaterialSnackbar snackbar)
    {
        ArgumentNullException.ThrowIfNull(snackbar);

        if (_pending.TryGetValue(snackbar, out var existing))
            return existing.Task;

        if (snackbar.GetVisualParent() is not null && !ReferenceEquals(snackbar.GetVisualParent(), _items))
            throw new InvalidOperationException(
                "The snackbar is already attached to a visual tree. Remove it or create a new instance.");

        if (_retiring.Remove(snackbar))
        {
            // A queued reuse must not remain in the overlay with stale close handlers.
            // Detach it now; AttachCurrent will subscribe again when its turn arrives.
            snackbar.Closing -= OnCurrentClosing;
            snackbar.Closed -= OnCurrentClosed;
            _items?.Children.Remove(snackbar);
        }

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

    /// <summary>
    /// Cancels one snackbar without touching the current item or the rest of the queue.
    /// </summary>
    /// <returns>True when the snackbar was current or queued.</returns>
    public bool Cancel(MaterialSnackbar snackbar,
        MaterialSnackbarCloseReason reason = MaterialSnackbarCloseReason.Programmatic)
    {
        ArgumentNullException.ThrowIfNull(snackbar);

        if (ReferenceEquals(_current, snackbar))
        {
            if (_currentShown)
            {
                snackbar.Dismiss(reason);
            }
            else
            {
                DetachCurrent(snackbar, reason);
                _current = null;
                _currentShown = false;
                _previousFocus = null;
                Advance();
            }
            return true;
        }

        if (!_queue.Contains(snackbar))
            return false;

        var remaining = new Queue<MaterialSnackbar>(_queue.Count);
        var found = false;
        while (_queue.Count > 0)
        {
            var item = _queue.Dequeue();
            if (ReferenceEquals(item, snackbar))
            {
                found = true;
                if (_pending.Remove(item, out var completion))
                    completion.TrySetResult(reason);
            }
            else
            {
                remaining.Enqueue(item);
            }
        }
        while (remaining.Count > 0)
            _queue.Enqueue(remaining.Dequeue());

        return found;
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
        _items = e.NameScope.Find<Panel>("PART_Items");
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

        foreach (var retiring in _retiring.ToArray())
        {
            retiring.Closing -= OnCurrentClosing;
            retiring.Closed -= OnCurrentClosed;
            _items?.Children.Remove(retiring);
        }
        _retiring.Clear();

        if (_current is not null && _pending.Remove(_current, out var currentCompletion))
        {
            currentCompletion.TrySetResult(MaterialSnackbarCloseReason.Programmatic);
            _current.Closing -= OnCurrentClosing;
            _current.Closed -= OnCurrentClosed;
            _current.Dismiss(MaterialSnackbarCloseReason.Programmatic);
            _items?.Children.Remove(_current);
        }

        _current = null;
        _currentShown = false;
        _previousFocus = null;

        base.OnDetachedFromVisualTree(e);
    }

    private void AttachCurrent()
    {
        if (_current is null || _items is null)
            return;
        if (ReferenceEquals(_current.GetVisualParent(), _items))
        {
            if (!_currentShown)
                ScheduleShow(_current);
            return;
        }

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
        _current.HorizontalAlignment = HorizontalAlignment.Stretch;
        _current.VerticalAlignment = VerticalAlignment.Bottom;
        _items.Children.Add(_current);

        if (_currentShown)
            return;

        ScheduleShow(_current);
    }

    private void ScheduleShow(MaterialSnackbar snackbar)
    {
        // One layout pass lets the hidden initial style apply before :open starts the transition.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (ReferenceEquals(_current, snackbar) && !_currentShown)
                {
                    _currentShown = true;
                    snackbar.Show();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void OnCurrentClosing(object? sender, MaterialSnackbarCloseEventArgs e)
    {
        if (sender is not MaterialSnackbar snackbar || !ReferenceEquals(_current, snackbar))
            return;

        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (IsFocusWithin(snackbar, focus) &&
            _previousFocus is Control { IsEffectivelyEnabled: true, IsVisible: true } control &&
            control.IsAttachedToVisualTree())
            control.Focus(NavigationMethod.Unspecified);
        _previousFocus = null;

        // SnackbarHostState resumes on performAction/dismiss, not after the fade. Complete the
        // waiter and let the next queued item appear while this one runs its exit springs.
        if (_pending.Remove(snackbar, out var completion))
            completion.TrySetResult(e.Reason);

        if (!_retiring.Contains(snackbar))
            _retiring.Add(snackbar);
        _current = null;
        _currentShown = false;
        Advance();
    }

    private void OnCurrentClosed(object? sender, MaterialSnackbarCloseEventArgs e)
    {
        if (sender is not MaterialSnackbar snackbar)
            return;

        if (_pending.Remove(snackbar, out var completion))
            completion.TrySetResult(e.Reason);

        snackbar.Closing -= OnCurrentClosing;
        snackbar.Closed -= OnCurrentClosed;
        _items?.Children.Remove(snackbar);
        _retiring.Remove(snackbar);

        if (ReferenceEquals(_current, snackbar))
        {
            _current = null;
            _currentShown = false;
            _previousFocus = null;
            Advance();
        }
    }

    private void Advance()
    {
        while (_current is null && _queue.Count > 0)
        {
            var next = _queue.Dequeue();
            if (!_pending.ContainsKey(next))
                continue; // Cancelled while waiting.
            _current = next;
            AttachCurrent();
            return;
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