namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

/// <summary>
/// Provides the phase identifiers and default panel layout for the sample scraping workflow.
/// </summary>
internal static class ScrapingPhaseCatalog {
    /// <summary>
    /// Identifies the phase that derives target pages from the source URL.
    /// </summary>
    public const string DISCOVER_TARGETS_KEY = "discoverTargets";

    /// <summary>
    /// Identifies the phase that builds simulated n-gram vectors.
    /// </summary>
    public const string SCRAPE_PAGES_KEY = "scrapePages";

    /// <summary>
    /// Identifies the phase that produces the final statistics summary.
    /// </summary>
    public const string BUILD_STATISTICS_KEY = "buildStatistics";

    /// <summary>
    /// Creates the default phase panel data used before a run begins.
    /// </summary>
    /// <returns>The initial phase panel data.</returns>
    public static IReadOnlyList<PhasePanelState> CreateEmptyPanels() {
        return [
            new PhasePanelState(DISCOVER_TARGETS_KEY, "Discover Target Pages", false, false, false, false, null, null, 0, 0, []),
            new PhasePanelState(SCRAPE_PAGES_KEY, "Scrape Page Content", false, false, false, false, null, null, 0, 0, []),
            new PhasePanelState(BUILD_STATISTICS_KEY, "Generate Statistics", false, false, false, false, null, null, 0, 0, [])
        ];
    }
}
