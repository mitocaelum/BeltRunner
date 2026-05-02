using System;
using BeltRunner.Core.Execution.Event;

namespace BeltRunner.Core.Execution;

internal readonly struct StoredRunEvent {
    public StoredRunEvent(long sequence, RunEvent @event) {
        Sequence = sequence;
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
    }

    public long Sequence { get; }
    public RunEvent Event { get; }
}
