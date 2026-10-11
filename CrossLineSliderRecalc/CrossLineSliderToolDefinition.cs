using Mapping_Tools.Application.QuickRun.Models;
using Mapping_Tools.Application.Tools;

namespace CrossLineSliderRecalc;

/// <summary>
///     Stable metadata for the Cross-line slider recalc plugin.
/// </summary>
public static class CrossLineSliderToolDefinition
{
    /// <summary>
    ///     Gets the tool metadata used by the shell and the QuickRun catalogs.
    /// </summary>
    /// <remarks>
    ///     QuickRun is offered for a single selected object, because that is the only live
    ///     selection this tool can act on.
    /// </remarks>
    public static ToolDefinition Definition { get; } = new(
        "cross-line-slider-recalc",
        "Cross-line slider recalc",
        "Moves the tail of the slider selected in the osu! editor onto a grid line or a manually "
        + "entered time, by changing its length.",
        ["cross", "line", "slider", "recalc", "length", "tail", "snap", "grid"],
        QuickRunTargets.SingleSelection);
}
