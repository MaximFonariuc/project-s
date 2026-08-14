using System;
using System.Collections.Generic;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal enum PlayerAnimationSource
    {
        Equipment,
        Locomotion,
        Combat,
        Traversal,
        Reaction
    }

    internal sealed class PlayerAnimationCoordinator
    {
        private readonly struct RequestKey : IEquatable<RequestKey>
        {
            public PlayerAnimationSource Source { get; }
            public int LayerIndex { get; }

            public RequestKey(PlayerAnimationSource source, int layerIndex)
            {
                Source = source;
                LayerIndex = layerIndex;
            }

            public bool Equals(RequestKey other) =>
                Source == other.Source && LayerIndex == other.LayerIndex;

            public override bool Equals(object obj) => obj is RequestKey other && Equals(other);
            public override int GetHashCode() => ((int)Source * 397) ^ LayerIndex;
        }

        private readonly struct AnimationRequest
        {
            public AnimationStateBinding Binding { get; }
            public float ExpectedDuration { get; }

            public AnimationRequest(
                AnimationStateBinding binding,
                float expectedDuration)
            {
                Binding = binding;
                ExpectedDuration = expectedDuration;
            }
        }

        private readonly ICharacterAnimationDriver _driver;
        private readonly IAnimationStateCatalog _catalog;
        private readonly Dictionary<RequestKey, AnimationRequest> _requests =
            new Dictionary<RequestKey, AnimationRequest>();
        private readonly Dictionary<int, PlayerAnimationSource> _owners =
            new Dictionary<int, PlayerAnimationSource>();
        private readonly float[] _playbackSpeeds =
            new float[Enum.GetValues(typeof(PlayerAnimationSource)).Length];
        private readonly List<int> _releaseLayers = new List<int>(4);

        public PlayerAnimationCoordinator(
            ICharacterAnimationDriver driver,
            IAnimationStateCatalog catalog)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            for (int i = 0; i < _playbackSpeeds.Length; i++)
            {
                _playbackSpeeds[i] = 1.0f;
            }
        }

        public AnimationPlayback CrossFade(
            PlayerAnimationSource source,
            AnimationSlotId slot,
            float expectedDurationOverride = 0.0f)
        {
            if (!_catalog.TryResolve(slot, out AnimationStateBinding binding))
            {
                return AnimationPlayback.Invalid;
            }

            AnimationLayerId layer = binding.Layer;
            float expectedDuration = expectedDurationOverride > 0.0f
                ? expectedDurationOverride
                : binding.ExpectedDuration;
            var key = new RequestKey(source, layer.Index);
            bool hadPreviousRequest = _requests.TryGetValue(
                key,
                out AnimationRequest previousRequest);
            _requests[key] = new AnimationRequest(binding, expectedDuration);

            if (_owners.TryGetValue(layer.Index, out PlayerAnimationSource owner) &&
                owner != source &&
                GetPriority(owner) > GetPriority(source))
            {
                return AnimationPlayback.Invalid;
            }

            AnimationPlayback playback = _driver.CrossFade(
                binding.State,
                layer,
                binding.TransitionSeconds,
                expectedDuration);
            if (!playback.IsValid)
            {
                if (hadPreviousRequest)
                {
                    _requests[key] = previousRequest;
                }
                else
                {
                    _requests.Remove(key);
                }

                return AnimationPlayback.Invalid;
            }

            bool ownerChanged = !_owners.TryGetValue(layer.Index, out owner) || owner != source;
            _owners[layer.Index] = source;
            if (ownerChanged)
            {
                ApplyPlaybackSpeed(layer, source);
            }

            return playback;
        }

        public void SetPlaybackSpeed(PlayerAnimationSource source, float speed)
        {
            float clampedSpeed = Mathf.Max(0.0f, speed);
            _playbackSpeeds[(int)source] = clampedSpeed;
            if (_owners.TryGetValue(AnimationLayerId.Base.Index, out PlayerAnimationSource owner) &&
                owner == source)
            {
                _driver.SetPlaybackSpeed(clampedSpeed);
            }
        }

        private void Release(PlayerAnimationSource source, AnimationLayerId layer)
        {
            _requests.Remove(new RequestKey(source, layer.Index));
            if (!_owners.TryGetValue(layer.Index, out PlayerAnimationSource owner) || owner != source)
            {
                return;
            }

            RestoreHighestPriorityRequest(layer);
        }

        public void ReleaseAll(PlayerAnimationSource source)
        {
            _releaseLayers.Clear();
            foreach (KeyValuePair<RequestKey, AnimationRequest> pair in _requests)
            {
                if (pair.Key.Source == source && !_releaseLayers.Contains(pair.Key.LayerIndex))
                {
                    _releaseLayers.Add(pair.Key.LayerIndex);
                }
            }

            for (int i = 0; i < _releaseLayers.Count; i++)
            {
                Release(source, new AnimationLayerId(_releaseLayers[i]));
            }

            _playbackSpeeds[(int)source] = 1.0f;
        }

        public PlayerAnimationSource? GetOwner(AnimationLayerId layer)
        {
            return _owners.TryGetValue(layer.Index, out PlayerAnimationSource owner)
                ? owner
                : null;
        }

        private void RestoreHighestPriorityRequest(AnimationLayerId layer)
        {
            while (TryFindHighestPriorityRequest(
                       layer.Index,
                       out RequestKey key,
                       out AnimationRequest request))
            {
                AnimationPlayback playback = _driver.CrossFade(
                    request.Binding.State,
                    request.Binding.Layer,
                    request.Binding.TransitionSeconds,
                    request.ExpectedDuration);
                if (playback.IsValid)
                {
                    _owners[layer.Index] = key.Source;
                    ApplyPlaybackSpeed(layer, key.Source);
                    return;
                }

                _requests.Remove(key);
            }

            _owners.Remove(layer.Index);
            if (layer == AnimationLayerId.Base)
            {
                _driver.SetPlaybackSpeed(1.0f);
            }
        }

        private bool TryFindHighestPriorityRequest(
            int layerIndex,
            out RequestKey selectedKey,
            out AnimationRequest selectedRequest)
        {
            selectedKey = default;
            selectedRequest = default;
            int selectedPriority = int.MinValue;
            bool found = false;

            foreach (KeyValuePair<RequestKey, AnimationRequest> pair in _requests)
            {
                if (pair.Key.LayerIndex != layerIndex)
                {
                    continue;
                }

                int priority = GetPriority(pair.Key.Source);
                if (found && priority <= selectedPriority)
                {
                    continue;
                }

                found = true;
                selectedPriority = priority;
                selectedKey = pair.Key;
                selectedRequest = pair.Value;
            }

            return found;
        }

        private void ApplyPlaybackSpeed(
            AnimationLayerId layer,
            PlayerAnimationSource source)
        {
            if (layer == AnimationLayerId.Base)
            {
                _driver.SetPlaybackSpeed(_playbackSpeeds[(int)source]);
            }
        }

        private static int GetPriority(PlayerAnimationSource source)
        {
            return source switch
            {
                PlayerAnimationSource.Reaction => 300,
                PlayerAnimationSource.Traversal => 200,
                PlayerAnimationSource.Combat => 100,
                PlayerAnimationSource.Locomotion => 0,
                PlayerAnimationSource.Equipment => -100,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
            };
        }
    }
}
