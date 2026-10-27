using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using MaterialColorUtilities.HCT;
using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Utils;

namespace ExpressiveAvalonia.Controls;

/// <summary>One tone cell of a palette row.</summary>
public readonly record struct MaterialTone(int Tone, Color Color)
{
    /// <summary>The color as <c>#RRGGBB</c>.</summary>
    public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
}

/// <summary>One labelled palette row, for example Primary 0..100.</summary>
public sealed class MaterialTonalPaletteRow
{
    /// <summary>Creates a row.</summary>
    /// <param name="name">Row label, for example <c>Primary</c>.</param>
    /// <param name="swatches">Ordered tone cells.</param>
    /// <param name="startsGroup">
    /// Adds the source board's extra gap above this row. The board separates the
    /// primary/secondary/tertiary/error group from the neutral rows this way.
    /// </param>
    /// <param name="height">
    /// Cell height for this row; <c>null</c> uses <see cref="MaterialTonalPalette.SwatchHeight"/>.
    /// The source board's clipped final row is 70 instead of 108.
    /// </param>
    public MaterialTonalPaletteRow(string name, IReadOnlyList<MaterialTone> swatches, bool startsGroup = false,
        double? height = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(swatches);
        if (height is { } value && (!double.IsFinite(value) || value <= 0))
            throw new ArgumentOutOfRangeException(nameof(height), value, "A row height must be positive.");
        Name = name;
        Swatches = swatches;
        StartsGroup = startsGroup;
        Height = height;
    }

    public string Name { get; }

    public IReadOnlyList<MaterialTone> Swatches { get; }

    public bool StartsGroup { get; }

    /// <summary>Cell height override for this row, or <c>null</c> for the palette default.</summary>
    public double? Height { get; }

    public override string ToString() => Name;
}

/// <summary>Tone rows used by <see cref="MaterialTonalPalette"/>.</summary>
public static class MaterialTonalPalettes
{
    /// <summary>The thirteen tones drawn by the source board.</summary>
    public static IReadOnlyList<int> Tones { get; } = new ReadOnlyCollection<int>(
        new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100 });

    private static readonly Lazy<IReadOnlyList<MaterialTonalPaletteRow>> s_source = new(CreateSourceRows);

    /// <summary>
    /// The exact colors of the <c>.Tonal palettes</c> source board (local-005641),
    /// transcribed from the exported design fills.
    /// </summary>
    public static IReadOnlyList<MaterialTonalPaletteRow> Source => s_source.Value;

    /// <summary>
    /// Builds live MD3 tonal palettes for <paramref name="seed"/> with the same Tonal Spot
    /// construction as <c>MaterialColorScheme</c>: primary chroma 36, secondary 16, tertiary 24,
    /// neutral 6, neutral-variant 8 and the fixed error hue 25 / chroma 84. With the library's
    /// default seed the tone 40 cells equal the generated primary, secondary, tertiary and error
    /// roles, so the board stays consistent with the active theme. This is a read-only
    /// visualization derived through MaterialColorUtilities HCT; it is not M3
    /// <c>TonalPalette.kt</c> theme data and does not add a dynamic-color API.
    /// </summary>
    public static IReadOnlyList<MaterialTonalPaletteRow> FromSeed(Color seed)
    {
        var source = Hct.From(new ArgbColor(255, seed.R, seed.G, seed.B));
        return
        [
            Row("Primary", new TonalPalette(source.Hue, 36)),
            Row("Secondary", new TonalPalette(source.Hue, 16)),
            Row("Tertiary", new TonalPalette(source.Hue + 60, 24)),
            Row("Error", new TonalPalette(25, 84)),
            Row("Neutral", new TonalPalette(source.Hue, 6), startsGroup: true),
            Row("Neutral Variant", new TonalPalette(source.Hue, 8))
        ];
    }

    private static MaterialTonalPaletteRow Row(string name, TonalPalette palette, bool startsGroup = false)
    {
        var swatches = new MaterialTone[Tones.Count];
        for (var i = 0; i < swatches.Length; i++)
        {
            var tone = Tones[i];
            var color = palette.Get(tone);
            swatches[i] = new MaterialTone(tone, Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        }
        return new MaterialTonalPaletteRow(name, swatches, startsGroup);
    }

    private static IReadOnlyList<MaterialTonalPaletteRow> CreateSourceRows()
    {
        var rows = new MaterialTonalPaletteRow[MaterialTonalPaletteSourceData.Rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var (name, swatches) = MaterialTonalPaletteSourceData.Rows[i];
            // The source board separates the neutral rows from the four accent rows.
            rows[i] = new MaterialTonalPaletteRow(name, swatches, startsGroup: i == 4,
                height: i == 5 ? 70 : null);
        }
        return new ReadOnlyCollection<MaterialTonalPaletteRow>(rows);
    }
}

/// <summary>Reports an activated palette cell.</summary>
/// <param name="rowName">Owning palette row label.</param>
/// <param name="tone">Material tone of the cell.</param>
/// <param name="color">The cell color.</param>
/// <param name="copied">Whether the hex value reached the platform clipboard.</param>
public sealed class MaterialTonalSwatchEventArgs(string rowName, int tone, Color color, bool copied) : EventArgs
{
    public string RowName { get; } = rowName;

    public int Tone { get; } = tone;

    public Color Color { get; } = color;

    /// <summary><c>false</c> when no platform clipboard accepted the text.</summary>
    public bool Copied { get; } = copied;

    public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
}

/// <summary>
/// A focusable tone cell. Activating it with the pointer or with Space/Enter copies the
/// <c>#RRGGBB</c> value to the clipboard and raises <see cref="Activated"/>.
/// </summary>
/// <remarks>
/// Cells are real buttons, so pointer capture, focus, pressed state and Invoke automation all
/// come from the native <see cref="Button"/> implementation. Clipboard failures (no platform
/// clipboard, denied access) are reported through <see cref="CopyColorToClipboardAsync"/>
/// instead of throwing from the click handler.
/// </remarks>
public sealed class MaterialTonalSwatch : Button
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, Color>(nameof(Color));

    public static readonly StyledProperty<int> ToneProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, int>(nameof(Tone), validate: value => value is >= 0 and <= 100);

    public static readonly StyledProperty<string?> RowNameProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, string?>(nameof(RowName));

    public static readonly StyledProperty<bool> CopyOnClickProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, bool>(nameof(CopyOnClick), true);

    public static readonly StyledProperty<bool> ShowToneLabelProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, bool>(nameof(ShowToneLabel), true);

    public static readonly StyledProperty<double> ToneFontSizeProperty =
        AvaloniaProperty.Register<MaterialTonalSwatch, double>(nameof(ToneFontSize), 16,
            validate: value => double.IsFinite(value) && value > 0);

    public static readonly DirectProperty<MaterialTonalSwatch, IBrush> SwatchBrushProperty =
        AvaloniaProperty.RegisterDirect<MaterialTonalSwatch, IBrush>(nameof(SwatchBrush), o => o.SwatchBrush);

    public static readonly DirectProperty<MaterialTonalSwatch, IBrush> ToneLabelBrushProperty =
        AvaloniaProperty.RegisterDirect<MaterialTonalSwatch, IBrush>(nameof(ToneLabelBrush), o => o.ToneLabelBrush);

    public static readonly DirectProperty<MaterialTonalSwatch, string> HexProperty =
        AvaloniaProperty.RegisterDirect<MaterialTonalSwatch, string>(nameof(Hex), o => o.Hex);

    public static readonly DirectProperty<MaterialTonalSwatch, string> ToneLabelProperty =
        AvaloniaProperty.RegisterDirect<MaterialTonalSwatch, string>(nameof(ToneLabel), o => o.ToneLabel);

    private static readonly IBrush s_darkLabel = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
    private static readonly IBrush s_lightLabel = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

    private IBrush _swatchBrush = Brushes.Transparent;
    private IBrush _toneLabelBrush = s_lightLabel;
    private string _hex = "#000000";
    private string _toneLabel = "0";

    public MaterialTonalSwatch()
    {
        // Direct properties are computed, not stored, so seed them for the default values.
        ApplyColor();
        ApplyTone();
    }

    /// <summary>Raised after every activation, whether or not a clipboard was available.</summary>
    public event EventHandler<MaterialTonalSwatchEventArgs>? Activated;

    /// <summary>The cell color.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Material tone, 0..100. Tones above 50 use a black label like the source board.</summary>
    public int Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>Owning palette row name, used for the automation name.</summary>
    public string? RowName { get => GetValue(RowNameProperty); set => SetValue(RowNameProperty, value); }

    /// <summary>Whether activation copies <see cref="Hex"/>.</summary>
    public bool CopyOnClick { get => GetValue(CopyOnClickProperty); set => SetValue(CopyOnClickProperty, value); }

    public bool ShowToneLabel { get => GetValue(ShowToneLabelProperty); set => SetValue(ShowToneLabelProperty, value); }

    public double ToneFontSize { get => GetValue(ToneFontSizeProperty); set => SetValue(ToneFontSizeProperty, value); }

    public IBrush SwatchBrush => _swatchBrush;

    public IBrush ToneLabelBrush => _toneLabelBrush;

    /// <summary>The cell color as <c>#RRGGBB</c>.</summary>
    public string Hex => _hex;

    /// <summary>The tone number as text.</summary>
    public string ToneLabel => _toneLabel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty)
        {
            ApplyColor();
            UpdateAutomationName();
        }
        else if (change.Property == ToneProperty)
        {
            ApplyTone();
            UpdateAutomationName();
        }
        else if (change.Property == RowNameProperty)
        {
            UpdateAutomationName();
        }
    }

    private void ApplyColor()
    {
        var color = Color;
        SetAndRaise(SwatchBrushProperty, ref _swatchBrush, new SolidColorBrush(color));
        SetAndRaise(HexProperty, ref _hex, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private void ApplyTone()
    {
        SetAndRaise(ToneLabelProperty, ref _toneLabel, Tone.ToString());
        SetAndRaise(ToneLabelBrushProperty, ref _toneLabelBrush, Tone > 50 ? s_darkLabel : s_lightLabel);
    }

    protected override void OnClick()
    {
        base.OnClick();
        _ = ActivateAsync();
    }

    /// <summary>Copies <see cref="Hex"/> to the platform clipboard.</summary>
    /// <returns><c>true</c> when the clipboard accepted the text.</returns>
    public async Task<bool> CopyColorToClipboardAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return false;
        try
        {
            await clipboard.SetTextAsync(Hex);
            return true;
        }
        catch (Exception)
        {
            // Headless and some platform backends expose a clipboard that refuses writes.
            return false;
        }
    }

    private async Task ActivateAsync()
    {
        var copied = CopyOnClick && await CopyColorToClipboardAsync();
        Activated?.Invoke(this,
            new MaterialTonalSwatchEventArgs(RowName ?? string.Empty, Tone, Color, copied));
    }

    // The automation name is only defaulted while the caller has not supplied one.
    private void UpdateAutomationName()
    {
        if (AutomationProperties.GetName(this) is { Length: > 0 })
            return;
        var prefix = string.IsNullOrEmpty(RowName) ? $"Tone {Tone}" : $"{RowName} {Tone}";
        AutomationProperties.SetName(this, $"{prefix}, {Hex}");
    }
}

/// <summary>
/// Material 3 tonal palette board: labelled rows of tone cells with pointer and keyboard
/// navigation, copy-to-clipboard activation and automation names.
/// </summary>
/// <remarks>
/// <para>
/// This is a design-source visualization, not the pinned <c>TonalPalette.kt</c> theme data.
/// <see cref="MaterialTonalPalettes.Source"/> is the fixed <c>.Tonal palettes</c> board exported
/// from the design source; <see cref="MaterialTonalPalettes.FromSeed"/> is a live Tonal Spot
/// palette computed with MaterialColorUtilities HCT from a seed, which is the same construction
/// <c>MaterialColorScheme</c> uses for its roles. Neither replaces M3's dynamic-color API: Android
/// wallpaper dynamic colors stay an OS service.
/// </para>
/// <para>
/// Set <see cref="Palettes"/> to draw explicit rows, for example
/// <see cref="MaterialTonalPalettes.Source"/> for the exact source board colors, or leave it
/// <c>null</c> to derive live Tonal Spot palettes from <see cref="SeedColor"/>. The theme binds
/// <see cref="SeedColor"/> to <c>MaterialPrimaryColor</c>, so the board follows the active
/// scheme unless a caller overrides it.
/// </para>
/// <para>
/// Every cell is a tab stop. Left/Right move within a row, Up/Down move between rows keeping
/// the column, Home/End jump to the row edges, and PageUp/PageDown jump to the first/last row.
/// Horizontal keys follow <see cref="Visual.FlowDirection"/>.
/// </para>
/// </remarks>
public sealed class MaterialTonalPalette : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<MaterialTonalPaletteRow>?> PalettesProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, IReadOnlyList<MaterialTonalPaletteRow>?>(nameof(Palettes));

    public static readonly StyledProperty<Color> SeedColorProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, Color>(nameof(SeedColor), Color.Parse("#6750A4"));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, string?>(nameof(Title), "Tonal Palettes");

    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, bool>(nameof(ShowTitle), true);

    public static readonly StyledProperty<double> RowLabelWidthProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, double>(nameof(RowLabelWidth), 140,
            validate: value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<double> SwatchWidthProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, double>(nameof(SwatchWidth), 61,
            validate: value => double.IsFinite(value) && value > 0);

    public static readonly StyledProperty<double> SwatchHeightProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, double>(nameof(SwatchHeight), 108,
            validate: value => double.IsFinite(value) && value > 0);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, double>(nameof(RowSpacing), 20,
            validate: value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<double> GroupSpacingProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, double>(nameof(GroupSpacing), 40,
            validate: value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<bool> ShowToneLabelsProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, bool>(nameof(ShowToneLabels), true);

    public static readonly StyledProperty<bool> CopyOnClickProperty =
        AvaloniaProperty.Register<MaterialTonalPalette, bool>(nameof(CopyOnClick), true);

    private readonly List<List<MaterialTonalSwatch>> _rows = [];
    private StackPanel? _root;

    static MaterialTonalPalette()
    {
        PalettesProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        SeedColorProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        TitleProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        ShowTitleProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        RowLabelWidthProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        SwatchWidthProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        SwatchHeightProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        RowSpacingProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        GroupSpacingProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        ShowToneLabelsProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
        CopyOnClickProperty.Changed.AddClassHandler<MaterialTonalPalette>((palette, _) => palette.Rebuild());
    }

    /// <summary>Raised after a cell was activated, with its row, tone, color and copy result.</summary>
    public event EventHandler<MaterialTonalSwatchEventArgs>? SwatchActivated;

    /// <summary>Explicit rows; <c>null</c> derives live palettes from <see cref="SeedColor"/>.</summary>
    public IReadOnlyList<MaterialTonalPaletteRow>? Palettes { get => GetValue(PalettesProperty); set => SetValue(PalettesProperty, value); }

    /// <summary>Seed used when <see cref="Palettes"/> is <c>null</c>.</summary>
    public Color SeedColor { get => GetValue(SeedColorProperty); set => SetValue(SeedColorProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }

    /// <summary>Width of the left label column; the source board uses 140.</summary>
    public double RowLabelWidth { get => GetValue(RowLabelWidthProperty); set => SetValue(RowLabelWidthProperty, value); }

    public double SwatchWidth { get => GetValue(SwatchWidthProperty); set => SetValue(SwatchWidthProperty, value); }

    public double SwatchHeight { get => GetValue(SwatchHeightProperty); set => SetValue(SwatchHeightProperty, value); }

    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>Extra gap above rows whose <see cref="MaterialTonalPaletteRow.StartsGroup"/> is set.</summary>
    public double GroupSpacing { get => GetValue(GroupSpacingProperty); set => SetValue(GroupSpacingProperty, value); }

    public bool ShowToneLabels { get => GetValue(ShowToneLabelsProperty); set => SetValue(ShowToneLabelsProperty, value); }

    public bool CopyOnClick { get => GetValue(CopyOnClickProperty); set => SetValue(CopyOnClickProperty, value); }

    /// <summary>The rows currently displayed, in order.</summary>
    public IReadOnlyList<IReadOnlyList<MaterialTonalSwatch>> Rows => _rows;

    /// <summary>Moves focus to a cell by row and tone index. Returns <c>false</c> when out of range.</summary>
    public bool FocusCell(int row, int column)
    {
        if (row < 0 || row >= _rows.Count || column < 0 || column >= _rows[row].Count)
            return false;
        return _rows[row][column].Focus(NavigationMethod.Tab);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<StackPanel>("PART_Root");
        // Re-template creates a fresh, empty PART_Root, so the rows are always rebuilt.
        Rebuild();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PaletteAutomationPeer(this);

    private void Rebuild()
    {
        // Property changes before the template is applied are picked up by OnApplyTemplate.
        if (_root is null)
            return;

        _root.Children.Clear();
        _root.Spacing = RowSpacing;
        _rows.Clear();

        var rows = Palettes ?? MaterialTonalPalettes.FromSeed(SeedColor);
        if (ShowTitle && !string.IsNullOrEmpty(Title))
        {
            var title = new TextBlock
            {
                Text = Title,
                FontSize = 16,
                FontWeight = FontWeight.Medium,
                // The source board leaves 80 between the title top and the first row: 24 text + 56 gap,
                // of which the panel spacing already contributes RowSpacing (20).
                Margin = new Thickness(RowLabelWidth, 0, 0, 36)
            };
            title[!TextBlock.ForegroundProperty] = this[!ForegroundProperty];
            _root.Children.Add(title);
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions($"{RowLabelWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)},Auto")
            };
            if (rowIndex > 0 && row.StartsGroup)
                grid.Margin = new Thickness(0, GroupSpacing, 0, 0);

            var label = new TextBlock
            {
                Text = row.Name,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 0, 0)
            };
            label[!TextBlock.ForegroundProperty] = this[!ForegroundProperty];
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var host = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(host, 1);
            var cells = new List<MaterialTonalSwatch>(row.Swatches.Count);
            foreach (var swatch in row.Swatches)
            {
                var cell = new MaterialTonalSwatch
                {
                    RowName = row.Name,
                    Tone = swatch.Tone,
                    Color = swatch.Color,
                    Width = SwatchWidth,
                    Height = row.Height ?? SwatchHeight,
                    ShowToneLabel = ShowToneLabels,
                    CopyOnClick = CopyOnClick
                };
                cell.Activated += OnCellActivated;
                cell.KeyDown += OnCellKeyDown;
                host.Children.Add(cell);
                cells.Add(cell);
            }
            grid.Children.Add(host);
            _rows.Add(cells);
            _root.Children.Add(grid);
        }
    }

    private void OnCellActivated(object? sender, MaterialTonalSwatchEventArgs e) =>
        SwatchActivated?.Invoke(this, e);

    private void OnCellKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not MaterialTonalSwatch cell || e.Handled)
            return;

        var (row, column) = IndexOf(cell);
        if (row < 0)
            return;

        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        int targetRow = row, targetColumn = column;
        switch (e.Key)
        {
            case Key.Left:
                targetColumn += rightToLeft ? 1 : -1;
                break;
            case Key.Right:
                targetColumn += rightToLeft ? -1 : 1;
                break;
            case Key.Up:
                targetRow--;
                break;
            case Key.Down:
                targetRow++;
                break;
            case Key.Home:
                targetColumn = 0;
                break;
            case Key.End:
                targetColumn = _rows[row].Count - 1;
                break;
            case Key.PageUp:
                targetRow = 0;
                break;
            case Key.PageDown:
                targetRow = _rows.Count - 1;
                break;
            default:
                return;
        }

        if (targetRow < 0 || targetRow >= _rows.Count)
            return;
        targetColumn = Math.Clamp(targetColumn, 0, _rows[targetRow].Count - 1);
        if (targetRow == row && targetColumn == column)
            return;
        if (_rows[targetRow][targetColumn].Focus(NavigationMethod.Directional))
            e.Handled = true;
    }

    private (int Row, int Column) IndexOf(MaterialTonalSwatch cell)
    {
        for (var row = 0; row < _rows.Count; row++)
        {
            var column = _rows[row].IndexOf(cell);
            if (column >= 0)
                return (row, column);
        }
        return (-1, -1);
    }

    private sealed class PaletteAutomationPeer(MaterialTonalPalette owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(MaterialTonalPalette);

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(owner) is { Length: > 0 } name
                ? name
                : owner.Title ?? base.GetNameCore();
    }
}
