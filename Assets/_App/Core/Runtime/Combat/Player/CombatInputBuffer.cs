using System;
using ModularInput;

namespace _App
{
    internal sealed class CombatInputBuffer
    {
        private readonly ICombatClock _clock;
        private readonly CombatInputBufferSettings _settings;
        private readonly InputBuffer<ESkillType> _buffer;

        public int Count => _buffer.Count;

        public CombatInputBuffer(
            ICombatClock clock,
            CombatInputBufferSettings settings)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _buffer = new InputBuffer<ESkillType>(_settings.BufferOptions);
        }

        public bool Store(ESkillType skillType)
        {
            return _buffer.TryEnqueue(
                skillType,
                _clock.UnscaledTime,
                _settings.GetWriteOptions(skillType));
        }

        public bool TryConsume(out ESkillType skillType)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    out InputBufferEntry<ESkillType> entry))
            {
                skillType = default;
                return false;
            }

            skillType = entry.Action;
            return true;
        }

        public bool TryConsume(
            InputBufferEntryPredicate<ESkillType> predicate,
            out ESkillType skillType)
        {
            if (!_buffer.TryConsume(
                    _clock.UnscaledTime,
                    predicate,
                    out InputBufferEntry<ESkillType> entry))
            {
                skillType = default;
                return false;
            }

            skillType = entry.Action;
            return true;
        }

        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
