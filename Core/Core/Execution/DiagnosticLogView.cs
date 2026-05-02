using System;
using System.Collections;
using System.Collections.Generic;

namespace BeltRunner.Core.Execution;

internal sealed class DiagnosticLogView : IReadOnlyList<IDiagnosticEntry> {
    private readonly CircularBuffer<DiagnosticEntry> source;
    private readonly object gate;

    public DiagnosticLogView(CircularBuffer<DiagnosticEntry> source, object gate) {
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

    public IDiagnosticEntry this[int index] {
        get {
            lock( this.gate ) {
                return this.source[index];
            }
        }
    }

    public IEnumerator<IDiagnosticEntry> GetEnumerator() {
        DiagnosticEntry[] snapshot;
        lock( this.gate ) {
            snapshot = this.source.ToArray();
        }

        for( int i = 0; i < snapshot.Length; i++ ) {
            yield return snapshot[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
