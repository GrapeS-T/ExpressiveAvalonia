using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using ExpressiveAvalonia.Controls;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Component-owned chip selection changes must not introduce a local value that shadows a
/// style-supplied <see cref="MaterialChip.IsSelected"/> source. Click activation, group selection
/// enforcement and automation selection therefore use <c>SetCurrentValue</c>: the style binding
/// keeps owning the value and later source updates still reach the chip.
/// </summary>
public class ChipBindingTests
{
    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static MaterialChip FilterChip(string cssClass, string content)
    {
        var chip = new MaterialChip { Kind = MaterialChipKind.Filter, Content = content };
        chip.Classes.Add(cssClass);
        return chip;
    }

    /// <summary>A one-way style binding for IsSelected on the chips carrying <paramref name="cssClass"/>.</summary>
    private static Style SelectionStyle(string cssClass, SelectionSource source) =>
        new(x => x.OfType<MaterialChip>().Class(cssClass))
        {
            Setters =
            {
                new Setter(MaterialChip.IsSelectedProperty,
                    new Binding(nameof(SelectionSource.IsSelected))
                    {
                        Source = source,
                        Mode = BindingMode.OneWay,
                    }),
            },
        };

    /// <summary>Proves the style-supplied source already drives the chip before the interaction.</summary>
    private static void AssertStyleBindingLive(MaterialChip chip, SelectionSource source)
    {
        Assert.False(chip.IsSelected);
        source.IsSelected = true;
        Assert.True(chip.IsSelected);
        source.IsSelected = false;
        Assert.False(chip.IsSelected);
    }

    [AvaloniaFact]
    public void ClickSelectionKeepsStyleBindingLive()
    {
        var source = new SelectionSource();
        var chip = FilterChip("chip-binding-click", "Filter");
        var window = new Window { Width = 480, Height = 240, Content = chip };
        window.Styles.Add(SelectionStyle("chip-binding-click", source));
        window.Show();
        window.UpdateLayout();
        try
        {
            AssertStyleBindingLive(chip, source);

            var point = Center(chip, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(chip.IsSelected);

            // The activation must not pin a local value: the style source still owns the value.
            source.IsSelected = true;
            Assert.True(chip.IsSelected);
            source.IsSelected = false;
            Assert.False(chip.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void GroupSelectionKeepsStyleBindingLive()
    {
        var firstSource = new SelectionSource();
        var secondSource = new SelectionSource();
        var first = FilterChip("chip-binding-first", "First");
        var second = FilterChip("chip-binding-second", "Second");
        var group = new MaterialChipGroup { SelectionMode = MaterialChipSelectionMode.Single };
        group.Items.Add(first);
        group.Items.Add(second);

        var window = new Window { Width = 480, Height = 240, Content = group };
        window.Styles.Add(SelectionStyle("chip-binding-first", firstSource));
        window.Styles.Add(SelectionStyle("chip-binding-second", secondSource));
        window.Show();
        window.UpdateLayout();
        try
        {
            AssertStyleBindingLive(first, firstSource);
            AssertStyleBindingLive(second, secondSource);

            group.SelectChip(first);
            Assert.True(first.IsSelected);

            // Single selection clears the previous chip through the group's own mutation path.
            group.SelectChip(second);
            Assert.False(first.IsSelected);
            Assert.True(second.IsSelected);

            // The group-driven deselection must not shadow the style source.
            firstSource.IsSelected = true;
            Assert.True(first.IsSelected);
            Assert.False(second.IsSelected);

            secondSource.IsSelected = true;
            Assert.True(second.IsSelected);
            Assert.False(first.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutomationSelectionKeepsStyleBindingLive()
    {
        var source = new SelectionSource();
        var chip = FilterChip("chip-binding-automation", "Filter");
        var window = new Window { Width = 480, Height = 240, Content = chip };
        window.Styles.Add(SelectionStyle("chip-binding-automation", source));
        window.Show();
        window.UpdateLayout();
        try
        {
            AssertStyleBindingLive(chip, source);

            var peer = ControlAutomationPeer.CreatePeerForElement(chip);
            var selectionItem = Assert.IsAssignableFrom<ISelectionItemProvider>(peer);

            selectionItem.Select();
            Assert.True(chip.IsSelected);
            source.IsSelected = true;
            source.IsSelected = false;
            Assert.False(chip.IsSelected);

            selectionItem.AddToSelection();
            Assert.True(chip.IsSelected);
            source.IsSelected = true;
            Assert.True(chip.IsSelected);
            source.IsSelected = false;
            Assert.False(chip.IsSelected);

            selectionItem.RemoveFromSelection();
            Assert.False(chip.IsSelected);

            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
            toggle.Toggle();
            Assert.True(chip.IsSelected);
            source.IsSelected = true;
            source.IsSelected = false;
            Assert.False(chip.IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PublicSetterKeepsLocalPriority()
    {
        var source = new SelectionSource();
        var chip = FilterChip("chip-binding-public", "Filter");
        var window = new Window { Width = 480, Height = 240, Content = chip };
        window.Styles.Add(SelectionStyle("chip-binding-public", source));
        window.Show();
        window.UpdateLayout();
        try
        {
            AssertStyleBindingLive(chip, source);

            // The public CLR setter stays conventional: an application assignment is a local value
            // and keeps precedence over a style-supplied selection.
            chip.IsSelected = true;
            source.IsSelected = true;
            Assert.True(chip.IsSelected);
            source.IsSelected = false;
            Assert.True(chip.IsSelected);
            Assert.False(source.IsSelected);
        }
        finally { window.Close(); }
    }

    private sealed class SelectionSource : INotifyPropertyChanged
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}