using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAnimation.Unity
{
    internal readonly struct MecanimAnimationStateDefinition
    {
        public AnimationSlotId Slot { get; }
        public bool UsesLocomotion { get; }
        public AnimationClip Clip { get; }
        public float TransitionSeconds { get; }
        public float ExpectedDuration { get; }
        public AnimationSlotId FallbackSlot { get; }
        public bool Required { get; }
        public bool ApplyFootIk { get; }

        public MecanimAnimationStateDefinition(
            AnimationSlotId slot,
            bool usesLocomotion,
            AnimationClip clip,
            float transitionSeconds,
            float expectedDuration,
            AnimationSlotId fallbackSlot,
            bool required,
            bool applyFootIk)
        {
            Slot = slot;
            UsesLocomotion = usesLocomotion;
            Clip = clip;
            TransitionSeconds = Mathf.Max(0.0f, transitionSeconds);
            ExpectedDuration = Mathf.Max(0.0f, expectedDuration);
            FallbackSlot = fallbackSlot;
            Required = required;
            ApplyFootIk = applyFootIk;
        }
    }

    public readonly struct MecanimAnimationGraphState
    {
        public bool UsesLocomotion { get; }
        public AnimationClip Clip { get; }
        public bool ApplyFootIk { get; }
        public float Duration { get; }

        internal MecanimAnimationGraphState(
            bool usesLocomotion,
            AnimationClip clip,
            bool applyFootIk,
            float expectedDuration)
        {
            UsesLocomotion = usesLocomotion;
            Clip = clip;
            ApplyFootIk = applyFootIk;
            Duration = expectedDuration > 0.0f
                ? expectedDuration
                : clip != null ? clip.length : 0.0f;
        }
    }

    public sealed class MecanimAnimationGraphConfiguration : IAnimationStateCatalog
    {
        public const int DirectionCount = 4;
        public const int GaitCount = 3;
        public const int LocomotionInputCount = 1 + DirectionCount * GaitCount;

        private readonly AnimationClip[] _locomotionClips =
            new AnimationClip[LocomotionInputCount];
        private readonly Dictionary<AnimationSlotId, MecanimAnimationStateDefinition> _definitions =
            new Dictionary<AnimationSlotId, MecanimAnimationStateDefinition>();
        private readonly Dictionary<AnimationSlotId, AnimationStateBinding> _resolved =
            new Dictionary<AnimationSlotId, AnimationStateBinding>();
        private readonly Dictionary<int, MecanimAnimationGraphState> _states =
            new Dictionary<int, MecanimAnimationGraphState>();
        private readonly Dictionary<int, AnimationSlotId> _stateSlots =
            new Dictionary<int, AnimationSlotId>();
        private readonly HashSet<AnimationSlotId> _unavailable = new HashSet<AnimationSlotId>();
        private readonly Animator _animator;

        public float LocomotionBlendSeconds { get; }

        internal MecanimAnimationGraphConfiguration(
            Animator animator,
            MecanimLocomotionClipSet locomotion,
            IReadOnlyList<MecanimAnimationStateDefinition> definitions)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            if (_animator.avatar == null || !_animator.avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a valid Generic or Humanoid Avatar.");
            }

            if (locomotion == null)
            {
                throw new ArgumentNullException(nameof(locomotion));
            }

            LocomotionBlendSeconds = locomotion.BlendSeconds;
            BuildLocomotion(locomotion);
            AddDefinitions(definitions ?? throw new ArgumentNullException(nameof(definitions)));
            ValidateRigCompatibility();

            foreach (KeyValuePair<AnimationSlotId, MecanimAnimationStateDefinition> pair in _definitions)
            {
                if (pair.Value.Required &&
                    !TryResolveInternal(pair.Key, new HashSet<AnimationSlotId>(), out _))
                {
                    throw new InvalidOperationException(
                        $"Required animation slot '{pair.Key}' cannot be resolved for Animator '{_animator.name}'.");
                }
            }
        }

        public AnimationClip GetLocomotionClip(int input)
        {
            if (input < 0 || input >= _locomotionClips.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(input));
            }

            return _locomotionClips[input];
        }

        public bool TryGetState(AnimationStateId state, out MecanimAnimationGraphState graphState)
        {
            if (state.IsValid && _states.TryGetValue(state.Hash, out graphState))
            {
                return true;
            }

            graphState = default;
            return false;
        }

        public bool TryResolve(AnimationSlotId slot, out AnimationStateBinding binding)
        {
            if (!slot.IsValid)
            {
                binding = default;
                return false;
            }

            if (_resolved.TryGetValue(slot, out binding))
            {
                return true;
            }

            if (_unavailable.Contains(slot) || !_definitions.ContainsKey(slot))
            {
                binding = default;
                return false;
            }

            return TryResolveInternal(slot, new HashSet<AnimationSlotId>(), out binding);
        }

        private void BuildLocomotion(MecanimLocomotionClipSet locomotion)
        {
            if (locomotion.Idle == null)
            {
                throw new InvalidOperationException("A locomotion profile requires an idle clip.");
            }

            _locomotionClips[0] = locomotion.Idle;
            CharacterGait[] gaits = { CharacterGait.Walk, CharacterGait.Run, CharacterGait.Sprint };
            for (int gait = 0; gait < gaits.Length; gait++)
            {
                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    _locomotionClips[1 + gait * DirectionCount + direction] =
                        locomotion.Get(gaits[gait], direction);
                }
            }
        }

        private void AddDefinitions(IReadOnlyList<MecanimAnimationStateDefinition> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                MecanimAnimationStateDefinition definition = definitions[i];
                if (!definition.Slot.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Animation profile has an empty semantic slot at index {i}.");
                }

                if (!_definitions.TryAdd(definition.Slot, definition))
                {
                    throw new InvalidOperationException(
                        $"Animation profile has duplicate semantic slot '{definition.Slot}'.");
                }
            }
        }

        private bool TryResolveInternal(
            AnimationSlotId slot,
            HashSet<AnimationSlotId> chain,
            out AnimationStateBinding binding)
        {
            if (_resolved.TryGetValue(slot, out binding))
            {
                return true;
            }

            if (_unavailable.Contains(slot) || !_definitions.TryGetValue(slot, out var definition))
            {
                binding = default;
                return false;
            }

            if (!chain.Add(slot))
            {
                throw new InvalidOperationException(
                    $"Animation fallback cycle detected at semantic slot '{slot}'.");
            }

            if (definition.UsesLocomotion || definition.Clip != null)
            {
                int hash = Animator.StringToHash(slot.Value);
                if (hash == 0)
                {
                    throw new InvalidOperationException(
                        $"Animation slot '{slot}' produced an invalid state identifier.");
                }

                if (_stateSlots.TryGetValue(hash, out AnimationSlotId existingSlot) &&
                    existingSlot != slot)
                {
                    throw new InvalidOperationException(
                        $"Animation slots '{existingSlot}' and '{slot}' produced the same state identifier.");
                }

                var state = new AnimationStateId(hash);
                _stateSlots[hash] = slot;
                _states[hash] = new MecanimAnimationGraphState(
                    definition.UsesLocomotion,
                    definition.Clip,
                    definition.ApplyFootIk,
                    definition.ExpectedDuration);
                binding = new AnimationStateBinding(
                    state,
                    AnimationLayerId.Base,
                    definition.TransitionSeconds,
                    _states[hash].Duration);
                _resolved.Add(slot, binding);
                chain.Remove(slot);
                return true;
            }

            if (definition.FallbackSlot.IsValid &&
                TryResolveInternal(definition.FallbackSlot, chain, out binding))
            {
                _resolved.Add(slot, binding);
                chain.Remove(slot);
                return true;
            }

            chain.Remove(slot);
            _unavailable.Add(slot);
            binding = default;
            return false;
        }

        private void ValidateRigCompatibility()
        {
            bool expectsHumanMotion = _animator.avatar.isHuman;
            var unique = new HashSet<AnimationClip>();
            for (int i = 0; i < _locomotionClips.Length; i++)
            {
                ValidateClip(_locomotionClips[i], expectsHumanMotion, unique);
            }

            foreach (MecanimAnimationStateDefinition definition in _definitions.Values)
            {
                ValidateClip(definition.Clip, expectsHumanMotion, unique);
            }
        }

        private void ValidateClip(
            AnimationClip clip,
            bool expectsHumanMotion,
            HashSet<AnimationClip> unique)
        {
            if (clip == null || !unique.Add(clip))
            {
                return;
            }

            if (clip.legacy)
            {
                throw new InvalidOperationException(
                    $"Animation clip '{clip.name}' is Legacy and cannot be used by the Playables backend.");
            }

            if (clip.humanMotion != expectsHumanMotion)
            {
                string rig = expectsHumanMotion ? "Humanoid" : "Generic";
                throw new InvalidOperationException(
                    $"Animation clip '{clip.name}' is incompatible with the {rig} Avatar on Animator '{_animator.name}'.");
            }
        }
    }
}
