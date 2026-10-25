namespace ExpressiveAvalonia.Controls;

/// <summary>A Material segmented button: a connected row of outlined, single-select segments.</summary>
/// <remarks>
/// <para>Equivalent to a <see cref="MaterialButtonGroup"/> with
/// <see cref="MaterialButtonGroup.Kind"/> = <see cref="MaterialButtonGroupKind.Segmented"/>,
/// single selection, and the check mark replacing the leading icon of the selected segment,
/// matching the source anatomy.</para>
/// <para>The first segment is selected by default so the control always shows a current choice;
/// assign <see cref="MaterialButtonGroup.SelectedIndex"/> = -1 to start with no selection.
/// <see cref="MaterialButtonGroup.Density"/> maps 0 to -3 onto the 48/44/40/36 DIP heights of the
/// source segmented button.</para>
/// </remarks>
public sealed class MaterialSegmentedButton : MaterialButtonGroup
{
    public MaterialSegmentedButton()
    {
        SetCurrentValue(KindProperty, MaterialButtonGroupKind.Segmented);
        SetCurrentValue(SelectionModeProperty, MaterialButtonGroupSelectionMode.Single);
        SetCurrentValue(ShowSelectedIconProperty, true);
        SetCurrentValue(ReplaceIconWhenSelectedProperty, true);
        SetCurrentValue(SelectedIndexProperty, 0);
    }

    /// <summary>Segmented buttons have no standard or connected anatomy.</summary>
    protected override MaterialButtonGroupKind CoerceKind(MaterialButtonGroupKind value) =>
        MaterialButtonGroupKind.Segmented;
}
