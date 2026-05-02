using System;
using System.Collections;
using System.Collections.Generic;
using BeltRunner.Core.Execution.Interaction;

namespace BeltRunner.Core.Execution;

internal sealed class ActiveInteractionView : IReadOnlyList<IInteractionSnapshot> {
    private readonly List<InteractionSnapshot> source;
    private readonly object gate;

    public ActiveInteractionView(List<InteractionSnapshot> source, object gate) {
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

    public IInteractionSnapshot this[int index] {
        get {
            lock( this.gate ) {
                return this.source[index];
            }
        }
    }

    public IEnumerator<IInteractionSnapshot> GetEnumerator() {
        InteractionSnapshot[] snapshot;

        lock( this.gate ) {
            snapshot = this.source.ToArray();
        }

        for( int i = 0; i < snapshot.Length; i++ ) {
            yield return snapshot[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
