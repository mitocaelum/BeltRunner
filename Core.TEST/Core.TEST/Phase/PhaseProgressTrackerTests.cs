using System.Collections.Concurrent;
using BeltRunner.Core.Phase;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.TEST.Phase;

/// <summary>
/// Verifies the high-level phase progress tracker helpers.
/// </summary>
[TestFixture]
[TestOf(typeof(IPhaseProgressTracker))]
[TestOf(typeof(ITrackedUnitScope))]
public sealed class PhaseProgressTrackerTests {
    /// <summary>
    /// Verifies that starting a tracker sets the total and that reported completed counts are monotonic and clamped.
    /// </summary>
    [Test]
    public void ReportCompleted_IsMonotonic_AndClampedToTotal() {
        RecordingProgressSink sink = new();
        using IPhaseProgressTracker tracker = new PhaseProgressTracker(3, sink.SetTotalUnits, sink.SetProcessedUnits, sink.SetUnitProgress, sink.SetUnitStatus);

        tracker.ReportCompleted(1);
        tracker.ReportCompleted(0);
        tracker.ReportCompleted(5);

        TestNarrative.ObserveMany(
            $"totalUnits={sink.TotalUnits}",
            $"processedUpdates={string.Join(", ", sink.ProcessedUpdates)}");

        Assert.Multiple(() => {
            Assert.That(sink.TotalUnits, Is.EqualTo(3));
            Assert.That(sink.ProcessedUpdates, Is.EqualTo(new[] { 1, 3 }));
        });
    }

    /// <summary>
    /// Verifies that tracked-unit scopes mark units as running immediately and complete successfully only when requested.
    /// </summary>
    [Test]
    public void BeginUnit_AndComplete_UseExpectedLifecycle() {
        RecordingProgressSink sink = new();
        TestUnit unit = new("Tracked Unit");
        using IPhaseProgressTracker tracker = new PhaseProgressTracker(2, sink.SetTotalUnits, sink.SetProcessedUnits, sink.SetUnitProgress, sink.SetUnitStatus);

        using ITrackedUnitScope firstScope = tracker.BeginUnit(unit);
        firstScope.Complete();
        firstScope.Complete();

        using ITrackedUnitScope secondScope = tracker.BeginUnit(Guid.NewGuid());

        TestNarrative.ObserveMany(
            $"statusUpdates={string.Join(", ", sink.StatusUpdates.Select(x => $"{x.UnitId}:{x.Status}"))}",
            $"progressUpdates={string.Join(", ", sink.ProgressUpdates.Select(x => $"{x.UnitId}:{x.Ratio:0.####}"))}",
            $"processedUpdates={string.Join(", ", sink.ProcessedUpdates)}");

        Assert.Multiple(() => {
            Assert.That(sink.StatusUpdates[0], Is.EqualTo((unit.Id, UnitStatus.Running)));
            Assert.That(sink.StatusUpdates[1], Is.EqualTo((unit.Id, UnitStatus.Succeeded)));
            Assert.That(sink.ProgressUpdates, Is.EqualTo(new[] { (unit.Id, 1.0) }));
            Assert.That(sink.ProcessedUpdates, Is.EqualTo(new[] { 1 }));
            Assert.That(sink.StatusUpdates.Count(x => x == (unit.Id, UnitStatus.Succeeded)), Is.EqualTo(1));
            Assert.That(sink.StatusUpdates.Last().Status, Is.EqualTo(UnitStatus.Running));
        });
    }

    /// <summary>
    /// Verifies that invalid completed counts are rejected.
    /// </summary>
    [Test]
    public void ReportCompleted_RejectsNegativeCounts() {
        RecordingProgressSink sink = new();
        using IPhaseProgressTracker tracker = new PhaseProgressTracker(2, sink.SetTotalUnits, sink.SetProcessedUnits, sink.SetUnitProgress, sink.SetUnitStatus);

        Assert.That(() => tracker.ReportCompleted(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    /// <summary>
    /// Verifies that concurrent completions on a single tracker are aggregated safely.
    /// </summary>
    [Test]
    public async Task Complete_FromMultipleTasks_AggregatesSafely() {
        ConcurrentProgressSink sink = new();
        using IPhaseProgressTracker tracker = new PhaseProgressTracker(32, sink.SetTotalUnits, sink.SetProcessedUnits, sink.SetUnitProgress, sink.SetUnitStatus);

        Task[] tasks = Enumerable.Range(0, 32)
            .Select(async index => {
                using ITrackedUnitScope scope = tracker.BeginUnit(Guid.NewGuid());
                await Task.Yield();
                scope.Complete();
            })
            .ToArray();

        await Task.WhenAll(tasks);
        int maxProcessed = sink.ProcessedUpdates.Count == 0 ? 0 : sink.ProcessedUpdates.Max();
        TestNarrative.ObserveMany(
            $"totalUnits={sink.TotalUnits}",
            $"processedUpdateCount={sink.ProcessedUpdates.Count}",
            $"maxProcessed={maxProcessed}");

        Assert.Multiple(() => {
            Assert.That(sink.TotalUnits, Is.EqualTo(32));
            Assert.That(maxProcessed, Is.EqualTo(32));
            Assert.That(sink.StatusUpdates.Count(x => x.Status == UnitStatus.Succeeded), Is.EqualTo(32));
        });
    }

    private sealed class RecordingProgressSink {
        public List<int> ProcessedUpdates { get; } = new();

        public List<(Guid UnitId, double Ratio)> ProgressUpdates { get; } = new();

        public List<(Guid UnitId, UnitStatus Status)> StatusUpdates { get; } = new();

        public int? TotalUnits { get; private set; }

        public void SetTotalUnits(int? totalUnits) {
            this.TotalUnits = totalUnits;
        }

        public void SetProcessedUnits(int processedUnits) {
            this.ProcessedUpdates.Add(processedUnits);
        }

        public void SetUnitProgress(Guid unitId, double ratio) {
            this.ProgressUpdates.Add((unitId, ratio));
        }

        public void SetUnitStatus(Guid unitId, UnitStatus status) {
            this.StatusUpdates.Add((unitId, status));
        }
    }

    private sealed class ConcurrentProgressSink {
        public ConcurrentBag<int> ProcessedUpdates { get; } = new();

        public ConcurrentBag<(Guid UnitId, double Ratio)> ProgressUpdates { get; } = new();

        public ConcurrentBag<(Guid UnitId, UnitStatus Status)> StatusUpdates { get; } = new();

        public int? TotalUnits { get; private set; }

        public void SetTotalUnits(int? totalUnits) {
            this.TotalUnits = totalUnits;
        }

        public void SetProcessedUnits(int processedUnits) {
            this.ProcessedUpdates.Add(processedUnits);
        }

        public void SetUnitProgress(Guid unitId, double ratio) {
            this.ProgressUpdates.Add((unitId, ratio));
        }

        public void SetUnitStatus(Guid unitId, UnitStatus status) {
            this.StatusUpdates.Add((unitId, status));
        }
    }

    private sealed class TestUnit : Unit<string> {
        public TestUnit(string name) : base(name, name) {
        }
    }
}
