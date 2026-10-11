using Mapping_Tools.Core.BeatmapHelper;

namespace CrossLineSliderRecalc;

/// <summary>
///     Works out where a slider currently ends and what its length has to be for the tail to end on
///     a wanted time. All of the arithmetic lives here so it can be exercised without the host.
/// </summary>
/// <remarks>
///     A slider's duration comes from the redline and the greenline in effect at its head, even
///     when the slider reaches past a later redline.
/// </remarks>
public static class SliderTailSolver
{
    /// <summary>Times closer together than this count as the same instant.</summary>
    public const double TimeEpsilon = 1e-6;

    /// <summary>
    ///     The time the slider's tail currently ends on, before the file format floors it.
    /// </summary>
    /// <param name="timing">The beatmap timing.</param>
    /// <param name="startTime">The slider head time in milliseconds.</param>
    /// <param name="length">The slider's pixel length.</param>
    /// <param name="slides">The slider's span count, from the <c>.osu</c> slides field.</param>
    /// <returns>The tail time in milliseconds.</returns>
    public static double CurrentEndTime(Timing timing, double startTime, double length, int slides)
    {
        return startTime + timing.CalculateSliderTemporalLength(startTime, length) * slides;
    }

    /// <summary>
    ///     The pixel length that puts the slider's tail exactly on a wanted time.
    /// </summary>
    /// <param name="timing">The beatmap timing.</param>
    /// <param name="startTime">The slider head time in milliseconds.</param>
    /// <param name="targetEndTime">The tail time to reach, in milliseconds.</param>
    /// <param name="slides">The slider's span count, from the <c>.osu</c> slides field.</param>
    /// <returns>The pixel length, without rounding.</returns>
    public static double LengthForEndTime(Timing timing, double startTime, double targetEndTime, int slides)
    {
        return timing.CalculateSliderLength(startTime, (targetEndTime - startTime) / slides);
    }

    /// <summary>
    ///     Finds the grid line nearest to a time, across every grid in
    ///     <paramref name="beatSpacings" />.
    /// </summary>
    /// <param name="timing">The beatmap timing, whose redlines divide the timeline into sections.</param>
    /// <param name="centerTime">The time to search around, in milliseconds.</param>
    /// <param name="beatSpacings">
    ///     How far apart each grid's lines are, counted in beats, once per grid to compare against
    ///     the others. A spacing below one subdivides the beat; above one it skips white lines.
    /// </param>
    /// <param name="mustBeAfter">
    ///     Exclusive lower bound for the result, normally the slider head time.
    /// </param>
    /// <param name="gridTime">The nearest grid line, in milliseconds.</param>
    /// <returns><see langword="true" /> when a grid line was found.</returns>
    /// <remarks>
    ///     <para>
    ///         Each redline covers the section <c>[redline, nextRedline)</c> and the grids inside it.
    ///         The next redline is the first line of its own section, so a line computed from an
    ///         earlier section is never placed past the redline that ended it. Equal distances go to
    ///         the later line.
    ///     </para>
    ///     <para>
    ///         A section is not enumerated line by line. For each grid, three lines from the section
    ///         are considered: the two either side of <paramref name="centerTime" />, and the first
    ///         line past <paramref name="mustBeAfter" />. The redline that starts the section is
    ///         considered separately, because it is the first line of every grid in that section.
    ///     </para>
    /// </remarks>
    public static bool TryFindNearestGridTime(
        Timing timing,
        double centerTime,
        IReadOnlyList<double> beatSpacings,
        double mustBeAfter,
        out double gridTime)
    {
        bool found = false;
        double bestDistance = double.PositiveInfinity;
        double best = 0;

        IReadOnlyList<TimingPoint> redlines = timing.Redlines;
        for (int index = 0; index < redlines.Count; index++)
        {
            double sectionStart = redlines[index].Offset;
            double sectionEnd = index + 1 < redlines.Count
                ? redlines[index + 1].Offset
                : double.PositiveInfinity;

            // The redline is the first line of every grid in its section. Without it a faster
            // section would have no line at all for a tail approaching it from before.
            Consider(sectionStart);

            foreach (double spacing in beatSpacings)
            {
                double step = redlines[index].MpB * spacing;

                // A beat length that gives no usable step means the map is broken.
                if (!double.IsFinite(step) || step <= 0) continue;

                // With the centre inside this section, only the lines either side of it can be the
                // nearest one.
                double position = (centerTime - sectionStart) / step;
                Consider(sectionStart + Math.Floor(position) * step);
                Consider(sectionStart + Math.Ceiling(position) * step);

                // The first line past the lower bound, which the two above miss when the centre is
                // at or behind that bound. Dividing can round to either side of an exact multiple,
                // so the index is moved one line either way until it is the first line past the
                // bound. Moving the index and recomputing keeps the candidate on the grid, where
                // adding a step to the time can give a time that is not a grid line. Such a time
                // lands on the centre and is chosen over every real line.
                double lineIndex = Math.Max(0, Math.Floor((mustBeAfter - sectionStart) / step) + 1);
                double line = sectionStart + lineIndex * step;

                if (line <= mustBeAfter)
                {
                    lineIndex += 1;
                    line = sectionStart + lineIndex * step;
                }
                else if (lineIndex > 0 && sectionStart + (lineIndex - 1) * step > mustBeAfter)
                {
                    lineIndex -= 1;
                    line = sectionStart + lineIndex * step;
                }

                Consider(line);
            }

            void Consider(double candidate)
            {
                // Outside this section, the line belongs to the neighbouring redline's section.
                if (candidate < sectionStart) return;
                if (candidate >= sectionEnd) return;
                if (candidate <= mustBeAfter) return;

                double distance = Math.Abs(candidate - centerTime);
                if (found && distance > bestDistance) return;
                if (found && distance == bestDistance && candidate <= best) return;

                found = true;
                bestDistance = distance;
                best = candidate;
            }
        }

        gridTime = best;
        return found;
    }
}
