using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapping_Tools.Application.Execution.ToolExecution;
using Mapping_Tools.Application.Execution.ToolExecution.Models;
using Mapping_Tools.Application.Workspace.Contracts;
using Mapping_Tools.Desktop.Localization;
using Mapping_Tools.Desktop.Services.Dialogs;
using Mapping_Tools.Desktop.Shell;
using Mapping_Tools.Desktop.ViewModels;

namespace CrossLineSliderRecalc;

/// <summary>
///     Moves the tail of the slider selected in the osu! editor onto a grid line or a typed time.
/// </summary>
public sealed partial class CrossLineSliderToolViewModel : SingleRunToolViewModel, IQuickRun
{
    /// <summary>The divider index that means "use the typed list instead of a preset".</summary>
    private const int DivisionCustom = 3;

    private readonly IDialogService dialogs;
    private readonly CrossLineSliderService service;
    private readonly IBeatmapWorkspace workspace;

    /// <summary>
    ///     Creates the tool's presentation model.
    /// </summary>
    public CrossLineSliderToolViewModel(
        CrossLineSliderService service,
        IToolExecutionService execution,
        IBeatmapWorkspace workspace,
        IDialogService dialogs)
        : base(execution, CrossLineSliderToolDefinition.Definition)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RecomputeForm();
    }

    /// <summary>Gets or sets the selected entry of the alignment mode box.</summary>
    [ObservableProperty]
    public partial int AlignmentModeIndex { get; set; }

    /// <summary>Gets or sets the selected divider: 0 is 1/1, 1 is 1/2, 2 is 1/4, 3 is custom.</summary>
    [ObservableProperty]
    public partial int SelectedDivisionIndex { get; set; }

    /// <summary>Gets or sets the beat spacings the custom option uses, as typed.</summary>
    [ObservableProperty]
    public partial string CustomDivisionsText { get; set; } = "1/12, 1/16";

    /// <summary>Gets or sets the wanted tail time in custom mode, as typed.</summary>
    [ObservableProperty]
    public partial string CustomTimeText { get; set; } = string.Empty;

    /// <summary>Gets the reason the form cannot be run right now, or an empty string.</summary>
    [ObservableProperty]
    public partial string FormHint { get; set; } = string.Empty;

    /// <summary>Gets whether the form is filled in well enough to run.</summary>
    [ObservableProperty]
    public partial bool CanRunExtra { get; set; }

    /// <summary>Gets whether the mode is "nearest 1/n grid line".</summary>
    public bool IsGridMode => AlignmentModeIndex == (int)TailAlignmentMode.Grid;

    /// <summary>Gets whether the mode is "custom end time".</summary>
    public bool IsCustomMode => !IsGridMode;

    /// <summary>Gets whether the divider row has the custom option selected.</summary>
    public bool IsCustomDivisionMode => IsGridMode && SelectedDivisionIndex == DivisionCustom;

    /// <summary>Gets whether the Recalculate button can be pressed right now.</summary>
    public bool CanPressRun => CanRunExtra && !IsRunning;

    /// <summary>
    ///     Gets the hint shown on the n box.
    /// </summary>
    /// <remarks>
    ///     This is Map Cleaner's own wording for its beat-divisor box, read from the host rather
    ///     than retyped, so the two match in every language the host ships and follow the host if
    ///     it is ever reworded. Reading it from C# rather than looking it up in XAML means a
    ///     renamed key breaks the build instead of throwing as the page loads.
    /// </remarks>
    public string DivisionsHint => DesktopStrings.MapCleaner_BeatDivisorsTip;

    // The four divider buttons bind to these. A click writes true to the one clicked and false to
    // the rest, so only the true counts. They are nullable because that is the type a toggle
    // button's checked state has.

    /// <summary>Gets or sets whether the 1/1 divider is the selected one.</summary>
    public bool? IsDivision1
    {
        get => SelectedDivisionIndex == 0;
        set { if (value == true) SelectedDivisionIndex = 0; }
    }

    /// <summary>Gets or sets whether the 1/2 divider is the selected one.</summary>
    public bool? IsDivision2
    {
        get => SelectedDivisionIndex == 1;
        set { if (value == true) SelectedDivisionIndex = 1; }
    }

    /// <summary>Gets or sets whether the 1/4 divider is the selected one.</summary>
    public bool? IsDivision4
    {
        get => SelectedDivisionIndex == 2;
        set { if (value == true) SelectedDivisionIndex = 2; }
    }

    /// <summary>Gets or sets whether the custom divider is the selected one.</summary>
    public bool? IsDivisionCustom
    {
        get => SelectedDivisionIndex == DivisionCustom;
        set { if (value == true) SelectedDivisionIndex = DivisionCustom; }
    }

    /// <inheritdoc />
    public Task RunQuickAsync(CancellationToken cancellationToken)
    {
        return RunWithStateAsync(() => RunAsync(true, cancellationToken));
    }

    /// <inheritdoc />
    protected override Task RunCoreAsync()
    {
        return RunAsync(false, CancellationToken.None);
    }

    private async Task RunAsync(bool quick, CancellationToken cancellationToken)
    {
        SliderRecalcRequest? request = TryBuildRequest(out string formError);
        if (request is null)
        {
            await ShowMessageAsync(formError).ConfigureAwait(true);
            return;
        }

        // The selection lives in the editor, so the beatmap open there is the one to act on.
        string path = await workspace
            .ResolveQuickRunBeatmapAsync(updateSelection: false, cancellationToken)
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(path))
        {
            await ShowMessageAsync("No beatmap is selected in Mapping Tools.").ConfigureAwait(true);
            return;
        }

        await Execution.ExecuteAsync(
            new ToolExecutionRequest<string>(
                Tool.Id,
                Tool.DisplayName,
                async context =>
                {
                    context.ReportProgress(0.5, "Recalculating the slider");
                    SliderRecalcOutcome outcome = await service
                        .RecalculateAsync(path, request, quick, context.CancellationToken)
                        .ConfigureAwait(false);
                    context.ReportProgress(1, "Saved");
                    return new ToolExecutionOutput<string>(path, Summarize(outcome));
                }),
            CreateProgress(),
            cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    ///     Builds the request the form asks for, or reports the one thing wrong with it.
    /// </summary>
    /// <param name="error">The reason there is no request, or an empty string.</param>
    /// <returns>The request, or <see langword="null" /> when the form cannot be run.</returns>
    private SliderRecalcRequest? TryBuildRequest(out string error)
    {
        error = string.Empty;

        if (IsCustomMode)
        {
            if (CustomTimeText.Trim().Length == 0)
            {
                error = "No target time entered.";
                return null;
            }

            if (!TargetTimeParser.TryParse(CustomTimeText, out double endTime))
            {
                error = "The target time is neither a millisecond count nor an mm:ss:mmm timestamp.";
                return null;
            }

            return new SliderRecalcRequest(TailAlignmentMode.Custom, [], endTime);
        }

        if (!TryGetSpacings(out IReadOnlyList<double> spacings))
        {
            error = "The n list is not a comma-separated list of distances in beats, such as 1/4, 1.5, 2.";
            return null;
        }

        return new SliderRecalcRequest(TailAlignmentMode.Grid, spacings, 0);
    }

    /// <summary>Gets the grids the selected divider stands for.</summary>
    private bool TryGetSpacings(out IReadOnlyList<double> spacings)
    {
        switch (SelectedDivisionIndex)
        {
            case 1:
                spacings = [0.5];
                return true;
            case 2:
                spacings = [0.25];
                return true;
            case DivisionCustom:
                return BeatSpacingParser.TryParse(CustomDivisionsText, out spacings);
            default:
                spacings = [1];
                return true;
        }
    }

    private static string Summarize(SliderRecalcOutcome outcome)
    {
        if (outcome.AlreadyAligned)
            return $"The slider tail is already on {Format(outcome.NewEndTime)}.";

        return $"Slider tail {Format(outcome.OldEndTime)} to {Format(outcome.NewEndTime)}, "
               + FormattableString.Invariant($"length {outcome.NewLength} px.");
    }

    private static string Format(double milliseconds)
    {
        long total = (long)Math.Floor(milliseconds);
        long minutes = total / 60000;
        long seconds = total / 1000 % 60;
        long thousandths = total % 1000;
        return FormattableString.Invariant($"{minutes:00}:{seconds:00}:{thousandths:000}");
    }

    private void RecomputeForm()
    {
        // Building the request is also the check, and the reason it failed is the hint. That covers
        // an empty box as well as one that cannot be read.
        CanRunExtra = TryBuildRequest(out string reason) is not null;
        FormHint = reason;
    }

    private Task ShowMessageAsync(string message)
    {
        return dialogs.ShowMessageAsync(
            new MessageDialogRequest<bool>(
                CrossLineSliderToolDefinition.Definition.DisplayName,
                message,
                [new DialogChoice<bool>("OK", true, IsDefault: true, IsCancel: true)],
                true));
    }

    partial void OnAlignmentModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGridMode));
        OnPropertyChanged(nameof(IsCustomMode));
        OnPropertyChanged(nameof(IsCustomDivisionMode));
        RecomputeForm();
    }

    partial void OnSelectedDivisionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsDivision1));
        OnPropertyChanged(nameof(IsDivision2));
        OnPropertyChanged(nameof(IsDivision4));
        OnPropertyChanged(nameof(IsDivisionCustom));
        OnPropertyChanged(nameof(IsCustomDivisionMode));
        RecomputeForm();
    }

    partial void OnCustomDivisionsTextChanged(string value)
    {
        RecomputeForm();
    }

    partial void OnCustomTimeTextChanged(string value)
    {
        RecomputeForm();
    }

    partial void OnCanRunExtraChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPressRun));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsRunning)) OnPropertyChanged(nameof(CanPressRun));
    }
}
