using BeltRunner.Core.Phase;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Builds the phase definition responsible for producing the final language statistics summary.
/// </summary>
internal sealed class BuildLanguageStatisticsPhaseFactory : PhaseFactoryBase {
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildLanguageStatisticsPhaseFactory"/> class.
    /// </summary>
    public BuildLanguageStatisticsPhaseFactory() : base(ScrapingPhaseCatalog.BUILD_STATISTICS_KEY) {
        Consume(ScrapingArtifacts.PageVectors);
        Produce(ScrapingArtifacts.StatisticsSummary);
    }

    /// <summary>
    /// Creates the executable phase instance.
    /// </summary>
    /// <returns>The phase that generates the final statistics summary for the current run.</returns>
    public override IPhase Create() {
        return new BuildLanguageStatisticsPhase();
    }
}
