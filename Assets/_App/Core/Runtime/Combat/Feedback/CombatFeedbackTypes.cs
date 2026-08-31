using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    internal sealed class CombatFeedbackDefinition
    {
        public CombatReactionId ReactionId { get; }
        public float CameraImpulse { get; }
        public int ParticleCount { get; }
        public float ParticleSpeed { get; }
        public float ParticleScatter { get; }
        public float ParticleLifetime { get; }
        public float ParticleSize { get; }
        public Color ParticleColor { get; }
        public AudioClip AudioClip { get; }
        public float AudioVolume { get; }
        public float AudioPitchMin { get; }
        public float AudioPitchMax { get; }
        public bool OverridesHitStop { get; }
        public CombatHitStop HitStopOverride { get; }

        public bool HasParticles => ParticleCount > 0;
        public bool HasAudio => AudioClip != null && AudioVolume > 0.0f;

        public CombatFeedbackDefinition(
            CombatReactionId reactionId,
            float cameraImpulse,
            int particleCount,
            float particleSpeed,
            float particleScatter,
            float particleLifetime,
            float particleSize,
            Color particleColor,
            AudioClip audioClip,
            float audioVolume,
            float audioPitchMin,
            float audioPitchMax,
            bool overridesHitStop,
            CombatHitStop hitStopOverride)
        {
            RequireFinite(cameraImpulse, nameof(cameraImpulse));
            RequireFinite(particleSpeed, nameof(particleSpeed));
            RequireFinite(particleScatter, nameof(particleScatter));
            RequireFinite(particleLifetime, nameof(particleLifetime));
            RequireFinite(particleSize, nameof(particleSize));
            RequireFinite(particleColor.r, nameof(particleColor));
            RequireFinite(particleColor.g, nameof(particleColor));
            RequireFinite(particleColor.b, nameof(particleColor));
            RequireFinite(particleColor.a, nameof(particleColor));
            RequireFinite(audioVolume, nameof(audioVolume));
            RequireFinite(audioPitchMin, nameof(audioPitchMin));
            RequireFinite(audioPitchMax, nameof(audioPitchMax));

            if (cameraImpulse < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraImpulse));
            }

            if (particleCount < 0 || particleCount > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(particleCount));
            }

            if (particleSpeed < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(particleSpeed));
            }

            if (particleScatter < 0.0f || particleScatter > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(particleScatter));
            }

            if (particleLifetime <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(particleLifetime));
            }

            if (particleSize <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(particleSize));
            }

            if (audioVolume < 0.0f || audioVolume > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(audioVolume));
            }

            if (audioPitchMin <= 0.0f || audioPitchMax < audioPitchMin)
            {
                throw new ArgumentOutOfRangeException(nameof(audioPitchMin));
            }

            if (overridesHitStop != hitStopOverride.IsEnabled)
            {
                throw new ArgumentException(
                    "Hit-stop override flag and value must describe the same policy.",
                    nameof(hitStopOverride));
            }

            ReactionId = reactionId;
            CameraImpulse = cameraImpulse;
            ParticleCount = particleCount;
            ParticleSpeed = particleSpeed;
            ParticleScatter = particleScatter;
            ParticleLifetime = particleLifetime;
            ParticleSize = particleSize;
            ParticleColor = particleColor;
            AudioClip = audioClip;
            AudioVolume = audioVolume;
            AudioPitchMin = audioPitchMin;
            AudioPitchMax = audioPitchMax;
            OverridesHitStop = overridesHitStop;
            HitStopOverride = hitStopOverride;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Combat feedback values must be finite.");
            }
        }
    }

    internal sealed class CombatFeedbackSettings
    {
        private readonly Dictionary<CombatReactionId, CombatFeedbackDefinition> _overrides;

        public float CameraImpulseDuration { get; }
        public CombatFeedbackDefinition Default { get; }
        public CombatFeedbackDefinition Blocked { get; }
        public CombatFeedbackDefinition Parried { get; }

        public CombatFeedbackSettings(
            float cameraImpulseDuration,
            CombatFeedbackDefinition defaultFeedback,
            CombatFeedbackDefinition blockedFeedback,
            CombatFeedbackDefinition parriedFeedback,
            IReadOnlyList<CombatFeedbackDefinition> overrides)
        {
            CombatActionNumeric.RequireFinite(
                cameraImpulseDuration,
                nameof(cameraImpulseDuration));
            if (cameraImpulseDuration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraImpulseDuration));
            }

            Default = defaultFeedback ?? throw new ArgumentNullException(nameof(defaultFeedback));
            Blocked = blockedFeedback ?? throw new ArgumentNullException(nameof(blockedFeedback));
            Parried = parriedFeedback ?? throw new ArgumentNullException(nameof(parriedFeedback));
            if (Default.ReactionId.IsValid)
            {
                throw new ArgumentException(
                    "The default combat feedback must not have a reaction id.",
                    nameof(defaultFeedback));
            }

            if (Blocked.ReactionId.IsValid || Parried.ReactionId.IsValid)
            {
                throw new ArgumentException(
                    "Defense feedback must be selected by hit outcome, not reaction id.");
            }

            CameraImpulseDuration = cameraImpulseDuration;
            _overrides = new Dictionary<CombatReactionId, CombatFeedbackDefinition>();
            if (overrides == null)
            {
                return;
            }

            for (int i = 0; i < overrides.Count; i++)
            {
                CombatFeedbackDefinition definition = overrides[i] ??
                    throw new ArgumentException(
                        $"Combat feedback override at index {i} is null.",
                        nameof(overrides));
                if (!definition.ReactionId.IsValid)
                {
                    throw new ArgumentException(
                        $"Combat feedback override at index {i} has no reaction id.",
                        nameof(overrides));
                }

                if (!_overrides.TryAdd(definition.ReactionId, definition))
                {
                    throw new ArgumentException(
                        $"Combat feedback reaction '{definition.ReactionId}' is duplicated.",
                        nameof(overrides));
                }
            }
        }

        public CombatFeedbackDefinition Resolve(in CombatHitResult hit)
        {
            if (hit.Outcome == CombatHitOutcome.Blocked)
            {
                return Blocked;
            }

            if (hit.Outcome == CombatHitOutcome.Parried)
            {
                return Parried;
            }

            if (hit.Outcome != CombatHitOutcome.Damaged)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hit),
                    hit.Outcome,
                    "Unsupported combat hit outcome.");
            }

            CombatReactionId reactionId = hit.ReactionId;
            return reactionId.IsValid && _overrides.TryGetValue(reactionId, out var definition)
                ? definition
                : Default;
        }

        public CombatHitStop ResolveHitStop(in CombatHitResult hit)
        {
            CombatFeedbackDefinition definition = Resolve(in hit);
            return definition.OverridesHitStop
                ? definition.HitStopOverride
                : hit.HitStop;
        }
    }
}
