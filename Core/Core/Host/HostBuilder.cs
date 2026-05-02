using System;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;

namespace BeltRunner.Core.Host;

/// <summary>
/// Builds BeltRunner <see cref="IHost"/> instances by collecting host-level options through a fluent configuration surface.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures BeltRunner's run host, not the .NET Generic Host from
/// <c>Microsoft.Extensions.Hosting</c>.
/// </para>
/// <para>
/// Each call to <see cref="Build"/> clones the accumulated <see cref="HostOptions"/> into a new host instance.
/// Changing the builder after a host has been built does not mutate hosts that were already created.
/// </para>
/// </remarks>
public sealed class HostBuilder {
    private readonly HostOptions options = new();

    /// <summary>
    /// Applies arbitrary configuration to the underlying <see cref="HostOptions"/>.
    /// </summary>
    /// <param name="configure">
    /// The configuration callback that can update any host option exposed by <see cref="HostOptions"/>.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public HostBuilder Configure(Action<HostOptions> configure) {
        if( configure is null ) {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(this.options);
        return this;
    }

    /// <summary>
    /// Sets the interaction broker factory used for hosts created by this builder.
    /// </summary>
    /// <param name="factory">
    /// The factory that creates a fresh run-scoped interaction broker for each start request.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public HostBuilder UseInteractionBrokerFactory(Func<IInteractionBroker> factory) {
        if( factory is null ) {
            throw new ArgumentNullException(nameof(factory));
        }

        this.options.InteractionBrokerFactory = factory;
        return this;
    }

    /// <summary>
    /// Sets the diagnostic mode used for hosts created by this builder.
    /// </summary>
    /// <param name="diagnosticMode">The diagnostic mode.</param>
    /// <returns>The current builder instance.</returns>
    public HostBuilder WithDiagnosticMode(DiagnosticMode diagnosticMode) {
        this.options.DiagnosticMode = diagnosticMode;
        return this;
    }

    /// <summary>
    /// Sets whether failed outcomes should be treated as host faults.
    /// </summary>
    /// <param name="value">
    /// <see langword="true"/> to transition the host to <see cref="HostState.Faulted"/> when a run completes
    /// with a failed outcome; otherwise, <see langword="false"/> to keep failed outcomes as non-fault host completion.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public HostBuilder FaultOnFailedOutcome(bool value) {
        this.options.FaultOnFailedOutcome = value;
        return this;
    }

    /// <summary>
    /// Sets the public fault-info policy used for hosts created by this builder.
    /// </summary>
    /// <param name="policy">
    /// The policy that converts internal failures into sanitized public fault payloads.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public HostBuilder UsePublicFaultInfoPolicy(IPublicFaultInfoPolicy policy) {
        if( policy is null ) {
            throw new ArgumentNullException(nameof(policy));
        }

        this.options.PublicFaultInfoPolicy = policy;
        return this;
    }

    /// <summary>
    /// Builds a host from the current configuration.
    /// </summary>
    /// <returns>
    /// A new <see cref="IHost"/> instance whose options are copied from the builder at build time.
    /// </returns>
    public IHost Build() {
        return new Host(CloneOptions());
    }

    private HostOptions CloneOptions() {
        return new HostOptions {
            PublicFaultInfoPolicy = this.options.PublicFaultInfoPolicy,
            DiagnosticMode = this.options.DiagnosticMode,
            FaultOnFailedOutcome = this.options.FaultOnFailedOutcome,
            RunEventLogMaxRetainedCount = this.options.RunEventLogMaxRetainedCount,
            InteractionRequestLogMaxRetainedCount = this.options.InteractionRequestLogMaxRetainedCount,
            InteractionMaxPendingRequestCount = this.options.InteractionMaxPendingRequestCount,
            RunDiagnosticsMaxRetainedCount = this.options.RunDiagnosticsMaxRetainedCount,
            SnapshotPublishCoalescingInterval = this.options.SnapshotPublishCoalescingInterval,
            InteractionBrokerFactory = this.options.InteractionBrokerFactory
        };
    }
}
