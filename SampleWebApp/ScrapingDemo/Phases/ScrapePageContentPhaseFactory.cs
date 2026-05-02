using BeltRunner.Core.Phase;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Builds the phase definition responsible for generating simulated n-gram vectors.
/// </summary>
internal sealed class ScrapePageContentPhaseFactory : PhaseFactoryBase {
    /// <summary>
    /// Initializes a new instance of the <see cref="ScrapePageContentPhaseFactory"/> class.
    /// </summary>
    public ScrapePageContentPhaseFactory() : base(ScrapingPhaseCatalog.SCRAPE_PAGES_KEY) {
        Consume(ScrapingArtifacts.TargetPages);
        Consume(ScrapingArtifacts.InjectMinorWarning);
        Produce(ScrapingArtifacts.PageVectors);
    }

    /// <summary>
    /// Creates the executable phase instance.
    /// </summary>
    /// <returns>The phase that generates page vectors for the current run.</returns>
    public override IPhase Create() {
        return new ScrapePageContentPhase();
    }
}
