using BeltRunner.Core.Phase;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.TEST.Phase;

/// <summary>
/// Verifies collection and locking behavior in <see cref="UnitSet"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the runtime unit collection against duplicate identifiers, incorrect event behavior, and unintended mutability.</para>
/// <para>Why this matters: Unit tracking is foundational for progress, snapshots, and telemetry, so collection semantics must remain predictable.</para>
/// <para>Expected result: Bulk addition locks the set correctly, duplicate additions are rejected safely, and invalid duplicate batches leave the set unchanged.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(UnitSet))]
public sealed class UnitSetTests {
    /// <summary>
    /// Verifies that adding a range and locking the set updates membership and raises the changed event once.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the combined behavior of bulk addition and locking.</para>
    /// <para>Why this matters: Runtime phases may finalize discovered units in one operation, and observers need a stable single notification.</para>
    /// <para>Expected result: The units are present, the set is locked, and the changed event fires exactly once.</para>
    /// </remarks>
    [Test]
    public void AddRangeAndLock_AddsUnits_LocksSet_AndRaisesChangedOnce() {
        UnitSet set = new();
        int changedCount = 0;
        TestUnit first = new(Guid.NewGuid(), "First");
        TestUnit second = new(Guid.NewGuid(), "Second");

        set.Changed += (_, _) => changedCount++;

        set.AddRangeAndLock([first, second]);
        TestNarrative.ObserveMany(
            $"isLocked={set.IsLocked}",
            $"count={set.Count}",
            $"changedCount={changedCount}",
            $"unitNames={string.Join(", ", set.Items.Select(x => x.Name))}");

        Assert.Multiple(() => {
            Assert.That(set.IsLocked, Is.True);
            Assert.That(set.Count, Is.EqualTo(2));
            Assert.That(set.Contains(first.Id), Is.True);
            Assert.That(set.Contains(second.Id), Is.True);
            Assert.That(set.Items, Has.Count.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies that adding a duplicate unit identifier with <c>TryAdd</c> fails without raising a changed event.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the non-throwing duplicate handling path.</para>
    /// <para>Why this matters: Duplicate discovery can happen in caller code, and the collection should reject it without pretending that state changed.</para>
    /// <para>Expected result: The duplicate is rejected, the original membership remains intact, and the changed event count does not increase.</para>
    /// </remarks>
    [Test]
    public void TryAdd_WhenDuplicateIdExists_ReturnsFalse_AndDoesNotRaiseChanged() {
        UnitSet set = new();
        int changedCount = 0;
        Guid unitId = Guid.NewGuid();
        TestUnit original = new(unitId, "Original");
        TestUnit duplicate = new(unitId, "Duplicate");

        set.Changed += (_, _) => changedCount++;
        set.Add(original);

        bool added = set.TryAdd(duplicate);
        TestNarrative.ObserveMany(
            $"added={added}",
            $"count={set.Count}",
            $"changedCount={changedCount}",
            $"containsOriginal={set.Contains(original.Id)}");

        Assert.Multiple(() => {
            Assert.That(added, Is.False);
            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set.Contains(original.Id), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies that a duplicate identifier inside a bulk addition throws and leaves the set unchanged.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the failure behavior for invalid bulk additions.</para>
    /// <para>Why this matters: Partially applied duplicate batches would make runtime state hard to reason about and harder to recover from.</para>
    /// <para>Expected result: An <see cref="InvalidOperationException"/> is thrown, and the set remains empty and unlocked.</para>
    /// </remarks>
    [Test]
    public void AddRange_WhenInputContainsDuplicateIds_Throws_AndLeavesSetUnchanged() {
        UnitSet set = new();
        Guid duplicatedId = Guid.NewGuid();
        TestUnit first = new(duplicatedId, "First");
        TestUnit second = new(duplicatedId, "Second");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => set.AddRange([first, second]))!;
        TestNarrative.ObserveMany(
            $"exceptionMessage={ex.Message}",
            $"count={set.Count}",
            $"isLocked={set.IsLocked}",
            $"itemCount={set.Items.Count}");

        Assert.Multiple(() => {
            Assert.That(ex.Message, Does.Contain("same id"));
            Assert.That(set.Count, Is.EqualTo(0));
            Assert.That(set.IsLocked, Is.False);
            Assert.That(set.Items, Is.Empty);
        });
    }

    private sealed class TestUnit : IUnit {
        public TestUnit(Guid id, string name) {
            this.Id = id;
            this.Name = name;
        }

        public Guid Id { get; }

        public string Name { get; }

        public UnitStatus Status => UnitStatus.Pending;

        public PhaseKey? CurrentPhaseKey => null;

        public IReadOnlyCollection<UnitTag> Tags { get; } = Array.Empty<UnitTag>();
    }
}
