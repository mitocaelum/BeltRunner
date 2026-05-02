using System;

namespace BeltRunner.Core.Host;

/// <summary>
/// Defines optional behavior for a single run launch request.
/// </summary>
/// <remarks>
/// <para>
/// Instances of this type are consumed per call to <see cref="IHost.StartAsync(BeltRunner.Core.Plan.SequentialPlan, RunLaunchOptions, System.Threading.CancellationToken)"/>
/// and its overloads.
/// They do not mutate host-wide defaults.
/// </para>
/// <para>
/// <see cref="Execution.IRun.Completion"/> remains the primary completion API for callers.
/// <see cref="LifecycleCallbacks"/> can be used to attach supplemental run-scoped hooks without changing host-wide behavior.
/// </para>
/// </remarks>
public sealed class RunLaunchOptions {
    private RunLifecycleCallbacks lifecycleCallbacks = new();

    /// <summary>
    /// Gets or sets the run-scoped lifecycle callbacks for this launch request.
    /// </summary>
    /// <remarks>
    /// These callbacks supplement, but do not replace, the primary completion flow exposed by <see cref="Execution.IRun.Completion"/>.
    /// When this property is left at its default value, no lifecycle hooks are invoked for the launch request.
    /// </remarks>
    public RunLifecycleCallbacks LifecycleCallbacks {
        get => this.lifecycleCallbacks;
        set => this.lifecycleCallbacks = value ?? throw new ArgumentNullException(nameof(value));
    }
}
