using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Host;

internal sealed class DefaultSequentialPlanExecutor {
    private const string PREFLIGHT_NULL_FACTORY_MESSAGE = "Sequential plan contains a null factory.";
    private const string PREFLIGHT_NULL_CONSUMES_MESSAGE = "Consumes list is null.";
    private const string PREFLIGHT_NULL_PRODUCES_MESSAGE = "Produces list is null.";
    private const string PREFLIGHT_CONTAINS_NULL_KEY_MESSAGE = "Artifact key list contains null.";
    private const string PREFLIGHT_DUPLICATE_PHASEKEY_MESSAGE = "Duplicate PhaseKey was found in the sequential plan.";
    private const string PREFLIGHT_INITIAL_ARTIFACT_NULL_MESSAGE = "Initial artifacts contains null.";
    private const string PREFLIGHT_INITIAL_ARTIFACT_KEY_NULL_MESSAGE = "Initial artifact Key is null.";
    private const string PREFLIGHT_INITIAL_ARTIFACT_NOT_DECLARED_MESSAGE = "Initial artifact key is not declared by any phase.";
    private const string PREFLIGHT_MISSING_BEFORE_PHASE_MESSAGE = "Preflight failed: missing required artifact before phase start.";

    public void Preflight(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts) {
        if( plan is null ) throw new ArgumentNullException(nameof(plan));
        if( initialArtifacts is null ) throw new ArgumentNullException(nameof(initialArtifacts));

        IReadOnlyList<IPhaseFactory> factories = plan.Steps.Select(x => x.Factory).ToArray();

        HashSet<PhaseKey> phaseKeySet = new();
        Dictionary<string, Type> nameToType = new(StringComparer.Ordinal);
        HashSet<IArtifactKey> declared = new(ArtifactKeySignatureComparer.Instance);

        for( int i = 0; i < factories.Count; i++ ) {
            IPhaseFactory factory = factories[i] ?? throw new InvalidOperationException(PREFLIGHT_NULL_FACTORY_MESSAGE);

            PhaseKey phaseKey = factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");
            if( !phaseKeySet.Add(phaseKey) )
                throw new InvalidOperationException($"{PREFLIGHT_DUPLICATE_PHASEKEY_MESSAGE} phaseKey=\"{phaseKey}\"");

            IReadOnlyList<IArtifactKey> consumes = factory.Consumes ?? throw new InvalidOperationException($"{PREFLIGHT_NULL_CONSUMES_MESSAGE} phaseKey=\"{phaseKey}\"");
            IReadOnlyList<IArtifactKey> produces = factory.Produces ?? throw new InvalidOperationException($"{PREFLIGHT_NULL_PRODUCES_MESSAGE} phaseKey=\"{phaseKey}\"");

            RegisterKeys(consumes, declared, nameToType, $"phaseConsumes phaseKey=\"{phaseKey}\"");
            RegisterKeys(produces, declared, nameToType, $"phaseProduces phaseKey=\"{phaseKey}\"");
        }

        HashSet<IArtifactKey> initialKeys = new(ArtifactKeySignatureComparer.Instance);

        for( int i = 0; i < initialArtifacts.Count; i++ ) {
            IProducedArtifact item = initialArtifacts[i] ?? throw new InvalidOperationException(PREFLIGHT_INITIAL_ARTIFACT_NULL_MESSAGE);
            IArtifactKey key = item.Key ?? throw new InvalidOperationException(PREFLIGHT_INITIAL_ARTIFACT_KEY_NULL_MESSAGE);

            RegisterKeyNameType(key, nameToType, "initialArtifacts");

            if( !initialKeys.Add(key) ) {
                throw new InvalidOperationException($"Duplicate initial artifact key signature was provided. key=\"{key.Name}\" type=\"{key.ValueType.FullName}\"");
            }

            if( !declared.Contains(key) ) {
                throw new InvalidOperationException($"{PREFLIGHT_INITIAL_ARTIFACT_NOT_DECLARED_MESSAGE} key=\"{key.Name}\" type=\"{key.ValueType.FullName}\"");
            }
        }

        HashSet<IArtifactKey> available = new(ArtifactKeySignatureComparer.Instance);
        foreach( IArtifactKey key in initialKeys )
            available.Add(key);

        for( int i = 0; i < factories.Count; i++ ) {
            IPhaseFactory factory = factories[i] ?? throw new InvalidOperationException(PREFLIGHT_NULL_FACTORY_MESSAGE);
            PhaseKey phaseKey = factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");

            IReadOnlyList<IArtifactKey> consumes = factory.Consumes ?? throw new InvalidOperationException($"{PREFLIGHT_NULL_CONSUMES_MESSAGE} phaseKey=\"{phaseKey}\"");
            for( int c = 0; c < consumes.Count; c++ ) {
                IArtifactKey key = consumes[c] ?? throw new InvalidOperationException($"{PREFLIGHT_CONTAINS_NULL_KEY_MESSAGE} phaseKey=\"{phaseKey}\"");

                if( !available.Contains(key) ) {
                    throw new InvalidOperationException($"{PREFLIGHT_MISSING_BEFORE_PHASE_MESSAGE} phaseIndex={i} phaseKey=\"{phaseKey}\" key=\"{key.Name}\" type=\"{key.ValueType.FullName}\"");
                }
            }

            IReadOnlyList<IArtifactKey> produces = factory.Produces ?? throw new InvalidOperationException($"{PREFLIGHT_NULL_PRODUCES_MESSAGE} phaseKey=\"{phaseKey}\"");
            for( int p = 0; p < produces.Count; p++ ) {
                IArtifactKey key = produces[p] ?? throw new InvalidOperationException($"{PREFLIGHT_CONTAINS_NULL_KEY_MESSAGE} phaseKey=\"{phaseKey}\"");
                available.Add(key);
            }
        }
    }

    public async Task ExecuteAsync(SequentialPlan plan, Run run, CancellationToken ct) {
        if( plan is null ) throw new ArgumentNullException(nameof(plan));
        if( run is null ) throw new ArgumentNullException(nameof(run));

        IReadOnlyList<IPhaseFactory> factories = plan.Steps.Select(x => x.Factory).ToArray();

        run.Publish(new RunStartedEvent(run.Id));

        PhaseResult aggregate = PhaseResult.Succeeded;
        PhaseKey? firstNonSucceededPhaseKey = null;

        try {
            for( int i = 0; i < factories.Count; i++ ) {
                if( ct.IsCancellationRequested || run.IsCancellationRequested ) {
                    run.Publish(new RunCancelledEvent(run.Id, run.CancelReason));
                    run.TryComplete(RunOutcome.Cancelled(run.CancelReason));
                    return;
                }

                IPhaseFactory factory = factories[i] ?? throw new InvalidOperationException("Phase factory list contained null.");
                PhaseKey phaseKey = factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");

                try {
                    run.ValidateConsumes(factory.Consumes, phaseKey);

                    IPhase phase = factory.Create();
                    if( phase is null )
                        throw new InvalidOperationException($"Phase factory returned null. PhaseKey=\"{phaseKey}\"");

                    run.AttachPhase(phaseKey, phase);
                    run.Publish(new PhaseStartedEvent(run.Id, phaseKey, i));

                    IPhaseTelemetry telemetry = new PhaseTelemetry(run, phaseKey);
                    IPhaseContext context = factory is IPhaseContextFactoryBridge typedFactory
                        ? typedFactory.CreateContext(run.CancellationToken, run.Artifacts, run.Interaction, telemetry)
                        : new PhaseContext(phaseKey, run.CancellationToken, run.Artifacts, run.Interaction, telemetry);

                    IPhaseOutcome phaseOutcome = await phase.ExecuteAsync(context, run.CancellationToken).ConfigureAwait(false);
                    if( phaseOutcome is null )
                        throw new InvalidOperationException($"Phase returned null outcome. PhaseKey=\"{phaseKey}\"");

                    IReadOnlyList<IProducedArtifact> produced = phaseOutcome.Produced ?? throw new InvalidOperationException($"Phase outcome returned null produced list. PhaseKey=\"{phaseKey}\"");
                    run.MergeProduced(factory.Produces, produced, phaseKey);

                    aggregate = Aggregate(aggregate, phaseOutcome.Result);
                    if( firstNonSucceededPhaseKey is null && IsNonSucceeded(phaseOutcome.Result) )
                        firstNonSucceededPhaseKey = phaseKey;

                    run.Publish(new PhaseCompletedEvent(run.Id, phaseKey, i, phaseOutcome.Result));

                    if( phaseOutcome.Result == PhaseResult.Cancelled ) {
                        run.Publish(new RunCancelledEvent(run.Id, run.CancelReason));
                        run.TryComplete(RunOutcome.Cancelled(run.CancelReason));
                        return;
                    }

                    if( phaseOutcome.Continuation == PhaseContinuation.Halt ) {
                        SettleAsAggregated(run, aggregate, firstNonSucceededPhaseKey);
                        return;
                    }
                } catch( OperationCanceledException ) when( ct.IsCancellationRequested || run.IsCancellationRequested ) {
                    throw;
                } catch( Exception ex ) {
                    run.Publish(new PhaseFaultedEvent(run.Id, phaseKey, i, ex, run.CreatePhaseFaultInfo(phaseKey, ex)));
                    throw;
                }
            }

            if( ct.IsCancellationRequested || run.IsCancellationRequested ) {
                run.Publish(new RunCancelledEvent(run.Id, run.CancelReason));
                run.TryComplete(RunOutcome.Cancelled(run.CancelReason));
                return;
            }

            SettleAsAggregated(run, aggregate, firstNonSucceededPhaseKey);
        } catch( OperationCanceledException ) when( ct.IsCancellationRequested || run.IsCancellationRequested ) {
            run.Publish(new RunCancelledEvent(run.Id, run.CancelReason));
            run.TryComplete(RunOutcome.Cancelled(run.CancelReason));
        } catch( Exception ex ) {
            run.Publish(new RunFaultedEvent(run.Id, ex, run.CreateRunFaultInfo(ex)));
            run.TryFault(ex);
        }
    }

    private static void RegisterKeys(IReadOnlyList<IArtifactKey> keys, HashSet<IArtifactKey> declared, Dictionary<string, Type> nameToType, string origin) {
        if( keys is null ) throw new ArgumentNullException(nameof(keys));

        for( int i = 0; i < keys.Count; i++ ) {
            IArtifactKey key = keys[i] ?? throw new InvalidOperationException(PREFLIGHT_CONTAINS_NULL_KEY_MESSAGE);

            declared.Add(key);
            RegisterKeyNameType(key, nameToType, origin);
        }
    }

    private static void RegisterKeyNameType(IArtifactKey key, Dictionary<string, Type> nameToType, string origin) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        string name = key.Name ?? string.Empty;
        Type valueType = key.ValueType ?? throw new InvalidOperationException("ValueType is null.");

        if( nameToType.TryGetValue(name, out Type existing) ) {
            if( !ReferenceEquals(existing, valueType) ) {
                throw new InvalidOperationException($"Artifact key name collision detected (same name with different ValueType). name=\"{name}\" type1=\"{existing.FullName}\" type2=\"{valueType.FullName}\" origin=\"{origin}\"");
            }

            return;
        }

        nameToType.Add(name, valueType);
    }

    private static void SettleAsAggregated(Run run, PhaseResult aggregate, PhaseKey? firstNonSucceededPhaseKey) {
        RunOutcome finalOutcome = aggregate switch {
            PhaseResult.Failed => RunOutcome.Failed(BuildSummary("At least one phase reported Failed.", firstNonSucceededPhaseKey)),
            PhaseResult.PartiallySucceeded => RunOutcome.PartiallySucceeded(BuildSummary("At least one phase reported PartiallySucceeded.", firstNonSucceededPhaseKey)),
            _ => RunOutcome.Succeeded()
        };

        run.Publish(new RunCompletedEvent(run.Id));
        run.TryComplete(finalOutcome);
    }

    private static string BuildSummary(string message, PhaseKey? phaseKey) {
        if( phaseKey is null )
            return message;

        return $"{message} firstPhaseKey=\"{phaseKey}\"";
    }

    private static bool IsNonSucceeded(PhaseResult result) {
        return result is PhaseResult.Failed or PhaseResult.PartiallySucceeded;
    }

    private static PhaseResult Aggregate(PhaseResult current, PhaseResult next) {
        int currentSeverity = GetSeverity(current);
        int nextSeverity = GetSeverity(next);
        return nextSeverity > currentSeverity ? next : current;
    }

    private static int GetSeverity(PhaseResult result) {
        return result switch {
            PhaseResult.Failed => 2,
            PhaseResult.PartiallySucceeded => 1,
            _ => 0
        };
    }
}
