using System;
using System.Collections.Generic;
using ModularAnimation;
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

        [Header("Optional Animation Mapping")]
        [SerializeField] private List<CharacterReactionAnimationAuthoring> _animations = new();

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

        internal Dictionary<CharacterReactionAnimationKey, CharacterReactionAnimationBinding>
            CreateAnimationMap()
        {
            var map = new Dictionary<CharacterReactionAnimationKey, CharacterReactionAnimationBinding>();
            if (_animations == null)
            {
                return map;
            }

            for (int i = 0; i < _animations.Count; i++)
            {
                CharacterReactionAnimationAuthoring authoring = _animations[i];
                if (authoring == null)
                {
                    throw new InvalidOperationException(
                        $"{name} has a null reaction animation at index {i}.");
                }

                CharacterReactionAnimationKey key = authoring.CreateKey();
                if (!map.TryAdd(key, authoring.CreateBinding()))
                {
                    throw new InvalidOperationException(
                        $"{name} has duplicate animation mapping for '{key}'.");
                }
            }

            return map;
        }
    }

    [Serializable]
    public sealed class CharacterReactionAnimationAuthoring
    {
        [Tooltip("Leave empty to use this entry as the fallback for the selected state.")]
        [SerializeField] private string _reactionId;
        [SerializeField] private CharacterReactionStateId _state = CharacterReactionStateId.HitStun;
        [SerializeField] private string _animationState = "HitLight";
        [SerializeField, Min(0)] private int _layer;
        [SerializeField, Min(0.0f)] private float _transitionDuration = 0.05f;
        [SerializeField, Min(0.0f)] private float _expectedDuration;

        internal CharacterReactionAnimationKey CreateKey()
        {
            if (_state == CharacterReactionStateId.Stable)
            {
                throw new InvalidOperationException("Stable state cannot have a reaction animation mapping.");
            }

            CombatReactionId reactionId = string.IsNullOrWhiteSpace(_reactionId)
                ? CombatReactionId.None
                : new CombatReactionId(_reactionId);
            return new CharacterReactionAnimationKey(reactionId, _state);
        }

        internal CharacterReactionAnimationBinding CreateBinding()
        {
            if (string.IsNullOrWhiteSpace(_animationState))
            {
                throw new InvalidOperationException("Reaction animation state cannot be empty.");
            }

            return new CharacterReactionAnimationBinding(
                new AnimationStateId(Animator.StringToHash(_animationState.Trim())),
                new AnimationLayerId(_layer),
                _transitionDuration,
                _expectedDuration);
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

    internal readonly struct CharacterReactionAnimationKey :
        IEquatable<CharacterReactionAnimationKey>
    {
        public CombatReactionId ReactionId { get; }
        public CharacterReactionStateId State { get; }

        public CharacterReactionAnimationKey(
            CombatReactionId reactionId,
            CharacterReactionStateId state)
        {
            ReactionId = reactionId;
            State = state;
        }

        public bool Equals(CharacterReactionAnimationKey other)
        {
            return ReactionId.Equals(other.ReactionId) && State == other.State;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterReactionAnimationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ReactionId.GetHashCode() * 397) ^ (int)State;
            }
        }

        public override string ToString()
        {
            string reaction = ReactionId.IsValid ? ReactionId.Value : "*";
            return $"{reaction}:{State}";
        }
    }

    internal readonly struct CharacterReactionAnimationBinding
    {
        public AnimationStateId State { get; }
        public AnimationLayerId Layer { get; }
        public float TransitionDuration { get; }
        public float ExpectedDuration { get; }

        public CharacterReactionAnimationBinding(
            AnimationStateId state,
            AnimationLayerId layer,
            float transitionDuration,
            float expectedDuration)
        {
            State = state;
            Layer = layer;
            TransitionDuration = Mathf.Max(0.0f, transitionDuration);
            ExpectedDuration = Mathf.Max(0.0f, expectedDuration);
        }
    }
}
