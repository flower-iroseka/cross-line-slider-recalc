using Mapping_Tools.Application.BeatmapEditing;
using Mapping_Tools.Application.BeatmapEditing.Contracts;
using Mapping_Tools.Application.BeatmapEditing.Models;
using Mapping_Tools.Application.Settings.Models;
using Mapping_Tools.Core.BeatmapHelper;

namespace CrossLineSliderRecalc;

/// <summary>
///     Recalculates the length of the slider selected in the osu! editor and saves it through
///     Mapping Tools' editing gateway, so backups and the editor reload behave like every
///     built-in tool.
/// </summary>
public sealed class CrossLineSliderService
{
    private readonly ApplicationSettings settings;
    private readonly IBeatmapEditingGateway editingGateway;

    /// <summary>
    ///     Creates the service.
    /// </summary>
    /// <param name="editingGateway">Opens live editor state and saves with backup protection.</param>
    /// <param name="settings">Supplies the automatic editor reload preference.</param>
    public CrossLineSliderService(
        IBeatmapEditingGateway editingGateway,
        ApplicationSettings settings)
    {
        this.editingGateway = editingGateway ?? throw new ArgumentNullException(nameof(editingGateway));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    ///     Moves the selected slider's tail onto the requested time.
    /// </summary>
    /// <param name="path">The beatmap the osu! editor has open.</param>
    /// <param name="request">The alignment mode and its settings.</param>
    /// <param name="quickRun">Whether this came from QuickRun, which is the only path that reloads osu!.</param>
    /// <param name="cancellationToken">Cancels the open, the save, or a requested reload.</param>
    /// <returns>What the run did.</returns>
    /// <exception cref="LiveBeatmapUnavailableException">
    ///     osu! is not running, has no beatmap open, or has a different beatmap open.
    /// </exception>
    /// <exception cref="InvalidOperationException">The editor's selection is not exactly one slider.</exception>
    /// <remarks>
    ///     The selection is the editor's, so this reads live state rather than the file on disk.
    /// </remarks>
    public async Task<SliderRecalcOutcome> RecalculateAsync(
        string path,
        SliderRecalcRequest request,
        bool quickRun,
        CancellationToken cancellationToken)
    {
        BeatmapEditingSession session = await editingGateway
            .OpenBeatmapAsync(path, LiveBeatmapPreference.RequireLive, cancellationToken)
            .ConfigureAwait(false);

        HitObject slider = FindSelectedSlider(session.SelectedHitObjects);
        Timing timing = session.Beatmap.BeatmapTiming;

        double startTime = slider.Time;
        int slides = slider.Repeat;

        if (slides < 1)
            throw new InvalidOperationException(
                "The selected slider reports no slides, so its length cannot be recalculated.");

        double oldEndTime = SliderTailSolver.CurrentEndTime(timing, startTime, slider.PixelLength, slides);

        // A zero or unreadable length, or a slider multiplier of zero in the timing, gives a tail
        // time that cannot be used.
        if (!double.IsFinite(oldEndTime))
            throw new InvalidOperationException(
                "The selected slider's current length does not convert to a usable tail time. "
                + "Its length and the beatmap's slider multiplier are the things to check.");

        double targetEndTime = request.Mode == TailAlignmentMode.Grid
            ? FindGridTime(timing, oldEndTime, request.BeatSpacings, startTime)
            : request.CustomEndTime;

        if (targetEndTime <= startTime)
            throw new InvalidOperationException(
                $"The wanted tail time {Format(targetEndTime)} is not after the slider head at {Format(startTime)}.");

        if (Math.Abs(targetEndTime - oldEndTime) <= SliderTailSolver.TimeEpsilon)
        {
            return new SliderRecalcOutcome(
                session.Path, startTime, oldEndTime, oldEndTime, slider.PixelLength, AlreadyAligned: true);
        }

        double newLength = SliderTailSolver.LengthForEndTime(timing, startTime, targetEndTime, slides);
        if (!double.IsFinite(newLength) || newLength <= 0)
            throw new InvalidOperationException(
                $"The recalculated slider length came out as {newLength.ToInvariant()}, which is not usable.");

        slider.PixelLength = newLength;
        slider.CalculateSliderTemporalLength(timing, useOwnSv: false);

        await editingGateway
            .SaveAsync(
                session,
                AutomaticEditorReloadPolicy.ShouldReloadEditor(session, quickRun, settings),
                cancellationToken)
            .ConfigureAwait(false);

        return new SliderRecalcOutcome(
            session.Path, startTime, oldEndTime, targetEndTime, newLength, AlreadyAligned: false);
    }

    private static HitObject FindSelectedSlider(IReadOnlyList<HitObject> selected)
    {
        if (selected.Count == 0)
            throw new InvalidOperationException(
                "Nothing is selected in the osu! editor. The tool needs exactly one slider.");

        if (selected.Count > 1)
            throw new InvalidOperationException(
                $"{selected.Count} objects are selected in the osu! editor. The tool needs exactly one slider.");

        HitObject candidate = selected[0];
        if (!candidate.IsSlider)
            throw new InvalidOperationException(
                "The object selected in the osu! editor is not a slider.");

        return candidate;
    }

    private static double FindGridTime(
        Timing timing,
        double endTime,
        IReadOnlyList<double> spacings,
        double startTime)
    {
        if (!SliderTailSolver.TryFindNearestGridTime(
                timing, endTime, spacings, startTime, out double gridTime))
        {
            string wanted = string.Join(", ", spacings.Select(spacing => spacing.ToInvariant()));
            throw new InvalidOperationException(
                $"No grid line was found after the slider head at {Format(startTime)} "
                + $"for the spacings {wanted} beats. "
                + "The beatmap may have no redline before the slider.");
        }

        return gridTime;
    }

    private static string Format(double milliseconds)
    {
        // The editor's own mm:ss:mmm form, so messages read like the timestamps users copy out of it.
        long total = (long)Math.Floor(milliseconds);
        long minutes = total / 60000;
        long seconds = total / 1000 % 60;
        long thousandths = total % 1000;
        return FormattableString.Invariant(
            $"{minutes:00}:{seconds:00}:{thousandths:000} ({milliseconds} ms)");
    }
}
