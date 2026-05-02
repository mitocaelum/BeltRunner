using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Units;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Simulates parallel vector generation for each discovered page URL.
/// </summary>
internal sealed class ScrapePageContentPhase : IPhase {
    private const int MINOR_WARNING_CHECKPOINT = 61;
    private const string MINOR_WARNING_KIND = "minor-warning-confirm";
    private static readonly int[] progressCheckpoints = [18, 39, 61, 82, 100];

    /// <summary>
    /// Gets the display name shown for the phase.
    /// </summary>
    public string Name => "Scrape Page Content";

    /// <summary>
    /// Gets the unit collection that represents the pages processed by the phase.
    /// </summary>
    public UnitSet Units { get; } = new();

    /// <summary>
    /// Executes the phase and produces simulated vectors for the discovered page URLs.
    /// </summary>
    /// <param name="context">The execution context that provides artifacts, telemetry, and interactions.</param>
    /// <param name="ct">The cancellation token used to stop the simulated work.</param>
    /// <returns>The phase outcome that publishes the generated vectors.</returns>
    public async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
        IReadOnlyList<string> pageUrls = context.Artifacts.GetRequired(ScrapingArtifacts.TargetPages);
        bool injectMinorWarning = context.Artifacts.GetRequired(ScrapingArtifacts.InjectMinorWarning);
        DemoUnit<string>[] units = CreatePageUnits(pageUrls);

        //  Ref60
        using IPhaseProgressTracker progressTracker = context.Telemetry.BeginPhaseProgressTracking(pageUrls.Count);
        context.Telemetry.Info("Phase 2 started building simulated n-gram vectors.");

        int warningUnitIndex = GetMinorWarningUnitIndex(pageUrls.Count, injectMinorWarning);
        if( warningUnitIndex >= 0 ) {
            context.Telemetry.Info($"Phase 2 selected a fixed unit for the recoverable anomaly. unit={units[warningUnitIndex].Name}");
        }

        using CancellationTokenSource phaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        int operatorRequestedStop = 0;
        Task<double[]>[] work = new Task<double[]>[pageUrls.Count];

        for( int i = 0; i < pageUrls.Count; i++ ) {
            int pageIndex = i;
            DemoUnit<string> unit = units[i];
            work[i] = BuildVectorAsync(context, progressTracker, unit, pageIndex, warningUnitIndex, phaseCancellation.Token, () => {
                Interlocked.Exchange(ref operatorRequestedStop, 1);
                phaseCancellation.Cancel();
            });
        }

        double[][] vectors;
        try {
            vectors = await Task.WhenAll(work).ConfigureAwait(false);
        } catch( OperationCanceledException ) when( Volatile.Read(ref operatorRequestedStop) == 1 ) {
            throw new OperationCanceledException("The operator stopped the run after a minor warning in Phase 2.");
        }

        context.Telemetry.Info("Phase 2 finished building simulated n-gram vectors.");
        return new PhaseOutcome().Produce(ScrapingArtifacts.PageVectors, vectors);
    }

    private DemoUnit<string>[] CreatePageUnits(IReadOnlyList<string> pageUrls) {
        DemoUnit<string>[] units = new DemoUnit<string>[pageUrls.Count];

        for( int i = 0; i < pageUrls.Count; i++ ) {
            string title = $"Target Page {i + 1:00}";
            DemoUnit<string> unit = new(pageUrls[i], title);
            units[i] = unit;
        }

        this.Units.AddRangeAndLock(units);
        return units;
    }

    private async Task<double[]> BuildVectorAsync(
        IPhaseContext context,
        IPhaseProgressTracker progressTracker,
        DemoUnit<string> unit,
        int pageIndex,
        int warningUnitIndex,
        CancellationToken ct,
        Action requestStop) {

        //  Ref61
        using ITrackedUnitScope trackedUnit = progressTracker.BeginUnit(unit);

        try {
            await Task.Delay(GetInitialDelay(pageIndex), ct).ConfigureAwait(false);

            for( int i = 0; i < progressCheckpoints.Length; i++ ) {
                int percentage = progressCheckpoints[i];
                if( pageIndex == warningUnitIndex && percentage == MINOR_WARNING_CHECKPOINT ) {
                    bool shouldContinue = await ConfirmMinorWarningAsync(context, unit, percentage, ct).ConfigureAwait(false);
                    if( !shouldContinue ) {
                        requestStop();
                        throw new OperationCanceledException($"The operator stopped the run while reviewing {unit.Name}.");
                    }
                }

                await Task.Delay(GetStepDelay(pageIndex, i), ct).ConfigureAwait(false);
            }

            //  Ref62
            trackedUnit.Complete();
            return BuildVectorValues(pageIndex);
        } catch( OperationCanceledException ) {
            //  Ref63
            context.Telemetry.SetUnitStatus(unit.Id, UnitStatus.Cancelled);
            throw;
        }
    }

    private static async Task<bool> ConfirmMinorWarningAsync(IPhaseContext context, DemoUnit<string> unit, int percentage, CancellationToken ct) {
        context.Telemetry.Warn($"A minor warning was detected. unit={unit.Name} progress={percentage}", unitId: unit.Id);

        //  Ref58
        InteractionRequest<bool> request = new(
            MINOR_WARNING_KIND,
            context.Key,
            title: "Minor warning detected",
            message: $"{unit.Name} reported a recoverable anomaly at {percentage}%. You can continue the run or stop here.");

        bool shouldContinue = await context.Interaction.AskAsync(request, ct).ConfigureAwait(false);
        if( shouldContinue ) {
            context.Telemetry.Info($"The operator chose to continue after the minor warning. unit={unit.Name}", unit.Id);
        } else {
            context.Telemetry.Warn($"The operator chose to stop after the minor warning. unit={unit.Name}", unitId: unit.Id);
        }

        return shouldContinue;
    }

    private static double[] BuildVectorValues(int pageIndex) {
        double seed = pageIndex + 1;
        return [0.12 + (seed * 0.17), 0.34 + (seed * 0.11), 0.56 + (seed * 0.09)];
    }

    private static int GetMinorWarningUnitIndex(int unitCount, bool injectMinorWarning) {
        if( !injectMinorWarning || unitCount == 0 ) {
            return -1;
        }

        // Keep the warning target deterministic so the sample is easier to follow in the UI and README.
        return Math.Min(2, unitCount - 1);
    }

    private static int GetInitialDelay(int pageIndex) {
        return 70 + ((pageIndex * 85) % 420);
    }

    private static int GetStepDelay(int pageIndex, int stepIndex) {
        return 110 + (((pageIndex + 2) * (stepIndex + 3) * 37) % 260);
    }
}
