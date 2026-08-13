namespace _App
{
    internal sealed class CombatInputBuffer
    {
        private const float DefaultLifetime = 0.35f;

        private readonly ICombatClock _clock;

        private ESkillType _skillType;
        private float _expiresAt;
        private bool _hasValue;

        public CombatInputBuffer(ICombatClock clock)
        {
            _clock = clock;
        }

        public void Store(ESkillType skillType)
        {
            _skillType = skillType;
            _expiresAt = _clock.UnscaledTime + DefaultLifetime;
            _hasValue = true;
        }

        public bool TryConsume(out ESkillType skillType)
        {
            if (!_hasValue || _clock.UnscaledTime > _expiresAt)
            {
                Clear();
                skillType = default;
                return false;
            }

            skillType = _skillType;
            Clear();
            return true;
        }

        public void Clear()
        {
            _skillType = default;
            _expiresAt = 0.0f;
            _hasValue = false;
        }
    }
}
