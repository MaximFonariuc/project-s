using System;
using ModularInput;

namespace _App
{
    internal sealed class CombatInputBuffer
    {
        private readonly ICombatClock _clock;
        private readonly CombatInputBufferSettings _settings;
        private readonly InputBuffer<PlayerCombatCommandId> _buffer;

        public int Count => _buffer.Count;

        public CombatInputBuffer(
            ICombatClock clock,
            CombatInputBufferSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _buffer = new InputBuffer<PlayerCombatCommandId>(_settings.BufferOptions);
        }

        public bool Store(PlayerCombatCommandId commandId)
        {
            return _buffer.TryEnqueue(
                commandId,
                _clock.UnscaledTime,
                _settings.GetWriteOptions(commandId));
        }

        public bool TryConsume(out PlayerCombatCommandId commandId)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    out InputBufferEntry<PlayerCombatCommandId> entry))
            {
                commandId = default;
                return false;
            }

            commandId = entry.Action;
            return true;
        }

        public bool TryConsume(
            InputBufferEntryPredicate<PlayerCombatCommandId> predicate,
            out PlayerCombatCommandId commandId)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    predicate,
                    out InputBufferEntry<PlayerCombatCommandId> entry))
            {
                commandId = default;
                return false;
            }

            commandId = entry.Action;
            return true;
        }

        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
