using BeltRunner.Core.Phase;
using BeltRunner.Core.Units;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Simulates the generation of summary statistics from the previously built page vectors.
/// </summary>
internal sealed class BuildLanguageStatisticsPhase : IPhase {
    private static readonly (string Key, string DisplayName)[] Tasks = [
        ("vocabularySpread", "Vocabulary Spread"),
        ("pageSimilarity", "Cross-Page Similarity"),
        ("topNgramTrend", "Top n-Gram Trend")
    ];

    /// <summary>
    /// Gets the display name shown for the phase.
    /// </summary>
    public string Name => "Generate Statistics";

    /// <summary>
    /// Gets the unit collection that represents the statistic tasks handled by the phase.
    /// </summary>
    public UnitSet Units { get; } = new();

    /// <summary>
    /// Executes the phase and produces the final statistics summary rows.
    /// </summary>
    /// <param name="context">The execution context that provides artifacts, telemetry, and interactions.</param>
    /// <param name="ct">The cancellation token used to stop the simulated work.</param>
    /// <returns>The phase outcome that publishes the final statistics summary rows.</returns>
    public async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
        IReadOnlyList<double[]> vectors = context.Artifacts.GetRequired(ScrapingArtifacts.PageVectors);
        DemoUnit<string>[] units = CreateTaskUnits();

        using IPhaseProgressTracker progressTracker = context.Telemetry.BeginPhaseProgressTracking(Tasks.Length);
        context.Telemetry.Info("Phase 3 started building the statistics summary.");

        List<(string Name, string Value, string Description)> items = new(Tasks.Length);

        for( int i = 0; i < Tasks.Length; i++ ) {
            (string Key, string DisplayName) task = Tasks[i];
            DemoUnit<string> unit = units[i];
            await SimulateUnitAsync(context, progressTracker, unit, ct).ConfigureAwait(false);
            items.Add(CreateItem(task.Key, task.DisplayName, vectors.Count));
        }

        context.Telemetry.Info("Phase 3 finished building the statistics summary.");
        return new PhaseOutcome().Produce(ScrapingArtifacts.StatisticsSummary, items);
    }

    private DemoUnit<string>[] CreateTaskUnits() {
        DemoUnit<string>[] units = new DemoUnit<string>[Tasks.Length];

        for( int i = 0; i < Tasks.Length; i++ ) {
            string displayName = Tasks[i].DisplayName;
            DemoUnit<string> unit = new(displayName, displayName);
            units[i] = unit;
        }

        this.Units.AddRangeAndLock(units);
        return units;
    }

    private static (string Name, string Value, string Description) CreateItem(string key, string displayName, int vectorCount) {
        return key switch {
            "vocabularySpread" => (displayName, "0.73", $"Illustrative value. Shows how vocabulary spread might be summarized from {vectorCount} vectors."),
            "pageSimilarity" => (displayName, "0.58", $"Illustrative value. Shows how average similarity might be summarized across {vectorCount} pages."),
            _ => (displayName, "tri-gram > bi-gram > uni-gram", "Illustrative value. Shows how the dominant n-gram trend might be summarized in prose.")
        };
    }

    private static async Task SimulateUnitAsync(IPhaseContext context, IPhaseProgressTracker progressTracker, DemoUnit<string> unit, CancellationToken ct) {
        int[] checkpoints = [25, 50, 80, 100];
        using ITrackedUnitScope trackedUnit = progressTracker.BeginUnit(unit);

        try {
            for( int i = 0; i < checkpoints.Length; i++ ) {
                int percentage = checkpoints[i];
                context.Telemetry.ReportUnitProgress(unit.Id, percentage / 100d);
                await Task.Delay(170, ct).ConfigureAwait(false);
            }

            trackedUnit.Complete();
        } catch( OperationCanceledException ) {
            context.Telemetry.SetUnitStatus(unit.Id, UnitStatus.Cancelled);
            throw;
        }
    }
}
