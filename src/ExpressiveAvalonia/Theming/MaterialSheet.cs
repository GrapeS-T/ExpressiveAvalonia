namespace ExpressiveAvalonia.Theming;

/// <summary>The edge a Material side sheet is docked to.</summary>
/// <remarks>
/// The value is logical: <see cref="Start"/> and <see cref="End"/> follow
/// <see cref="Avalonia.Visual.FlowDirection"/>, so a sheet at <see cref="End"/> appears on the
/// right in left-to-right content and on the left in right-to-left content.
/// </remarks>
public enum MaterialSheetSide { Start, End }
