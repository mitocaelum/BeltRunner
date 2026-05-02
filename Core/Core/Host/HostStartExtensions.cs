using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Host;

/// <summary>
/// Provides convenience entry points for common host startup scenarios that build a sequential plan inline.
/// </summary>
/// <remarks>
/// These helpers are intended for call sites that want to start a run immediately without manually creating
/// a <see cref="SequentialPlanBuilder"/>, a <see cref="SequentialPlan"/>, or initial artifact collections first.
/// </remarks>
public static class HostStartExtensions {
    /// <summary>
    /// Builds a sequential plan and starts it immediately.
    /// </summary>
    /// <param name="host">The host that starts the run.</param>
    /// <param name="configurePlan">
    /// The callback that populates the temporary <see cref="SequentialPlanBuilder"/> used for this start request.
    /// </param>
    /// <param name="ct">The optional cancellation token.</param>
    /// <returns>
    /// A task that returns the already-started run after the plan has been built and accepted by the host.
    /// </returns>
    public static Task<IRun> StartSequentialAsync(this IHost host, 
        Action<SequentialPlanBuilder> configurePlan, 
        CancellationToken ct = default) {
        if( host is null ) {
            throw new ArgumentNullException(nameof(host));
        }

        SequentialPlan plan = BuildPlan(configurePlan);
        return host.StartAsync(plan, ct);
    }

    /// <summary>
    /// Builds a sequential plan, builds initial artifacts, and starts the run immediately.
    /// </summary>
    /// <param name="host">The host that starts the run.</param>
    /// <param name="configurePlan">
    /// The callback that populates the temporary <see cref="SequentialPlanBuilder"/> used for this start request.
    /// </param>
    /// <param name="configureArtifacts">
    /// The callback that populates the temporary <see cref="ArtifactSeedBuilder"/> used to create initial artifacts.
    /// </param>
    /// <param name="ct">The optional cancellation token.</param>
    /// <returns>
    /// A task that returns the already-started run after the plan and initial artifacts have been built.
    /// </returns>
    public static Task<IRun> StartSequentialAsync(this IHost host, 
        Action<SequentialPlanBuilder> configurePlan, 
        Action<ArtifactSeedBuilder> configureArtifacts, 
        CancellationToken ct = default) {
        if( host is null ) {
            throw new ArgumentNullException(nameof(host));
        }

        SequentialPlan plan = BuildPlan(configurePlan);
        IReadOnlyList<IProducedArtifact> artifacts = BuildArtifacts(configureArtifacts);
        return host.StartAsync(plan, artifacts, ct);
    }

    /// <summary>
    /// Builds a sequential plan, builds initial artifacts, and starts the run immediately with launch options.
    /// </summary>
    /// <param name="host">The host that starts the run.</param>
    /// <param name="configurePlan">
    /// The callback that populates the temporary <see cref="SequentialPlanBuilder"/> used for this start request.
    /// </param>
    /// <param name="configureArtifacts">
    /// The callback that populates the temporary <see cref="ArtifactSeedBuilder"/> used to create initial artifacts.
    /// </param>
    /// <param name="options">The launch options for the run.</param>
    /// <param name="ct">The optional cancellation token.</param>
    /// <returns>
    /// A task that returns the already-started run after the plan, artifacts, and launch options have been applied.
    /// </returns>
    public static Task<IRun> StartSequentialAsync(this IHost host, 
        Action<SequentialPlanBuilder> configurePlan, 
        Action<ArtifactSeedBuilder> configureArtifacts, 
        RunLaunchOptions options, 
        CancellationToken ct = default) {
        if( host is null ) {
            throw new ArgumentNullException(nameof(host));
        }

        if( options is null ) {
            throw new ArgumentNullException(nameof(options));
        }

        SequentialPlan plan = BuildPlan(configurePlan);
        IReadOnlyList<IProducedArtifact> artifacts = BuildArtifacts(configureArtifacts);
        return host.StartAsync(plan, artifacts, options, ct);
    }

    private static SequentialPlan BuildPlan(Action<SequentialPlanBuilder> configurePlan) {
        if( configurePlan is null ) {
            throw new ArgumentNullException(nameof(configurePlan));
        }

        SequentialPlanBuilder builder = new();
        configurePlan(builder);
        return builder.Build();
    }

    private static IReadOnlyList<IProducedArtifact> BuildArtifacts(Action<ArtifactSeedBuilder> configureArtifacts) {
        if( configureArtifacts is null ) {
            throw new ArgumentNullException(nameof(configureArtifacts));
        }

        ArtifactSeedBuilder builder = new();
        configureArtifacts(builder);
        return builder.Build();
    }
}
