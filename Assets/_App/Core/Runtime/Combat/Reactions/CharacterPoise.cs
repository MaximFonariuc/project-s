using System;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterPoise
    {
        private readonly CharacterReactionSettings _settings;
        private float _recoveryDelayRemaining;

        public float Current { get; private set; }
        public float Max => _settings.MaxPoise;
        public bool IsBroken => Current <= 0.0f;

        public CharacterPoise(CharacterReactionSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            RestoreFull();
        }

        public bool ApplyStagger(float amount)
        {
            if (amount <= 0.0f)
            {
                return false;
            }

            Current = Mathf.Max(0.0f, Current - amount);
            _recoveryDelayRemaining = _settings.PoiseRecoveryDelay;
            return IsBroken;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0.0f || IsBroken || Current >= Max)
            {
                return;
            }

            if (_recoveryDelayRemaining > 0.0f)
            {
                _recoveryDelayRemaining = Mathf.Max(0.0f, _recoveryDelayRemaining - deltaTime);
                return;
            }

            Current = Mathf.Min(Max, Current + _settings.PoiseRecoveryPerSecond * deltaTime);
        }

        public void Deplete()
        {
            Current = 0.0f;
            _recoveryDelayRemaining = 0.0f;
        }

        public void RestoreFull()
        {
            Current = Max;
            _recoveryDelayRemaining = 0.0f;
        }
    }
}
