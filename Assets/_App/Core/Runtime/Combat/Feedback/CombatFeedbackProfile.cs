using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "CombatFeedbackProfile",
        menuName = "Combat/Feedback Profile")]
    public sealed class CombatFeedbackProfile : ScriptableObject
    {
        [Header("Camera")]
        [SerializeField, Min(0.01f)] private float _cameraImpulseDuration = 0.12f;

        [Header("Fallback")]
        [SerializeField] private CombatFeedbackAuthoring _defaultFeedback =
            CombatFeedbackAuthoring.CreateDefault();

        [Header("Defense Outcomes")]
        [SerializeField] private CombatFeedbackAuthoring _blockedFeedback =
            CombatFeedbackAuthoring.CreateBlocked();
        [SerializeField] private CombatFeedbackAuthoring _parriedFeedback =
            CombatFeedbackAuthoring.CreateParried();

        [Header("Reaction Overrides")]
        [SerializeField] private List<CombatFeedbackAuthoring> _reactionOverrides = new();

        internal CombatFeedbackSettings CreateSettings()
        {
            if (_defaultFeedback == null)
            {
                throw new InvalidOperationException(
                    $"Combat feedback profile '{name}' has no default feedback.");
            }

            if (_blockedFeedback == null || _parriedFeedback == null)
            {
                throw new InvalidOperationException(
                    $"Combat feedback profile '{name}' requires block and parry feedback.");
            }

            var overrides = new List<CombatFeedbackDefinition>(
                _reactionOverrides?.Count ?? 0);
            if (_reactionOverrides != null)
            {
                for (int i = 0; i < _reactionOverrides.Count; i++)
                {
                    CombatFeedbackAuthoring authoring = _reactionOverrides[i];
                    if (authoring == null)
                    {
                        throw new InvalidOperationException(
                            $"Combat feedback profile '{name}' has a null override at index {i}.");
                    }

                    overrides.Add(authoring.Build(requireReactionId: true));
                }
            }

            return new CombatFeedbackSettings(
                _cameraImpulseDuration,
                _defaultFeedback.Build(requireReactionId: false),
                _blockedFeedback.Build(requireReactionId: false),
                _parriedFeedback.Build(requireReactionId: false),
                overrides);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                CreateSettings();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Combat feedback profile '{name}' is invalid: {exception.Message}",
                    this);
            }
        }
#endif
    }

    [Serializable]
    public sealed class CombatFeedbackAuthoring
    {
        [Tooltip("Leave empty only for the fallback entry.")]
        [SerializeField] private string _reactionId;

        [Header("Camera")]
        [SerializeField, Min(0.0f)] private float _cameraImpulse = 0.1f;

        [Header("Impact Particles")]
        [SerializeField, Range(0, 64)] private int _particleCount = 8;
        [SerializeField, Min(0.0f)] private float _particleSpeed = 2.5f;
        [SerializeField, Range(0.0f, 1.0f)] private float _particleScatter = 0.7f;
        [SerializeField, Min(0.01f)] private float _particleLifetime = 0.18f;
        [SerializeField, Min(0.001f)] private float _particleSize = 0.035f;
        [SerializeField] private Color _particleColor = new Color(1.0f, 0.75f, 0.25f, 1.0f);

        [Header("Impact Audio")]
        [SerializeField] private AudioClip _audioClip;
        [SerializeField, Range(0.0f, 1.0f)] private float _audioVolume = 0.8f;
        [SerializeField, Range(0.25f, 2.0f)] private float _audioPitchMin = 0.96f;
        [SerializeField, Range(0.25f, 2.0f)] private float _audioPitchMax = 1.04f;

        [Header("Hit Stop")]
        [SerializeField] private bool _overrideHitStop;
        [SerializeField, Min(0.001f)] private float _hitStopDuration = 0.06f;
        [SerializeField, Range(0.0f, 1.0f)] private float _hitStopTimeScale = 0.1f;

        internal CombatFeedbackDefinition Build(bool requireReactionId)
        {
            CombatReactionId reactionId;
            if (string.IsNullOrWhiteSpace(_reactionId))
            {
                if (requireReactionId)
                {
                    throw new InvalidOperationException(
                        "A combat feedback override requires a reaction id.");
                }

                reactionId = CombatReactionId.None;
            }
            else
            {
                if (!requireReactionId)
                {
                    throw new InvalidOperationException(
                        "The default combat feedback cannot have a reaction id.");
                }

                reactionId = new CombatReactionId(_reactionId);
            }

            CombatHitStop hitStopOverride = _overrideHitStop
                ? new CombatHitStop(_hitStopDuration, _hitStopTimeScale)
                : CombatHitStop.Disabled;
            return new CombatFeedbackDefinition(
                reactionId,
                _cameraImpulse,
                _particleCount,
                _particleSpeed,
                _particleScatter,
                _particleLifetime,
                _particleSize,
                _particleColor,
                _audioClip,
                _audioVolume,
                _audioPitchMin,
                _audioPitchMax,
                _overrideHitStop,
                hitStopOverride);
        }

        internal static CombatFeedbackAuthoring CreateDefault()
        {
            return new CombatFeedbackAuthoring();
        }

        internal static CombatFeedbackAuthoring CreateBlocked()
        {
            return new CombatFeedbackAuthoring
            {
                _cameraImpulse = 0.06f,
                _particleCount = 10,
                _particleSpeed = 2.6f,
                _particleScatter = 0.55f,
                _particleLifetime = 0.18f,
                _particleSize = 0.035f,
                _particleColor = new Color(0.55f, 0.82f, 1.0f, 1.0f),
                _overrideHitStop = true,
                _hitStopDuration = 0.045f,
                _hitStopTimeScale = 0.18f
            };
        }

        internal static CombatFeedbackAuthoring CreateParried()
        {
            return new CombatFeedbackAuthoring
            {
                _cameraImpulse = 0.14f,
                _particleCount = 18,
                _particleSpeed = 3.8f,
                _particleScatter = 0.72f,
                _particleLifetime = 0.24f,
                _particleSize = 0.05f,
                _particleColor = new Color(0.72f, 0.96f, 1.0f, 1.0f),
                _overrideHitStop = true,
                _hitStopDuration = 0.09f,
                _hitStopTimeScale = 0.02f
            };
        }
    }
}
