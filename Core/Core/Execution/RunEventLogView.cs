using System;
using System.Collections;
using System.Collections.Generic;
using BeltRunner.Core.Execution.Event;

namespace BeltRunner.Core.Execution;

internal sealed class RunEventLogView : IReadOnlyList<RunEvent> {
    private readonly CircularBuffer<StoredRunEvent> source;
    private readonly object gate;

    public RunEventLogView(CircularBuffer<StoredRunEvent> source, object gate) {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public int Count {
        get {
            lock( this.gate ) {
                return this.source.Count;
            }
        }
    }

    public RunEvent this[int index] {
        get {
            lock( this.gate ) {
                return this.source[index].Event;
            }
        }
    }

    public IEnumerator<RunEvent> GetEnumerator() {
        // Take a snapshot to avoid holding the lock during enumeration.
        StoredRunEvent[] snapshot;
        lock( this.gate ) {
            snapshot = this.source.ToArray();
        }

        for( int i = 0; i < snapshot.Length; i++ )
            yield return snapshot[i].Event;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
