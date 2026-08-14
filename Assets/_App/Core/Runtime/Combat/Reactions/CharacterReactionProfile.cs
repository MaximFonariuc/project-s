using System;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "CharacterReactionProfile",
        menuName = "Combat/Character Reaction Profile")]
    public sealed class CharacterReactionProfile : ScriptableObject
    {
        [Header("Poise")]
        [SerializeField, Min(0.01f)] private float _maxPoise = 10.0f;
        [SerializeField, Min(0.0f)] private float _poiseRecoveryDelay = 1.25f;
        [SerializeField, Min(0.0f)] private float _poiseRecoveryPerSecond = 10.0f;

        [Header("Reaction Timing")]
        [SerializeField, Min(0.01f)] private float _hitStunDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float _knockdownDuration = 0.8f;
        [SerializeField, Min(0.01f)] private float _airborneDuration = 0.45f;
        [SerializeField, Min(0.01f)] private float _knockdownThreshold = 6.0f;

        internal CharacterReactionSettings CreateSettings()
        {
            return new CharacterReactionSettings(
                _maxPoise,
                _poiseRecoveryDelay,
                _poiseRecoveryPerSecond,
                _hitStunDuration,
                _knockdownDuration,
                _airborneDuration,
                _knockdownThreshold);
        }
    }

    internal sealed class CharacterReactionSettings
    {
        public float MaxPoise { get; }
        public float PoiseRecoveryDelay { get; }
        public float PoiseRecoveryPerSecond { get; }
        public float HitStunDuration { get; }
        public float KnockdownDuration { get; }
        public float AirborneDuration { get; }
        public float KnockdownThreshold { get; }

        public CharacterReactionSettings(
            float maxPoise,
            float poiseRecoveryDelay,
            float poiseRecoveryPerSecond,
            float hitStunDuration,
            float knockdownDuration,
            float airborneDuration,
            float knockdownThreshold)
        {
            MaxPoise = RequirePositive(maxPoise, nameof(maxPoise));
            PoiseRecoveryDelay = RequireNonNegative(poiseRecoveryDelay, nameof(poiseRecoveryDelay));
            PoiseRecoveryPerSecond = RequireNonNegative(
                poiseRecoveryPerSecond,
                nameof(poiseRecoveryPerSecond));
            HitStunDuration = RequirePositive(hitStunDuration, nameof(hitStunDuration));
            KnockdownDuration = RequirePositive(knockdownDuration, nameof(knockdownDuration));
            AirborneDuration = RequirePositive(airborneDuration, nameof(airborneDuration));
            KnockdownThreshold = RequirePositive(knockdownThreshold, nameof(knockdownThreshold));
        }

        public static CharacterReactionSettings CreateDefaults()
        {
            return new CharacterReactionSettings(
                10.0f,
                1.25f,
                10.0f,
                0.25f,
                0.8f,
                0.45f,
                6.0f);
        }

        private static float RequirePositive(float value, string parameterName)
        {
            return value > 0.0f
                ? value
                : throw new ArgumentOutOfRangeException(parameterName);
        }

        private static float RequireNonNegative(float value, string parameterName)
        {
            return value >= 0.0f
                ? value
                : throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
