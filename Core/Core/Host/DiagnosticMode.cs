namespace BeltRunner.Core.Host;

/// <summary>
/// Controls which diagnostics the host creates and retains for started runs.
/// </summary>
public enum DiagnosticMode {
    /// <summary>
    /// Diagnostics are disabled.
    /// No diagnostic entries are retained or published for the run.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Only error diagnostics are retained and published.
    /// </summary>
    ErrorsOnly,

    /// <summary>
    /// Information, warning, and error diagnostics are retained and published.
    /// </summary>
    All
}
