using System;
using UnityEngine;

namespace _App
{
    public enum CombatTrainingAttackPhase
    {
        Waiting,
        Windup,
        Active,
        Recovery,
        ParryStagger
    }

    public enum CombatTrainingAttackReaction
    {
        Light,
        Heavy
    }

    public readonly struct CombatTrainingAttackSnapshot
    {
        public CombatTrainingAttackPhase Phase { get; }
        public float NormalizedTime { get; }
        public bool HasConfirmedHit { get; }

        public CombatTrainingAttackSnapshot(
            CombatTrainingAttackPhase phase,
            float normalizedTime,
            bool hasConfirmedHit)
        {
            Phase = phase;
            NormalizedTime = Mathf.Clamp01(normalizedTime);
            HasConfirmedHit = hasConfirmedHit;
        }
    }

    [Serializable]
    public sealed class CombatTrainingAttackSettings
    {
        [Header("Timeline")]
        [SerializeField, Min(0.0f)] private float _initialDelay = 1.25f;
        [SerializeField, Min(0.01f)] private float _windupDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float _activeDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float _recoveryDuration = 0.70f;
        [SerializeField, Min(0.0f)] private float _cooldownDuration = 1.20f;
        [SerializeField, Min(0.01f)] private float _parryStaggerDuration = 1.0f;

        [Header("Reach")]
        [SerializeField, Min(0.1f)] private float _range = 2.4f;
        [SerializeField, Min(0.0f)] private float _windupLeash = 0.75f;

        [Header("Hit")]
        [SerializeField, Min(1)] private int _damage = 10;
        [SerializeField, Min(0.0f)] private float _staggerDamage = 12.0f;
        [SerializeField, Min(0.0f)] private float _knockback = 2.5f;
        [SerializeField] private CombatTrainingAttackReaction _reaction =
            CombatTrainingAttackReaction.Light;

        [Header("Hit Stop")]
        [SerializeField] private bool _useHitStop = true;
        [SerializeField, Min(0.001f)] private float _hitStopDuration = 0.07f;
        [SerializeField, Range(0.0f, 1.0f)] private float _hitStopTimeScale = 0.10f;

        internal CombatTrainingAttackDefinition Compile()
        {
            float initialDelay = RequireNonNegative(_initialDelay, nameof(_initialDelay));
            float windup = RequirePositive(_windupDuration, nameof(_windupDuration));
            float active = RequirePositive(_activeDuration, nameof(_activeDuration));
            float recovery = RequirePositive(_recoveryDuration, nameof(_recoveryDuration));
            float cooldown = RequireNonNegative(_cooldownDuration, nameof(_cooldownDuration));
            float parryStagger = RequirePositive(
                _parryStaggerDuration,
                nameof(_parryStaggerDuration));
            float range = RequirePositive(_range, nameof(_range));
            float leash = RequireNonNegative(_windupLeash, nameof(_windupLeash));
            float stagger = RequireNonNegative(_staggerDamage, nameof(_staggerDamage));
            float knockback = RequireNonNegative(_knockback, nameof(_knockback));
            int damage = Mathf.Max(1, _damage);

            CombatReactionId reactionId = _reaction == CombatTrainingAttackReaction.Heavy
                ? CombatReactionIds.Heavy
                : CombatReactionIds.Light;
            CombatHitStop hitStop = _useHitStop
                ? new CombatHitStop(
                    RequirePositive(_hitStopDuration, nameof(_hitStopDuration)),
                    Mathf.Clamp01(_hitStopTimeScale))
                : CombatHitStop.Disabled;
            var hit = new CombatHitDefinition(
                damage,
                stagger,
                knockback,
                0.0f,
                reactionId,
                hitStop,
                CombatHitRepeatPolicy.OncePerAction);

            return new CombatTrainingAttackDefinition(
                initialDelay,
                windup,
                active,
                recovery,
                cooldown,
                parryStagger,
                range,
                leash,
                hit);
        }

        private static float RequirePositive(float value, string parameterName)
        {
            CombatActionNumeric.RequireFinite(value, parameterName);
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float RequireNonNegative(float value, string parameterName)
        {
            CombatActionNumeric.RequireFinite(value, parameterName);
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    internal readonly struct CombatTrainingAttackDefinition
    {
        public float InitialDelay { get; }
        public float WindupDuration { get; }
        public float ActiveDuration { get; }
        public float RecoveryDuration { get; }
        public float CooldownDuration { get; }
        public float ParryStaggerDuration { get; }
        public float Range { get; }
        public float WindupLeash { get; }
        public CombatHitDefinition Hit { get; }

        public CombatTrainingAttackDefinition(
            float initialDelay,
            float windupDuration,
            float activeDuration,
            float recoveryDuration,
            float cooldownDuration,
            float parryStaggerDuration,
            float range,
            float windupLeash,
            CombatHitDefinition hit)
        {
            InitialDelay = initialDelay;
            WindupDuration = windupDuration;
            ActiveDuration = activeDuration;
            RecoveryDuration = recoveryDuration;
            CooldownDuration = cooldownDuration;
            ParryStaggerDuration = parryStaggerDuration;
            Range = range;
            WindupLeash = windupLeash;
            Hit = hit;
        }
    }
}
