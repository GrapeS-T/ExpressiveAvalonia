using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
// System.IO is an implicit using here, so the shape type needs an explicit alias.
using Path = Avalonia.Controls.Shapes.Path;

namespace ExpressiveAvalonia.Controls;

/// <summary>A row of <see cref="MaterialMenuTextField"/>.</summary>
public sealed class MaterialMenuTextFieldItem : INotifyPropertyChanged
{
    public MaterialMenuTextFieldItem()
    {
    }

    public MaterialMenuTextFieldItem(string label, string? supportingText = null, object? icon = null)
    {
        Label = label;
        SupportingText = supportingText;
        Icon = icon;
    }

    private string? _label;
    private string? _supportingText;
    private object? _icon;
    private string? _keywords;
    private bool _isEnabled = true;
    private object? _tag;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Primary text, rendered at 16/24.</summary>
    public string? Label { get => _label; set => Set(ref _label, value); }
    /// <summary>Secondary text, rendered at 14/20 under the label.</summary>
    public string? SupportingText { get => _supportingText; set => Set(ref _supportingText, value); }
    /// <summary>Leading content; a 24x24 control such as a <see cref="Path"/> is expected.</summary>
    public object? Icon { get => _icon; set => Set(ref _icon, value); }
    /// <summary>Additional text matched by the filter but never displayed.</summary>
    public string? Keywords { get => _keywords; set => Set(ref _keywords, value); }
    /// <summary>Disabled rows stay visible but cannot be selected or invoked.</summary>
    public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }
    /// <summary>Consumer payload returned by <see cref="MaterialMenuTextField.ItemInvoked"/>.</summary>
    public object? Tag { get => _tag; set => Set(ref _tag, value); }

    internal bool Matches(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return true;
        return Contains(Label, query) || Contains(SupportingText, query) || Contains(Keywords, query);
    }

    private static bool Contains(string? value, string query) =>
        value is not null && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}

public sealed class MaterialMenuTextFieldInvokedEventArgs : EventArgs
{
    public MaterialMenuTextFieldInvokedEventArgs(MaterialMenuTextFieldItem item) => Item = item;

    public MaterialMenuTextFieldItem Item { get; }
}

/// <summary>
/// Searchable menu composed from a Material outlined text field and a menu list, following the
/// "Menu with Text field - Example 1/2" source compositions (local-004355, local-004356).
/// </summary>
/// <remarks>
/// The field keeps native text editing, selection, clipboard and input methods. Typing filters
/// <see cref="Items"/>; Down/Up move the highlighted row while focus stays in the field, Enter
/// invokes the highlighted row, Escape clears the filter and a pointer click invokes a row.
/// </remarks>
public class MaterialMenuTextField : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, string?>(nameof(Label));
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, string?>(nameof(PlaceholderText), "Search");
    public static readonly StyledProperty<string?> SupportingTextProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, string?>(nameof(SupportingText));
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, string?>(nameof(Text), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ShowSearchIconProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, bool>(nameof(ShowSearchIcon), true);
    public static readonly StyledProperty<double> MaxMenuHeightProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, double>(nameof(MaxMenuHeight), 240);
    public static readonly StyledProperty<MaterialMenuTextFieldItem?> SelectedItemProperty =
        AvaloniaProperty.Register<MaterialMenuTextField, MaterialMenuTextFieldItem?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    private static readonly Geometry SearchGeometry = Geometry.Parse(
        "M39.6 45L33.3 38.7C32.8 39.1 32.225 39.4167 31.575 39.65C30.925 39.8833 30.2333 40 29.5 40" +
        "C27.6833 40 26.1458 39.3708 24.8875 38.1125C23.6292 36.8542 23 35.3167 23 33.5" +
        "C23 31.6833 23.6292 30.1458 24.8875 28.8875C26.1458 27.6292 27.6833 27 29.5 27" +
        "C31.3167 27 32.8542 27.6292 34.1125 28.8875C35.3708 30.1458 36 31.6833 36 33.5" +
        "C36 34.2333 35.8833 34.925 35.65 35.575C35.4167 36.225 35.1 36.8 34.7 37.3L41 43.6L39.6 45Z" +
        "M29.5 38C30.75 38 31.8125 37.5625 32.6875 36.6875C33.5625 35.8125 34 34.75 34 33.5" +
        "C34 32.25 33.5625 31.1875 32.6875 30.3125C31.8125 29.4375 30.75 29 29.5 29" +
        "C28.25 29 27.1875 29.4375 26.3125 30.3125C25.4375 31.1875 25 32.25 25 33.5" +
        "C25 34.75 25.4375 35.8125 26.3125 36.6875C27.1875 37.5625 28.25 38 29.5 38Z");

    private readonly AvaloniaList<MaterialMenuTextFieldItem> _items = new();
    private readonly ObservableCollection<MaterialMenuTextFieldItem> _visible = new();
    private readonly HashSet<MaterialMenuTextFieldItem> _observedItems = new();
    private TextBox? _input;
    private ListBox? _list;
    private TextBlock? _empty;
    private IDisposable? _leadingSubscription;
    private bool _updatingSelection;

    public MaterialMenuTextField()
    {
        _items.CollectionChanged += OnItemsChanged;
        _visible.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    /// <summary>Rows of the menu. Mutations re-run the current filter.</summary>
    public IList<MaterialMenuTextFieldItem> Items => _items;

    /// <summary>Raised when a row is invoked by Enter or a pointer click.</summary>
    public event EventHandler<MaterialMenuTextFieldInvokedEventArgs>? ItemInvoked;

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Shows the leading search icon of the second source example.</summary>
    public bool ShowSearchIcon { get => GetValue(ShowSearchIconProperty); set => SetValue(ShowSearchIconProperty, value); }

    /// <summary>Maximum menu height; the source menu is 240 with a 224 list viewport.</summary>
    public double MaxMenuHeight { get => GetValue(MaxMenuHeightProperty); set => SetValue(MaxMenuHeightProperty, value); }

    /// <summary>The highlighted row, or null when the filter matches nothing.</summary>
    public MaterialMenuTextFieldItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _input = e.NameScope.Find<TextBox>("PART_Input");
        _list = e.NameScope.Find<ListBox>("PART_List");
        _empty = e.NameScope.Find<TextBlock>("PART_Empty");
        if (_input is not null)
            _input.KeyDown += OnInputKeyDown;
        if (_list is not null)
        {
            _list.ItemsSource = _visible;
            _list.SelectionChanged += OnListSelectionChanged;
            _list.ContainerPrepared += OnContainerPrepared;
            _list.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }
        UpdateLeadingIcon();
        ApplyFilter();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
            ApplyFilter();
        else if (change.Property == ShowSearchIconProperty)
            UpdateLeadingIcon();
        else if (change.Property == SelectedItemProperty && !_updatingSelection)
            SyncSelectionToList();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var current = new HashSet<MaterialMenuTextFieldItem>(_items);
        foreach (var item in _observedItems)
            if (!current.Contains(item)) item.PropertyChanged -= OnItemPropertyChanged;
        foreach (var item in current)
            if (!_observedItems.Contains(item)) item.PropertyChanged += OnItemPropertyChanged;
        _observedItems.Clear();
        _observedItems.UnionWith(current);
        ApplyFilter();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => ApplyFilter();

    private void DetachParts()
    {
        if (_input is not null)
            _input.KeyDown -= OnInputKeyDown;
        if (_list is not null)
        {
            _list.SelectionChanged -= OnListSelectionChanged;
            _list.ContainerPrepared -= OnContainerPrepared;
            _list.RemoveHandler(PointerReleasedEvent, OnListPointerReleased);
        }
        _leadingSubscription?.Dispose();
        _leadingSubscription = null;
        _input = null;
        _list = null;
        _empty = null;
    }

    private void UpdateLeadingIcon()
    {
        if (_input is null)
            return;
        _leadingSubscription?.Dispose();
        _leadingSubscription = null;
        if (!ShowSearchIcon)
        {
            MaterialTextField.SetLeadingContent(_input, null);
            return;
        }
        // The icon is created per instance because a control cannot have two visual parents.
        var icon = new Path
        {
            Data = SearchGeometry,
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        _leadingSubscription = icon.Bind(Shape.FillProperty, icon.GetResourceObservable("MaterialOnSurfaceVariantBrush"));
        MaterialTextField.SetLeadingContent(_input, icon);
    }

    private void ApplyFilter()
    {
        var query = Text?.Trim();
        var previous = SelectedItem;
        _updatingSelection = true;
        try
        {
            _visible.Clear();
            foreach (var item in _items)
            {
                if (item.Matches(query))
                    _visible.Add(item);
            }
            if (_list is not null)
            {
                // Keep the highlight on the same row while it still matches; otherwise start clean
                // so the menu matches the source composition until the user navigates by keyboard.
                _list.SelectedItem = previous is not null && previous.IsEnabled && _visible.Contains(previous) ? previous : null;
                SetCurrentValue(SelectedItemProperty, _list.SelectedItem as MaterialMenuTextFieldItem);
            }
        }
        finally
        {
            _updatingSelection = false;
        }
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (_empty is not null)
            _empty.IsVisible = _visible.Count == 0;
        if (_list is not null)
            _list.IsVisible = _visible.Count > 0;
    }

    private void SyncSelectionToList()
    {
        if (_list is null)
            return;
        _updatingSelection = true;
        try
        {
            _list.SelectedItem = SelectedItem;
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
            return;
        _updatingSelection = true;
        try
        {
            SetCurrentValue(SelectedItemProperty, _list?.SelectedItem as MaterialMenuTextFieldItem);
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // Disabled rows stay in the list but cannot be selected; the container follows the model.
        if (e.Container is ListBoxItem container && e.Index >= 0 && e.Index < _visible.Count)
            container.IsEnabled = _visible[e.Index].IsEnabled;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (InvokeSelected())
                    e.Handled = true;
                break;
            case Key.Escape:
                if (!string.IsNullOrEmpty(Text))
                {
                    SetCurrentValue(TextProperty, string.Empty);
                    e.Handled = true;
                }
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_list is null || _visible.Count == 0)
            return;
        var count = _visible.Count;
        var start = _list.SelectedIndex;
        if (start < 0)
            start = delta > 0 ? -1 : count;
        for (var step = 1; step <= count; step++)
        {
            var next = ((start + delta * step) % count + count) % count;
            if (!_visible[next].IsEnabled)
                continue;
            _list.SelectedIndex = next;
            _list.ScrollIntoView(next);
            return;
        }
    }

    private bool InvokeSelected()
    {
        var item = SelectedItem ?? _visible.FirstOrDefault(candidate => candidate.IsEnabled);
        if (item is null || !item.IsEnabled)
            return false;
        ItemInvoked?.Invoke(this, new MaterialMenuTextFieldInvokedEventArgs(item));
        return true;
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if ((e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) || e.Source is not Visual source)
            return;
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } container)
            return;
        if (container.DataContext is not MaterialMenuTextFieldItem item || !item.IsEnabled)
            return;
        _updatingSelection = true;
        try
        {
            if (_list is not null)
                _list.SelectedItem = item;
            SetCurrentValue(SelectedItemProperty, item);
        }
        finally
        {
            _updatingSelection = false;
        }
        ItemInvoked?.Invoke(this, new MaterialMenuTextFieldInvokedEventArgs(item));
        e.Handled = true;
    }
}
