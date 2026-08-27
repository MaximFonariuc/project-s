using System;
using System.Collections.Generic;
using ModularInput;

namespace _App
{
    internal sealed class CombatInputBuffer
    {
        private readonly ICombatClock _clock;
        private readonly CombatInputBufferSettings _settings;
        private readonly InputBuffer<PlayerCombatCommandSample> _buffer;

        public int Count => _buffer.Count;

        public CombatInputBuffer(
            ICombatClock clock,
            CombatInputBufferSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _buffer = new InputBuffer<PlayerCombatCommandSample>(
                _settings.BufferOptions,
                CommandSampleComparer.Instance);
        }

        public bool Store(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId sourceActionId,
            float sourceNormalizedTime)
        {
            return _buffer.TryEnqueue(
                new PlayerCombatCommandSample(
                    commandId,
                    sourceActionId,
                    sourceNormalizedTime),
                _clock.UnscaledTime,
                _settings.GetWriteOptions(commandId));
        }

        public bool TryConsume(out PlayerCombatCommandSample sample)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    out InputBufferEntry<PlayerCombatCommandSample> entry))
            {
                sample = default;
                return false;
            }

            sample = entry.Action;
            return true;
        }

        public bool TryConsume(
            InputBufferEntryPredicate<PlayerCombatCommandSample> predicate,
            out PlayerCombatCommandSample sample)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    predicate,
                    out InputBufferEntry<PlayerCombatCommandSample> entry))
            {
                sample = default;
                return false;
            }

            sample = entry.Action;
            return true;
        }

        public void Clear()
        {
            _buffer.Clear();
        }

        private sealed class CommandSampleComparer : IEqualityComparer<PlayerCombatCommandSample>
        {
            public static CommandSampleComparer Instance { get; } = new CommandSampleComparer();

            public bool Equals(PlayerCombatCommandSample left, PlayerCombatCommandSample right)
            {
                return left.CommandId == right.CommandId;
            }

            public int GetHashCode(PlayerCombatCommandSample sample)
            {
                return sample.CommandId.GetHashCode();
            }
        }
    }
}
