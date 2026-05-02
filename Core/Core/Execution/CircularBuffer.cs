using System;

namespace BeltRunner.Core.Execution;

internal sealed class CircularBuffer<T> {
    private const int DEFAULT_UNBOUNDED_CAPACITY = 4;

    private T[] items;
    private int start;
    private int count;
    private int? boundedCapacity;

    public CircularBuffer(int? capacity = null) {
        if( capacity.HasValue && capacity.Value <= 0 ) {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        this.boundedCapacity = capacity;
        this.items = new T[capacity ?? DEFAULT_UNBOUNDED_CAPACITY];
    }

    public int Count => this.count;

    public T this[int index] {
        get {
            if( index < 0 || index >= this.count ) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return this.items[GetPhysicalIndex(index)];
        }
    }

    public void Add(T item) {
        if( this.boundedCapacity.HasValue ) {
            AddBounded(item);
            return;
        }

        EnsureUnboundedCapacity(this.count + 1);
        this.items[GetPhysicalIndex(this.count)] = item;
        this.count++;
    }

    public void SetCapacity(int? capacity) {
        if( capacity.HasValue && capacity.Value <= 0 ) {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        T[] snapshot = ToArray();
        this.boundedCapacity = capacity;

        if( capacity.HasValue ) {
            int retained = Math.Min(snapshot.Length, capacity.Value);
            T[] newItems = new T[capacity.Value];
            int offset = snapshot.Length - retained;
            Array.Copy(snapshot, offset, newItems, 0, retained);
            this.items = newItems;
            this.start = 0;
            this.count = retained;
            return;
        }

        int nextCapacity = Math.Max(snapshot.Length, DEFAULT_UNBOUNDED_CAPACITY);
        T[] unboundedItems = new T[nextCapacity];
        Array.Copy(snapshot, 0, unboundedItems, 0, snapshot.Length);
        this.items = unboundedItems;
        this.start = 0;
        this.count = snapshot.Length;
    }

    public T[] ToArray() {
        if( this.count == 0 ) {
            return Array.Empty<T>();
        }

        T[] snapshot = new T[this.count];
        if( this.start + this.count <= this.items.Length ) {
            Array.Copy(this.items, this.start, snapshot, 0, this.count);
            return snapshot;
        }

        int firstSegmentLength = this.items.Length - this.start;
        Array.Copy(this.items, this.start, snapshot, 0, firstSegmentLength);
        Array.Copy(this.items, 0, snapshot, firstSegmentLength, this.count - firstSegmentLength);
        return snapshot;
    }

    private void AddBounded(T item) {
        int capacity = this.boundedCapacity!.Value;
        if( this.count < capacity ) {
            this.items[GetPhysicalIndex(this.count)] = item;
            this.count++;
            return;
        }

        this.items[this.start] = item;
        this.start++;
        if( this.start == capacity ) {
            this.start = 0;
        }
    }

    private void EnsureUnboundedCapacity(int requiredCount) {
        if( requiredCount <= this.items.Length ) {
            return;
        }

        int nextCapacity = Math.Max(requiredCount, this.items.Length * 2);
        T[] newItems = new T[nextCapacity];
        T[] snapshot = ToArray();
        Array.Copy(snapshot, 0, newItems, 0, snapshot.Length);
        this.items = newItems;
        this.start = 0;
    }

    private int GetPhysicalIndex(int logicalIndex) {
        int index = this.start + logicalIndex;
        if( index >= this.items.Length ) {
            index -= this.items.Length;
        }

        return index;
    }
}
