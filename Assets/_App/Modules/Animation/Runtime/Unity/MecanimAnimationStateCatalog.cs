using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAnimation.Unity
{
    internal readonly struct MecanimAnimationStateDefinition
    {
        public AnimationSlotId Slot { get; }
        public string State { get; }
        public string Layer { get; }
        public float TransitionSeconds { get; }
        public float ExpectedDuration { get; }
        public AnimationSlotId FallbackSlot { get; }
        public bool Required { get; }

        public MecanimAnimationStateDefinition(
            AnimationSlotId slot,
            string state,
            string layer,
            float transitionSeconds,
            float expectedDuration,
            AnimationSlotId fallbackSlot,
            bool required)
        {
            Slot = slot;
            State = state?.Trim();
            Layer = layer?.Trim();
            TransitionSeconds = Mathf.Max(0.0f, transitionSeconds);
            ExpectedDuration = Mathf.Max(0.0f, expectedDuration);
            FallbackSlot = fallbackSlot;
            Required = required;
        }
    }

    public sealed class MecanimAnimationStateCatalog : IAnimationStateCatalog
    {
        private readonly Animator _animator;
        private readonly Dictionary<AnimationSlotId, MecanimAnimationStateDefinition> _definitions;
        private readonly Dictionary<AnimationSlotId, AnimationStateBinding> _resolved;
        private readonly HashSet<AnimationSlotId> _unavailable;

        internal MecanimAnimationStateCatalog(
            Animator animator,
            IReadOnlyList<MecanimAnimationStateDefinition> definitions)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _definitions = new Dictionary<AnimationSlotId, MecanimAnimationStateDefinition>();
            _resolved = new Dictionary<AnimationSlotId, AnimationStateBinding>();
            _unavailable = new HashSet<AnimationSlotId>();

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

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

            if (TryCreateBinding(definition, out binding))
            {
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

        private bool TryCreateBinding(
            in MecanimAnimationStateDefinition definition,
            out AnimationStateBinding binding)
        {
            if (string.IsNullOrWhiteSpace(definition.State) ||
                string.IsNullOrWhiteSpace(definition.Layer))
            {
                binding = default;
                return false;
            }

            int layerIndex = _animator.GetLayerIndex(definition.Layer);
            if (layerIndex < 0)
            {
                binding = default;
                return false;
            }

            string fullPath = definition.State.StartsWith(definition.Layer + ".", StringComparison.Ordinal)
                ? definition.State
                : definition.Layer + "." + definition.State;
            var state = new AnimationStateId(Animator.StringToHash(fullPath));
            if (!_animator.HasState(layerIndex, state.Hash))
            {
                state = new AnimationStateId(Animator.StringToHash(definition.State));
                if (!_animator.HasState(layerIndex, state.Hash))
                {
                    binding = default;
                    return false;
                }
            }

            binding = new AnimationStateBinding(
                state,
                new AnimationLayerId(layerIndex),
                definition.TransitionSeconds,
                definition.ExpectedDuration);
            return true;
        }
    }
}
