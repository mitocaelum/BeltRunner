using BeltRunner.Core.Phase;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Builds the phase definition responsible for discovering target page URLs from the source URL.
/// </summary>
internal sealed class DiscoverTargetsPhaseFactory : PhaseFactoryBase {
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverTargetsPhaseFactory"/> class.
    /// </summary>
    public DiscoverTargetsPhaseFactory() : base(ScrapingPhaseCatalog.DISCOVER_TARGETS_KEY) {
        Consume(ScrapingArtifacts.SourceUrl);
        Consume(ScrapingArtifacts.InjectAuthenticationChallenge);
        Produce(ScrapingArtifacts.TargetPages);
    }

    /// <summary>
    /// Creates the executable phase instance.
    /// </summary>
    /// <returns>The phase that discovers target page URLs for the current run.</returns>
    public override IPhase Create() {
        return new DiscoverTargetsPhase();
    }
}
