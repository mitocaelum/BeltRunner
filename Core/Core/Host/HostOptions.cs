using System;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;

namespace BeltRunner.Core.Host;

/// <summary>
/// Host-level options that control how the host interprets run completion, faults, and interaction wiring.
/// </summary>
public sealed class HostOptions {
    /// <summary>
    /// Gets or sets the policy that creates sanitized public fault information.
    /// </summary>
    public IPublicFaultInfoPolicy PublicFaultInfoPolicy {
        get => this.publicFaultInfoPolicy;
        set => this.publicFaultInfoPolicy = value ?? throw new ArgumentNullException(nameof(value));
    }
    private IPublicFaultInfoPolicy publicFaultInfoPolicy = new DefaultPublicFaultInfoPolicy();

    /// <summary>
    /// Gets or sets the diagnostic generation mode applied to started runs.
    /// </summary>
    /// <remarks>
    /// Default is <see cref="DiagnosticMode.All"/>.
    /// </remarks>
    public DiagnosticMode DiagnosticMode { get; set; } = DiagnosticMode.All;

    /// <summary>
    /// Gets or sets a value indicating whether a run completion whose outcome kind is
    /// <see cref="RunOutcomeKind.Failed"/>
    /// should transition the host to <see cref="HostState.Faulted"/>.
    /// </summary>
    /// <remarks>
    /// Default is <c>true</c>.
    /// </remarks>
    public bool FaultOnFailedOutcome { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retained entries in <see cref="IRun.EventLog"/>.
    /// </summary>
    /// <remarks>
    /// Set this value to cap in-memory run event history and replay size for late subscribers.
    /// The default is <c>256</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int? RunEventLogMaxRetainedCount {
        get => this.runEventLogMaxRetainedCount;
        set {
            if( value.HasValue && value.Value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Run event log max retained count must be greater than zero.");
            }

            this.runEventLogMaxRetainedCount = value;
        }
    }
    private int? runEventLogMaxRetainedCount = 256;

    /// <summary>
    /// Gets or sets the maximum number of retained entries in <see cref="IInteractionBroker.RequestLog"/>.
    /// </summary>
    /// <remarks>
    /// Set this value to cap in-memory interaction request history and replay size for late subscribers.
    /// The default is <c>64</c>.
    /// When <see cref="InteractionBrokerFactory"/> returns an <see cref="InMemoryInteractionBroker"/>,
    /// the host applies this limit before the run starts.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int? InteractionRequestLogMaxRetainedCount {
        get => this.interactionRequestLogMaxRetainedCount;
        set {
            if( value.HasValue && value.Value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Interaction request log max retained count must be greater than zero.");
            }

            this.interactionRequestLogMaxRetainedCount = value;
        }
    }
    private int? interactionRequestLogMaxRetainedCount = 64;

    /// <summary>
    /// Gets or sets the maximum number of simultaneously pending interaction requests.
    /// </summary>
    /// <remarks>
    /// Set this value to cap in-memory active interaction state and active-request notifications.
    /// The default is <c>10</c>.
    /// When <see cref="InteractionBrokerFactory"/> returns an <see cref="InMemoryInteractionBroker"/>,
    /// the host applies this limit before the run starts.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int InteractionMaxPendingRequestCount {
        get => this.interactionMaxPendingRequestCount;
        set {
            if( value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Interaction max pending request count must be greater than zero.");
            }

            this.interactionMaxPendingRequestCount = value;
        }
    }
    private int interactionMaxPendingRequestCount = 10;

    /// <summary>
    /// Gets or sets the maximum number of retained entries in <see cref="IRun.DiagnosticLog"/>.
    /// </summary>
    /// <remarks>
    /// Set this value to cap in-memory run diagnostic history retained by the run.
    /// The default is <c>256</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int? RunDiagnosticsMaxRetainedCount {
        get => this.runDiagnosticsMaxRetainedCount;
        set {
            if( value.HasValue && value.Value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Run diagnostics max retained count must be greater than zero.");
            }

            this.runDiagnosticsMaxRetainedCount = value;
        }
    }
    private int? runDiagnosticsMaxRetainedCount = 256;

    /// <summary>
    /// Gets or sets the interval used to coalesce high-frequency snapshot publications.
    /// </summary>
    /// <remarks>
    /// Set this value to batch frequent snapshot updates such as telemetry progress.
    /// The default is <see cref="TimeSpan.Zero"/>, which disables coalescing and publishes every snapshot immediately.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan SnapshotPublishCoalescingInterval {
        get => this.snapshotPublishCoalescingInterval;
        set {
            if( value < TimeSpan.Zero ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Snapshot publish coalescing interval must be zero or greater.");
            }

            this.snapshotPublishCoalescingInterval = value;
        }
    }
    private TimeSpan snapshotPublishCoalescingInterval = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the factory that creates the run-scoped interaction broker for each start request.
    /// </summary>
    /// <remarks>
    /// The default factory creates a <see cref="DisabledInteractionBroker"/>, so interaction is disabled
    /// unless the caller opts in by providing another factory. When the created broker is an
    /// <see cref="InMemoryInteractionBroker"/>, the host applies <see cref="InteractionRequestLogMaxRetainedCount"/>
    /// and <see cref="InteractionMaxPendingRequestCount"/> automatically before execution begins.
    /// </remarks>
    public Func<IInteractionBroker> InteractionBrokerFactory {
        get => this.interactionBrokerFactory;
        set => this.interactionBrokerFactory = value ?? throw new ArgumentNullException(nameof(value));
    }
    private Func<IInteractionBroker> interactionBrokerFactory = CreateDisabledInteractionBroker;

    private static IInteractionBroker CreateDisabledInteractionBroker() {
        return new DisabledInteractionBroker();
    }
}
