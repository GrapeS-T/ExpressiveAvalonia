using Avalonia.Controls;

namespace ExpressiveAvalonia.Controls;

/// <summary>
/// One day inside a <see cref="MaterialDatePicker"/> grid. The picker owns the state:
/// selection, today, out-of-month and range pseudo-classes are applied by the picker so the
/// control theme can style every combination without duplicating layout in code.
/// </summary>
/// <remarks>
/// Cells are focusable but not individual tab stops. The picker is the single tab stop and
/// forwards focus to the active cell (roving focus), which keeps Tab navigation usable while
/// still letting pointer and screen-reader users activate any day.
/// </remarks>
public sealed class MaterialDateCell : Button
{
    public MaterialDateCell()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
    }

    /// <summary>The month block this cell belongs to. Leading and trailing days are dimmed.</summary>
    internal DateTime ContextMonth { get; set; }

    internal void SetState(string name, bool value) => PseudoClasses.Set(name, value);
}

internal interface IMaterialDateChoiceCell
{
    void SetSelected(bool selected);
}

/// <summary>A month or year chip used by the month and year views of a date picker.</summary>
public sealed class MaterialDateChoiceChip : Button, IMaterialDateChoiceCell
{
    public MaterialDateChoiceChip()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
    }

    void IMaterialDateChoiceCell.SetSelected(bool selected) => PseudoClasses.Set(":selected", selected);
}

/// <summary>A full-width month or year row used by the docked (desktop) date picker list.</summary>
public sealed class MaterialDateChoiceRow : Button, IMaterialDateChoiceCell
{
    public MaterialDateChoiceRow()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
    }

    void IMaterialDateChoiceCell.SetSelected(bool selected) => PseudoClasses.Set(":selected", selected);
}
