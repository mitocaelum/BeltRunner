using System;
using System.Threading;
using BeltRunner.Core.Host;

namespace BeltRunner.Core.Execution;

internal sealed class RunConfiguration {
    public IPublicFaultInfoPolicy PublicFaultInfoPolicy { get; set; } = new DefaultPublicFaultInfoPolicy();

    public CancellationToken CancellationToken { get; set; }

    public RunLifecycleCallbacks? LifecycleCallbacks { get; set; }

    public int? EventLogMaxRetainedCount { get; set; } = 256;

    public int? RunDiagnosticsMaxRetainedCount { get; set; } = 256;

    public DiagnosticMode DiagnosticMode { get; set; } = DiagnosticMode.All;

    public TimeSpan? SnapshotPublishCoalescingInterval { get; set; }
}
