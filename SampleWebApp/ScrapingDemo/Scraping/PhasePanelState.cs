namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

/// <summary>
/// Represents one phase panel shown on the sample screen.
/// </summary>
internal sealed record PhasePanelState(
    string Key,
    string Title,
    bool IsActive,
    bool IsCompleted,
    bool HasWarning,
    bool HasError,
    string? DiagnosticMessage,
    int? TotalUnits,
    int ProcessedUnits,
    int PhasePercentage,
    IReadOnlyList<UnitProgressState> Units);
