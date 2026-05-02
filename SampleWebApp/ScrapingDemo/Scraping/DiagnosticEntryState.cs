namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

/// <summary>
/// Represents one diagnostic row shown in the runtime diagnostics panel.
/// </summary>
internal sealed record DiagnosticEntryState(
    DateTimeOffset Timestamp,
    string SeverityLabel,
    string ScopeLabel,
    string Message,
    bool IsWarning,
    bool IsError);
