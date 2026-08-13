using System;
using System.Collections.Generic;

namespace ModularInput
{
    public sealed class InputBuffer<TAction>
    {
        private readonly InputBufferEntry<TAction>[] _entries;
        private readonly IEqualityComparer<TAction> _comparer;
        private readonly InputBufferOverflowPolicy _overflowPolicy;
        private readonly InputBufferSelectionPolicy _selectionPolicy;

        private int _count;
        private long _nextSequence;

        public int Count => _count;
        public int Capacity => _entries.Length;

        public InputBuffer(
            InputBufferOptions options,
            IEqualityComparer<TAction> comparer = null)
        {
            if (options.Capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            _entries = new InputBufferEntry<TAction>[options.Capacity];
            _comparer = comparer ?? EqualityComparer<TAction>.Default;
            _overflowPolicy = options.OverflowPolicy;
            _selectionPolicy = options.SelectionPolicy;
        }

        public bool TryEnqueue(
            TAction action,
            double timestamp,
            InputBufferWriteOptions options)
        {
            ValidateTimestamp(timestamp);
            ValidateWriteOptions(options);
            PruneExpired(timestamp);

            int matchingIndex = FindMatchingIndex(action);
            switch (options.DuplicatePolicy)
            {
                case InputBufferDuplicatePolicy.RejectIncoming when matchingIndex >= 0:
                    return false;

                case InputBufferDuplicatePolicy.RefreshMatching when matchingIndex >= 0:
                    return TryRefresh(matchingIndex, timestamp, options);

                case InputBufferDuplicatePolicy.ReplaceMatching:
                    RemoveMatching(action);
                    break;
            }

            if (_count == Capacity && !MakeRoom(options.Priority))
            {
                return false;
            }

            double expiresAt = timestamp + options.Lifetime;
            if (double.IsInfinity(expiresAt))
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            _entries[_count++] = new InputBufferEntry<TAction>(
                NextSequence(),
                action,
                timestamp,
                expiresAt,
                options.Priority);
            return true;
        }

        public bool TryPeek(double timestamp, out InputBufferEntry<TAction> entry)
        {
            return TryPeek(timestamp, null, out entry);
        }

        public bool TryPeek(
            double timestamp,
            InputBufferEntryPredicate<TAction> predicate,
            out InputBufferEntry<TAction> entry)
        {
            ValidateTimestamp(timestamp);
            PruneExpired(timestamp);

            int index = FindSelectionIndex(predicate);
            if (index < 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            return true;
        }

        public bool TryConsume(double timestamp, out InputBufferEntry<TAction> entry)
        {
            return TryConsume(timestamp, null, out entry);
        }

        public bool TryConsume(
            double timestamp,
            InputBufferEntryPredicate<TAction> predicate,
            out InputBufferEntry<TAction> entry)
        {
            ValidateTimestamp(timestamp);
            PruneExpired(timestamp);

            int index = FindSelectionIndex(predicate);
            if (index < 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            RemoveAt(index);
            return true;
        }

        public int PruneExpired(double timestamp)
        {
            ValidateTimestamp(timestamp);

            int removed = 0;
            for (int i = _count - 1; i >= 0; i--)
            {
                if (!_entries[i].IsExpired(timestamp))
                {
                    continue;
                }

                RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public void Clear()
        {
            Array.Clear(_entries, 0, _count);
            _count = 0;
        }

        private bool TryRefresh(
            int index,
            double timestamp,
            InputBufferWriteOptions options)
        {
            double expiresAt = timestamp + options.Lifetime;
            if (double.IsInfinity(expiresAt))
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            InputBufferEntry<TAction> existing = _entries[index];
            _entries[index] = new InputBufferEntry<TAction>(
                existing.Sequence,
                existing.Action,
                timestamp,
                expiresAt,
                options.Priority);
            return true;
        }

        private bool MakeRoom(int incomingPriority)
        {
            switch (_overflowPolicy)
            {
                case InputBufferOverflowPolicy.RejectIncoming:
                    return false;

                case InputBufferOverflowPolicy.DropOldest:
                    RemoveAt(FindOldestIndex());
                    return true;

                case InputBufferOverflowPolicy.DropLowestPriority:
                    int index = FindLowestPriorityIndex();
                    if (incomingPriority < _entries[index].Priority)
                    {
                        return false;
                    }

                    RemoveAt(index);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private int FindMatchingIndex(TAction action)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_comparer.Equals(_entries[i].Action, action))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveMatching(TAction action)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                if (_comparer.Equals(_entries[i].Action, action))
                {
                    RemoveAt(i);
                }
            }
        }

        private int FindSelectionIndex(InputBufferEntryPredicate<TAction> predicate)
        {
            int selectedIndex = -1;
            for (int i = 0; i < _count; i++)
            {
                InputBufferEntry<TAction> candidate = _entries[i];
                if (predicate != null && !predicate(in candidate))
                {
                    continue;
                }

                if (selectedIndex < 0 || IsPreferred(candidate, _entries[selectedIndex]))
                {
                    selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        private bool IsPreferred(
            InputBufferEntry<TAction> candidate,
            InputBufferEntry<TAction> selected)
        {
            switch (_selectionPolicy)
            {
                case InputBufferSelectionPolicy.OldestFirst:
                    return IsOlder(candidate, selected);

                case InputBufferSelectionPolicy.NewestFirst:
                    return IsNewer(candidate, selected);

                case InputBufferSelectionPolicy.HighestPriorityThenOldest:
                    return candidate.Priority != selected.Priority
                        ? candidate.Priority > selected.Priority
                        : IsOlder(candidate, selected);

                case InputBufferSelectionPolicy.HighestPriorityThenNewest:
                    return candidate.Priority != selected.Priority
                        ? candidate.Priority > selected.Priority
                        : IsNewer(candidate, selected);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private int FindOldestIndex()
        {
            int selectedIndex = 0;
            for (int i = 1; i < _count; i++)
            {
                if (IsOlder(_entries[i], _entries[selectedIndex]))
                {
                    selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        private int FindLowestPriorityIndex()
        {
            int selectedIndex = 0;
            for (int i = 1; i < _count; i++)
            {
                InputBufferEntry<TAction> candidate = _entries[i];
                InputBufferEntry<TAction> selected = _entries[selectedIndex];
                if (candidate.Priority < selected.Priority ||
                    (candidate.Priority == selected.Priority && IsOlder(candidate, selected)))
                {
                    selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        private void RemoveAt(int index)
        {
            int moveCount = _count - index - 1;
            if (moveCount > 0)
            {
                Array.Copy(_entries, index + 1, _entries, index, moveCount);
            }

            _count--;
            _entries[_count] = default;
        }

        private long NextSequence()
        {
            if (_nextSequence == long.MaxValue)
            {
                _nextSequence = 0;
            }

            return ++_nextSequence;
        }

        private static bool IsOlder(
            InputBufferEntry<TAction> candidate,
            InputBufferEntry<TAction> selected)
        {
            return candidate.ReceivedAt < selected.ReceivedAt ||
                   (candidate.ReceivedAt.Equals(selected.ReceivedAt) &&
                    candidate.Sequence < selected.Sequence);
        }

        private static bool IsNewer(
            InputBufferEntry<TAction> candidate,
            InputBufferEntry<TAction> selected)
        {
            return candidate.ReceivedAt > selected.ReceivedAt ||
                   (candidate.ReceivedAt.Equals(selected.ReceivedAt) &&
                    candidate.Sequence > selected.Sequence);
        }

        private static void ValidateTimestamp(double timestamp)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp))
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp));
            }
        }

        private static void ValidateWriteOptions(InputBufferWriteOptions options)
        {
            if (double.IsNaN(options.Lifetime) ||
                double.IsInfinity(options.Lifetime) ||
                options.Lifetime <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }
        }
    }
}
