using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;
using BeltRunner.SampleConsoleApp.Phases;

namespace BeltRunner.SampleConsoleApp;

/// <summary>
/// Provides the console entry point for the beginner BeltRunner sample.
/// </summary>
internal static class Program {
    private static async Task Main(string[] _) {
        
        //  Ref01: Create an artifact list for the initial order data that should be passed to the first phase. 
        IReadOnlyList<OrderData> initialOrders = CreateInitialOrders();
        ListArtifactKey<OrderData> incomingOrdersKey = new(ArtifactName.Create(OrderValidationPhaseFactory.INCOMING_ORDERS));
        IReadOnlyList<IProducedArtifact> initialArtifacts = [
            ArtifactSeeds.Seed(incomingOrdersKey, initialOrders)
        ];

        //  Ref02: Create a plan with three phases' factories. 
        SequentialPlan plan = new SequentialPlan(
            new OrderValidationPhaseFactory(),
            new ItemExpansionPhaseFactory(),
            new PackagePlanningPhaseFactory());

        Console.WriteLine("=== BeltRunner Beginner Sample ===");
        Console.WriteLine($"Seeded orders: {initialOrders.Count}");
        Console.WriteLine();

        //  Ref03: Create a host (without options)
        using IHost host = new Host();
        
        //  Ref04: Create a run by starting the process by the Host. 
        using IRun run = await host.StartAsync(plan, initialArtifacts).ConfigureAwait(false);
        //  Ref05
        using IDisposable progressSubscription = ObserveProgress(run);
        
        //  Ref06: Wait for the finish of the process, and get the outcome. 
        RunOutcome settledOutcome = await run.Completion.ConfigureAwait(false);
        Console.WriteLine();

        //  Ref10: Showing the diagnostic logs
        WriteWarnings(run.DiagnosticLog);

        //  Ref11: Showing the fault if any
        if( settledOutcome is {Kind: RunOutcomeKind.Faulted, FaultInfo: not null} ) {
            Console.WriteLine("Result: Faulted");
            Console.WriteLine($"Fault kind: {settledOutcome.FaultInfo.FaultKind}");
            Console.WriteLine($"Fault message: {settledOutcome.FaultInfo.PublicMessage}");
            return;
        }

        //  Ref12: Showing the status of each phase
        Console.WriteLine("Phase status:");
        foreach( IPhaseSnapshot phase in run.Snapshot.Phases ) {
            int totalUnits = phase.TotalUnits ?? phase.Units.Count;
            Console.WriteLine($"- {phase.PhaseName}: {phase.Status} ({phase.ProcessedUnits}/{totalUnits} units)");
        }

        Console.WriteLine();
        Console.WriteLine("Result:");
        Console.WriteLine($"- Outcome: {settledOutcome.Kind}");
    }

    private static IReadOnlyList<OrderData> CreateInitialOrders() {
        return new List<OrderData> {
            new("O-001", "Ava North", "1 River St", true, 2, false, false),
            new("O-002", "Mia West", "2 Cedar Ave", true, 3, true, false),
            new("O-003", "Eli Stone", string.Empty, true, 2, false, false),
            new("O-004", "Noah Gray", "4 Hill Rd", true, 5, false, false),
            new("O-005", "Ivy Lane", "5 Oak Dr", true, 1, false, false),
            new("O-006", "Leo Park", "6 Pine Ct", true, 4, false, false),
            new("O-007", "Zoe Hart", "7 Lake Blvd", true, 2, false, false),
            new("O-008", "Kai Reed", "8 Harbor Way", true, 6, true, true),
            new("O-009", "Nora Brooks", "9 Meadow Ln", false, 0, false, false),
            new("O-010", "Liam Ford", "10 Elm St", true, 3, false, false)
        };
    }

    private static void WriteWarnings(IReadOnlyList<IDiagnosticEntry> diagnostics) {
        Console.WriteLine("Diagnostic Logs:");
        IReadOnlyList<IDiagnosticEntry> warnings = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if( warnings.Count == 0 )
            return;

        foreach( IDiagnosticEntry warning in warnings ) {
            string phaseLabel = warning.PhaseKey?.ToString() ?? "run";
            Console.WriteLine($"- [{phaseLabel}] {FormatDiagnostic(warning)}");
        }

        Console.WriteLine();
    }

    private static string FormatDiagnostic(IDiagnosticEntry diagnostic) {
        if( diagnostic.FaultInfo is null ) {
            return diagnostic.Message;
        }

        return $"{diagnostic.Message} [fault={diagnostic.FaultInfo.FaultKind}: {diagnostic.FaultInfo.PublicMessage}]";
    }

    private static IDisposable ObserveProgress(IRun run) {
        string? lastLine = null;

        return run.SnapshotStream.Subscribe(snapshot => {
            string? line = FormatProgress(snapshot);
            if( line is null || string.Equals(line, lastLine, StringComparison.Ordinal) ) {
                return;
            }

            lastLine = line;
            Console.WriteLine(line);
        });
    }

    private static string? FormatProgress(IRunSnapshot snapshot) {
        if( snapshot.Status == RunStatus.Created ) {
            return null;
        }

        string runLabel = $"Run {snapshot.Status} ({snapshot.OverallRatio:P0})";
        if( snapshot.CurrentPhaseKey is null ) {
            return $"[Progress] {runLabel}";
        }

        IPhaseSnapshot? currentPhase = snapshot.Phases.FirstOrDefault(phase => phase.PhaseKey == snapshot.CurrentPhaseKey);
        if( currentPhase is null ) {
            return $"[Progress] {runLabel}";
        }

        int totalUnits = currentPhase.TotalUnits ?? currentPhase.Units.Count;
        string phaseLabel = $"{currentPhase.PhaseName} {currentPhase.Status} ({currentPhase.Ratio:P0}, {currentPhase.ProcessedUnits}/{totalUnits} units)";
        IUnitSnapshot? runningUnit = currentPhase.Units.FirstOrDefault(unit => unit.Status == UnitStatus.Running);
        if( runningUnit is null ) {
            return $"[Progress] {runLabel} | {phaseLabel}";
        }

        return $"[Progress] {runLabel} | {phaseLabel} | Unit {runningUnit.Name}: {runningUnit.Ratio:P0} ({runningUnit.Status})";
    }
}
