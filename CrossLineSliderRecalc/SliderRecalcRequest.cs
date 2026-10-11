namespace CrossLineSliderRecalc;

/// <summary>Where the wanted tail time comes from.</summary>
public enum TailAlignmentMode
{
    /// <summary>Snap to the nearest grid line.</summary>
    Grid,

    /// <summary>Take a time the user typed.</summary>
    Custom
}

/// <summary>
///     One request to move the tail of the slider that is selected in the osu! editor.
/// </summary>
/// <param name="Mode">Where the wanted tail time comes from.</param>
/// <param name="BeatSpacings">
///     The grids to snap to, used in grid mode only, each entry being how far apart that grid's
///     lines are in beats. A preset has one entry; the custom option has everything it was given,
///     and the nearest line of any of them is the one used.
/// </param>
/// <param name="CustomEndTime">The wanted tail time in milliseconds, used in custom mode only.</param>
public sealed record SliderRecalcRequest(
    TailAlignmentMode Mode,
    IReadOnlyList<double> BeatSpacings,
    double CustomEndTime);

/// <summary>
///     What one run did to the beatmap.
/// </summary>
/// <param name="BeatmapPath">The beatmap that was written.</param>
/// <param name="StartTime">The slider head time, in milliseconds.</param>
/// <param name="OldEndTime">The tail time before the run.</param>
/// <param name="NewEndTime">The tail time the length now produces.</param>
/// <param name="NewLength">The slider's new pixel length.</param>
/// <param name="AlreadyAligned">Whether the tail was already on the wanted time, so nothing was written.</param>
public sealed record SliderRecalcOutcome(
    string BeatmapPath,
    double StartTime,
    double OldEndTime,
    double NewEndTime,
    double NewLength,
    bool AlreadyAligned);
