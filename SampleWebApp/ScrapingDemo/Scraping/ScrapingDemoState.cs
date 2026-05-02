using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

/// <summary>
/// Provides thread-safe state management for the sample scraping screen.
/// </summary>
/// <remarks>
/// The state uses a few small view-oriented records so the Razor component can read like a walkthrough of the demo
/// instead of a long list of tuple field names.
/// </remarks>
internal sealed class ScrapingDemoState {
    private readonly object gate = new();

    /// <summary>
    /// Occurs after the state has changed.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets a value indicating whether the workflow is running.
    /// </summary>
    public bool IsRunning {
        get {
            lock( this.gate ) {
                return this.isRunning;
            }
        }
    }
    private bool isRunning;

    /// <summary>
    /// Gets the current status message shown to the operator.
    /// </summary>
    public string StatusMessage {
        get {
            lock( this.gate ) {
                return this.statusMessage;
            }
        }
    }
    private string statusMessage = "Enter a URL and run the demo.";

    /// <summary>
    /// Gets the current error message, if one exists.
    /// </summary>
    public string? ErrorMessage {
        get {
            lock( this.gate ) {
                return this.errorMessage;
            }
        }
    }
    private string? errorMessage;

    /// <summary>
    /// Gets the current phase panel data.
    /// </summary>
    public IReadOnlyList<PhasePanelState> Phases {
        get {
            lock( this.gate ) {
                return this.phases;
            }
        }
    }
    private IReadOnlyList<PhasePanelState> phases = ScrapingPhaseCatalog.CreateEmptyPanels();
    private IRunSnapshot? latestSnapshot;
    private IReadOnlyDictionary<string, (DiagnosticSeverity Severity, string Message)> phaseDiagnostics =
        new Dictionary<string, (DiagnosticSeverity Severity, string Message)>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the runtime diagnostics shown in the diagnostics panel.
    /// </summary>
    public IReadOnlyList<DiagnosticEntryState> Diagnostics {
        get {
            lock( this.gate ) {
                return this.diagnostics;
            }
        }
    }
    private IReadOnlyList<DiagnosticEntryState> diagnostics = [];

    /// <summary>
    /// Gets the time when the final result was generated, if a result exists.
    /// </summary>
    public DateTimeOffset? ResultGeneratedAt {
        get {
            lock( this.gate ) {
                return this.resultGeneratedAt;
            }
        }
    }
    private DateTimeOffset? resultGeneratedAt;

    /// <summary>
    /// Gets the total number of target pages represented by the final result.
    /// </summary>
    public int ResultTargetPageCount {
        get {
            lock( this.gate ) {
                return this.resultTargetPageCount;
            }
        }
    }
    private int resultTargetPageCount;

    /// <summary>
    /// Gets the total number of vectors represented by the final result.
    /// </summary>
    public int ResultVectorCount {
        get {
            lock( this.gate ) {
                return this.resultVectorCount;
            }
        }
    }
    private int resultVectorCount;

    /// <summary>
    /// Gets the rows shown in the final result area.
    /// </summary>
    public IReadOnlyList<ResultItemState> ResultItems {
        get {
            lock( this.gate ) {
                return this.resultItems;
            }
        }
    }
    private IReadOnlyList<ResultItemState> resultItems = [];

    /// <summary>
    /// Gets a value indicating whether the log dialog is open.
    /// </summary>
    public bool IsLogDialogOpen {
        get {
            lock( this.gate ) {
                return this.isLogDialogOpen;
            }
        }
    }
    private bool isLogDialogOpen;

    /// <summary>
    /// Gets the log lines currently shown in the log dialog.
    /// </summary>
    public IReadOnlyList<string> LogEntries {
        get {
            lock( this.gate ) {
                return this.logEntries;
            }
        }
    }
    private IReadOnlyList<string> logEntries = [];

    /// <summary>
    /// Gets the active interaction request identifier, if one exists.
    /// </summary>
    public Guid? DialogRequestId {
        get {
            lock( this.gate ) {
                return this.dialogRequestId;
            }
        }
    }
    private Guid? dialogRequestId;

    /// <summary>
    /// Gets the active interaction kind, if one exists.
    /// </summary>
    public string? DialogKind {
        get {
            lock( this.gate ) {
                return this.dialogKind;
            }
        }
    }
    private string? dialogKind;

    /// <summary>
    /// Gets the active interaction title, if one exists.
    /// </summary>
    public string? DialogTitle {
        get {
            lock( this.gate ) {
                return this.dialogTitle;
            }
        }
    }
    private string? dialogTitle;

    /// <summary>
    /// Gets the active interaction message, if one exists.
    /// </summary>
    public string? DialogMessage {
        get {
            lock( this.gate ) {
                return this.dialogMessage;
            }
        }
    }
    private string? dialogMessage;

    /// <summary>
    /// Gets the phase key associated with the active interaction, if one exists.
    /// </summary>
    public string? DialogPhaseKey {
        get {
            lock( this.gate ) {
                return this.dialogPhaseKey;
            }
        }
    }
    private string? dialogPhaseKey;

    /// <summary>
    /// Resets the state so a new workflow run can begin.
    /// </summary>
    public void Reset() {
        Update(() => {
            this.isRunning = true;
            this.statusMessage = "Preparing the demo workflow.";
            this.errorMessage = null;
            this.phases = ScrapingPhaseCatalog.CreateEmptyPanels();
            this.latestSnapshot = null;
            this.phaseDiagnostics = new Dictionary<string, (DiagnosticSeverity Severity, string Message)>(StringComparer.Ordinal);
            this.diagnostics = [];
            this.resultGeneratedAt = null;
            this.resultTargetPageCount = 0;
            this.resultVectorCount = 0;
            this.resultItems = [];
            this.isLogDialogOpen = false;
            ClearDialogFields();
        });
    }

    /// <summary>
    /// Applies the latest BeltRunner run snapshot to the UI state.
    /// </summary>
    /// <param name="snapshot">The immutable run snapshot.</param>
    public void ApplySnapshot(IRunSnapshot snapshot) {
        if( snapshot is null ) throw new ArgumentNullException(nameof(snapshot));

        Update(() => {
            this.isRunning = snapshot.Status is RunStatus.Created or RunStatus.Running or RunStatus.Cancelling;
            this.statusMessage = BuildStatusMessage(snapshot.Status, snapshot.CurrentPhaseName);
            this.errorMessage = null;
            this.latestSnapshot = snapshot;
            this.phases = MapPhases(snapshot.Phases, this.phaseDiagnostics);
        });
    }

    /// <summary>
    /// Applies the latest run-level diagnostics to the phase panels.
    /// </summary>
    /// <param name="diagnostics">The retained diagnostics for the current run.</param>
    public void ApplyDiagnostics(IReadOnlyList<IDiagnosticEntry> diagnostics) {
        if( diagnostics is null ) throw new ArgumentNullException(nameof(diagnostics));

        Update(() => {
            this.phaseDiagnostics = BuildPhaseDiagnostics(diagnostics);
            this.diagnostics = MapDiagnostics(diagnostics);

            if( this.latestSnapshot is not null ) {
                this.phases = MapPhases(this.latestSnapshot.Phases, this.phaseDiagnostics);
            }
        });
    }

    /// <summary>
    /// Applies the latest active interaction list to the UI state.
    /// </summary>
    /// <param name="interactions">The active interaction snapshots for the current run.</param>
    public void ApplyActiveInteractions(IReadOnlyList<IInteractionSnapshot> interactions) {
        if( interactions is null ) throw new ArgumentNullException(nameof(interactions));

        Update(() => {
            if( interactions.Count > 0 ) {
                SetInteractionCore(interactions[0]);

                if( this.isRunning ) {
                    this.statusMessage = "Waiting for operator input.";
                }
            } else {
                ClearDialogFields();
            }
        });
    }

    /// <summary>
    /// Removes the active interaction request from the state.
    /// </summary>
    public void ClearInteraction() {
        Update(ClearDialogFields);
    }

    /// <summary>
    /// Stores the final result shown in the UI.
    /// </summary>
    /// <param name="generatedAt">The time when the result was generated.</param>
    /// <param name="targetPageCount">The number of target pages represented by the result.</param>
    /// <param name="vectorCount">The number of vectors represented by the result.</param>
    /// <param name="items">The statistic rows to display.</param>
    public void SetResult(DateTimeOffset generatedAt, int targetPageCount, int vectorCount, IReadOnlyList<ResultItemState> items) {
        Update(() => {
            this.resultGeneratedAt = generatedAt;
            this.resultTargetPageCount = targetPageCount;
            this.resultVectorCount = vectorCount;
            this.resultItems = items;
        });
    }

    /// <summary>
    /// Opens the log dialog and supplies the current log snapshot.
    /// </summary>
    /// <param name="entries">The log lines to show.</param>
    public void OpenLogDialog(IReadOnlyList<string> entries) {
        if( entries is null ) throw new ArgumentNullException(nameof(entries));

        Update(() => {
            this.logEntries = entries;
            this.isLogDialogOpen = true;
        });
    }

    /// <summary>
    /// Closes the log dialog.
    /// </summary>
    public void CloseLogDialog() {
        Update(() => {
            this.isLogDialogOpen = false;
        });
    }

    /// <summary>
    /// Marks the current run as completed successfully.
    /// </summary>
    /// <param name="statusMessage">The final completion message to display.</param>
    public void CompleteRun(string statusMessage) {
        Update(() => {
            this.isRunning = false;
            this.statusMessage = statusMessage;
            this.errorMessage = null;
            ClearDialogFields();
        });
    }

    /// <summary>
    /// Marks the current run as stopped by the operator.
    /// </summary>
    /// <param name="statusMessage">The final stop message to display.</param>
    public void StopRun(string statusMessage) {
        Update(() => {
            this.isRunning = false;
            this.statusMessage = statusMessage;
            this.errorMessage = null;
            ClearDialogFields();
        });
    }

    /// <summary>
    /// Marks the current run as failed.
    /// </summary>
    /// <param name="errorMessage">The failure message to display.</param>
    public void FailRun(string errorMessage) {
        Update(() => {
            this.isRunning = false;
            this.statusMessage = "The run failed.";
            this.errorMessage = errorMessage;
            ClearDialogFields();
        });
    }

    private void Update(Action updater) {
        Action? changed;

        lock( this.gate ) {
            updater();
            changed = this.Changed;
        }

        changed?.Invoke();
    }

    private void SetInteractionCore(IInteractionSnapshot request) {
        SetInteractionCore(request.RequestId, request.Kind, request.Title, request.Message, request.PhaseKey.ToString());
    }

    private void SetInteractionCore(Guid requestId, string kind, string title, string message, string phaseKey) {
        this.dialogRequestId = requestId;
        this.dialogKind = kind;
        this.dialogTitle = title;
        this.dialogMessage = message;
        this.dialogPhaseKey = phaseKey;
    }

    private void ClearDialogFields() {
        this.dialogRequestId = null;
        this.dialogKind = null;
        this.dialogTitle = null;
        this.dialogMessage = null;
        this.dialogPhaseKey = null;
    }

    private static IReadOnlyList<PhasePanelState> MapPhases(
        IReadOnlyList<IPhaseSnapshot> phases,
        IReadOnlyDictionary<string, (DiagnosticSeverity Severity, string Message)> phaseDiagnostics) {

        if( phases is null || phases.Count == 0 ) {
            return ScrapingPhaseCatalog.CreateEmptyPanels();
        }

        PhasePanelState[] mapped = new PhasePanelState[phases.Count];

        for( int i = 0; i < phases.Count; i++ ) {
            IPhaseSnapshot phase = phases[i];
            string phaseKey = phase.PhaseKey.ToString();
            phaseDiagnostics.TryGetValue(phaseKey, out (DiagnosticSeverity Severity, string Message) diagnostic);
            mapped[i] = new PhasePanelState(
                phaseKey,
                phase.PhaseName,
                phase.Status == PhaseStatus.Running,
                phase.Status == PhaseStatus.Completed,
                diagnostic.Severity == DiagnosticSeverity.Warning,
                diagnostic.Severity == DiagnosticSeverity.Error,
                diagnostic.Message,
                phase.TotalUnits,
                phase.ProcessedUnits,
                MapPercentage(phase.Ratio),
                MapUnits(phase.Units));
        }

        return mapped;
    }

    private static IReadOnlyList<UnitProgressState> MapUnits(IReadOnlyList<IUnitSnapshot> units) {
        UnitProgressState[] mapped = new UnitProgressState[units.Count];

        for( int i = 0; i < units.Count; i++ ) {
            IUnitSnapshot unit = units[i];
            int percentage = MapPercentage(unit.Ratio);
            mapped[i] = new UnitProgressState(unit.Name, percentage);
        }

        return mapped;
    }

    private static int MapPercentage(double ratio) {
        return (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0, MidpointRounding.AwayFromZero);
    }

    private static string BuildStatusMessage(RunStatus status, string? currentPhaseName) {
        return status switch {
            RunStatus.Created => "Preparing the demo workflow.",
            RunStatus.Running when !string.IsNullOrWhiteSpace(currentPhaseName) => $"{currentPhaseName} is running.",
            RunStatus.Cancelling => "Stopping the run...",
            RunStatus.Completed => "The run completed successfully.",
            RunStatus.Cancelled => "The run was stopped.",
            RunStatus.Faulted => "The run failed.",
            _ => "Enter a URL and run the demo."
        };
    }

    private static IReadOnlyList<DiagnosticEntryState> MapDiagnostics(IReadOnlyList<IDiagnosticEntry> diagnostics) {
        DiagnosticEntryState[] mapped = new DiagnosticEntryState[diagnostics.Count];

        for( int i = 0; i < diagnostics.Count; i++ ) {
            IDiagnosticEntry diagnostic = diagnostics[diagnostics.Count - 1 - i];
            mapped[i] = new DiagnosticEntryState(
                diagnostic.Timestamp,
                BuildSeverityLabel(diagnostic.Severity),
                diagnostic.PhaseKey?.ToString() ?? "run",
                BuildDiagnosticMessage(diagnostic),
                diagnostic.Severity == DiagnosticSeverity.Warning,
                diagnostic.Severity == DiagnosticSeverity.Error);
        }

        return mapped;
    }

    private static IReadOnlyDictionary<string, (DiagnosticSeverity Severity, string Message)> BuildPhaseDiagnostics(IReadOnlyList<IDiagnosticEntry> diagnostics) {
        Dictionary<string, (DiagnosticSeverity Severity, string Message)> mapped = new(StringComparer.Ordinal);

        for( int i = 0; i < diagnostics.Count; i++ ) {
            IDiagnosticEntry diagnostic = diagnostics[i];
            if( diagnostic.PhaseKey is null || diagnostic.Severity == DiagnosticSeverity.Information ) {
                continue;
            }

            string phaseKey = diagnostic.PhaseKey.ToString();
            string message = BuildDiagnosticMessage(diagnostic);

            if( mapped.TryGetValue(phaseKey, out (DiagnosticSeverity Severity, string Message) existing) &&
                GetDiagnosticRank(existing.Severity) > GetDiagnosticRank(diagnostic.Severity) ) {
                continue;
            }

            mapped[phaseKey] = (diagnostic.Severity, message);
        }

        return mapped;
    }

    private static string BuildDiagnosticMessage(IDiagnosticEntry diagnostic) {
        if( !string.IsNullOrWhiteSpace(diagnostic.Message) ) {
            return diagnostic.Message;
        }

        if( diagnostic.FaultInfo is not null && !string.IsNullOrWhiteSpace(diagnostic.FaultInfo.PublicMessage) ) {
            return diagnostic.FaultInfo.PublicMessage;
        }

        return "A diagnostic was reported for this phase.";
    }

    private static int GetDiagnosticRank(DiagnosticSeverity severity) {
        return severity switch {
            DiagnosticSeverity.Error => 2,
            DiagnosticSeverity.Warning => 1,
            _ => 0
        };
    }

    private static string BuildSeverityLabel(DiagnosticSeverity severity) {
        return severity switch {
            DiagnosticSeverity.Error => "Error",
            DiagnosticSeverity.Warning => "Warning",
            _ => "Info"
        };
    }
}
