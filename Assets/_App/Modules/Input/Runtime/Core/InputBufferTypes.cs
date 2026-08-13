using System;

namespace ModularInput
{
    public enum InputBufferOverflowPolicy
    {
        RejectIncoming,
        DropOldest,
        DropLowestPriority
    }

    public enum InputBufferDuplicatePolicy
    {
        Allow,
        RejectIncoming,
        RefreshMatching,
        ReplaceMatching
    }

    public enum InputBufferSelectionPolicy
    {
        OldestFirst,
        NewestFirst,
        HighestPriorityThenOldest,
        HighestPriorityThenNewest
    }

    public readonly struct InputBufferOptions
    {
        public int Capacity { get; }
        public InputBufferOverflowPolicy OverflowPolicy { get; }
        public InputBufferSelectionPolicy SelectionPolicy { get; }

        public InputBufferOptions(
            int capacity,
            InputBufferOverflowPolicy overflowPolicy,
            InputBufferSelectionPolicy selectionPolicy)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            OverflowPolicy = overflowPolicy;
            SelectionPolicy = selectionPolicy;
        }
    }

    public readonly struct InputBufferWriteOptions
    {
        public double Lifetime { get; }
        public int Priority { get; }
        public InputBufferDuplicatePolicy DuplicatePolicy { get; }

        public InputBufferWriteOptions(
            double lifetime,
            int priority = 0,
            InputBufferDuplicatePolicy duplicatePolicy = InputBufferDuplicatePolicy.Allow)
        {
            if (double.IsNaN(lifetime) || double.IsInfinity(lifetime) || lifetime <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            Lifetime = lifetime;
            Priority = priority;
            DuplicatePolicy = duplicatePolicy;
        }
    }

    public readonly struct InputBufferEntry<TAction>
    {
        public long Sequence { get; }
        public TAction Action { get; }
        public double ReceivedAt { get; }
        public double ExpiresAt { get; }
        public int Priority { get; }

        internal InputBufferEntry(
            long sequence,
            TAction action,
            double receivedAt,
            double expiresAt,
            int priority)
        {
            Sequence = sequence;
            Action = action;
            ReceivedAt = receivedAt;
            ExpiresAt = expiresAt;
            Priority = priority;
        }

        public bool IsExpired(double timestamp)
        {
            return timestamp > ExpiresAt;
        }
    }

    public delegate bool InputBufferEntryPredicate<TAction>(
        in InputBufferEntry<TAction> entry);
}
