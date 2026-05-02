using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.SampleWebApp.ScrapingDemo;

/// <summary>
/// Defines the artifact keys exchanged between the sample scraping phases.
/// </summary>
/// <remarks>
/// This sample intentionally keeps the artifacts close to the raw phase inputs and outputs so readers can focus on
/// how data moves through the BeltRunner workflow.
/// </remarks>
internal static class ScrapingArtifacts {
    /// <summary>
    /// Gets the artifact key that stores the source URL provided by the operator.
    /// </summary>
    public static ArtifactKey<string> SourceUrl { get; } = ArtifactSeeds.Key<string>("sourceUrl");

    /// <summary>
    /// Gets the artifact key that stores whether the run should simulate an authentication challenge in Phase 1.
    /// </summary>
    public static ArtifactKey<bool> InjectAuthenticationChallenge { get; } = ArtifactSeeds.Key<bool>("injectAuthenticationChallenge");

    /// <summary>
    /// Gets the artifact key that stores whether the run should inject a recoverable anomaly.
    /// </summary>
    public static ArtifactKey<bool> InjectMinorWarning { get; } = ArtifactSeeds.Key<bool>("injectMinorWarning");

    /// <summary>
    /// Gets the artifact key that stores the discovered target page URLs.
    /// </summary>
    public static ListArtifactKey<string> TargetPages { get; } = new(ArtifactName.Create("targetPages"));

    /// <summary>
    /// Gets the artifact key that stores the generated page vectors.
    /// </summary>
    public static ListArtifactKey<double[]> PageVectors { get; } = new(ArtifactName.Create("pageVectors"));

    /// <summary>
    /// Gets the artifact key that stores the final statistics rows.
    /// </summary>
    public static ListArtifactKey<(string Name, string Value, string Description)> StatisticsSummary { get; } = new(ArtifactName.Create("statisticsSummary"));
}
