using System;
using BeltRunner.Core.Execution;

namespace BeltRunner.Core.Host;

internal sealed class LauncherConfiguration {
    public IPublicFaultInfoPolicy PublicFaultInfoPolicy { get; set; } = new DefaultPublicFaultInfoPolicy();

    public int? RunEventLogMaxRetainedCount { get; set; } = 256;

    public int? InteractionRequestLogMaxRetainedCount { get; set; } = 64;

    public int InteractionMaxPendingRequestCount { get; set; } = 10;

    public int? RunDiagnosticsMaxRetainedCount { get; set; } = 256;

    public DiagnosticMode DiagnosticMode { get; set; } = DiagnosticMode.All;

    public TimeSpan? SnapshotPublishCoalescingInterval { get; set; }
}
