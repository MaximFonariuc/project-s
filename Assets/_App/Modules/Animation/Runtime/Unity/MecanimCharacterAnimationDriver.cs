using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace ModularAnimation.Unity
{
    public sealed class MecanimCharacterAnimationDriver :
        ICharacterAnimationDriver,
        IAnimationClipActionDriver,
        IDisposable
    {
        private const int ControllerInput = 0;
        private const int FullBodyActionInput = 1;
        private const int MaskedActionInput = 2;
        private const int ActionLaneCount = 2;
        private const int ClipSlotCount = 2;

        private readonly Animator _animator;
        private readonly MecanimCharacterAnimationBindings _bindings;
        private readonly RuntimeAnimatorController _sourceController;
        private readonly AnimatorOverrideController _loadoutController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _baselineOverrides;
        private readonly HashSet<string> _overrideNames;
        private readonly Dictionary<int, float> _clipDurations = new Dictionary<int, float>();

        private readonly AnimationMixerPlayable[] _actionMixers =
            new AnimationMixerPlayable[ActionLaneCount];
        private readonly AnimationClipPlayable[,] _actionClips =
            new AnimationClipPlayable[ActionLaneCount, ClipSlotCount];
        private readonly bool[,] _actionClipValid = new bool[ActionLaneCount, ClipSlotCount];
        private readonly int[] _activeClipSlots = { -1, -1 };
        private readonly float[] _laneWeights = new float[ActionLaneCount];
        private readonly float[] _laneStartWeights = new float[ActionLaneCount];
        private readonly float[] _laneTargetWeights = new float[ActionLaneCount];

        private PlayableGraph _graph;
        private AnimatorControllerPlayable _controllerPlayable;
        private AnimationLayerMixerPlayable _layerMixer;

        private int _activeLane = -1;
        private int _targetLane = -1;
        private int _clipBlendLane = -1;
        private int _clipBlendFromSlot = -1;
        private int _clipBlendToSlot = -1;
        private float _clipBlendDuration;
        private float _clipBlendElapsed;
        private float _laneBlendDuration;
        private float _laneBlendElapsed;
        private int _nextActionSequence;

        private float _movementType;
        private float _movementDirection;
        private float _verticalSpeed;
        private float _movementTypeVelocity;
        private float _movementDirectionVelocity;
        private float _verticalSpeedVelocity;
        private bool _disposed;

        public Animator Animator => _animator;
        public bool IsActionPlaying =>
            _activeLane >= 0 || _laneWeights[0] > 0.0001f || _laneWeights[1] > 0.0001f;

        public MecanimCharacterAnimationDriver(
            Animator animator,
            MecanimCharacterAnimationBindings bindings)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            if (!bindings.IsValid)
            {
                throw new ArgumentException(
                    "Complete Mecanim character parameter bindings are required.",
                    nameof(bindings));
            }

            _bindings = bindings;
            _sourceController = _animator.runtimeAnimatorController != null
                ? _animator.runtimeAnimatorController
                : throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a RuntimeAnimatorController.");
            _loadoutController = new AnimatorOverrideController(_sourceController);
            _baselineOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(
                _loadoutController.overridesCount);
            _loadoutController.GetOverrides(_baselineOverrides);
            _overrideNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _baselineOverrides.Count; i++)
            {
                AnimationClip original = _baselineOverrides[i].Key;
                if (original == null)
                {
                    continue;
                }

                _overrideNames.Add(original.name);
                _baselineOverrides[i] =
                    new KeyValuePair<AnimationClip, AnimationClip>(original, original);
            }

            _animator.runtimeAnimatorController = _loadoutController;
            CreateGraph();
            RefreshBindings();
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            ThrowIfDisposed();

            float signedMovement = frame.NormalizedSpeed;
            if (Mathf.Abs(frame.ForwardSpeed) > 0.0001f)
            {
                signedMovement *= Mathf.Sign(frame.ForwardSpeed);
            }

            float movementTypeTarget = frame.NormalizedSpeed > 0.0001f
                ? (float)frame.Gait
                : 0.0f;
            float verticalSpeedTarget = frame.Mode == CharacterLocomotionMode.Airborne
                ? frame.VerticalSpeed
                : 0.0f;

            _movementType = Damp(
                _movementType,
                movementTypeTarget,
                ref _movementTypeVelocity,
                0.33f,
                deltaTime);
            _movementDirection = Damp(
                _movementDirection,
                signedMovement,
                ref _movementDirectionVelocity,
                0.1f,
                deltaTime);
            _verticalSpeed = Damp(
                _verticalSpeed,
                verticalSpeedTarget,
                ref _verticalSpeedVelocity,
                0.1f,
                deltaTime);

            _controllerPlayable.SetFloat(_bindings.MovementType.Hash, _movementType);
            _controllerPlayable.SetFloat(_bindings.MovementDirection.Hash, _movementDirection);
            _controllerPlayable.SetFloat(_bindings.VerticalSpeed.Hash, _verticalSpeed);
        }

        public AnimationPlayback CrossFade(
            AnimationStateId state,
            AnimationLayerId layer,
            float transitionSeconds,
            float expectedDuration = 0.0f)
        {
            ThrowIfDisposed();
            if (!state.IsValid || layer.Index >= _controllerPlayable.GetLayerCount())
            {
                return AnimationPlayback.Invalid;
            }

            if (!_controllerPlayable.HasState(layer.Index, state.Hash))
            {
                Debug.LogError(
                    $"Animator '{_animator.name}' has no state hash '{state.Hash}' on layer {layer.Index}.",
                    _animator);
                return AnimationPlayback.Invalid;
            }

            _controllerPlayable.CrossFadeInFixedTime(
                state.Hash,
                Mathf.Max(0.0f, transitionSeconds),
                layer.Index,
                0.0f);

            float duration = expectedDuration > 0.0f
                ? expectedDuration
                : GetClipDuration(state.Hash);
            return new AnimationPlayback(state, layer, duration);
        }

        public AnimationActionPlayback PlayAction(in AnimationClipActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid)
            {
                return AnimationActionPlayback.Invalid;
            }

            FinishClipBlend();

            int lane = request.AvatarMask == null ? 0 : 1;
            if (lane == 1)
            {
                _layerMixer.SetLayerMaskFromAvatarMask(MaskedActionInput, request.AvatarMask);
            }

            int currentSlot = _activeClipSlots[lane];
            if (_activeLane == lane && currentSlot >= 0 && _actionClipValid[lane, currentSlot])
            {
                int nextSlot = 1 - currentSlot;
                DestroyClip(lane, nextSlot);
                CreateActionClip(lane, nextSlot, in request);
                _actionMixers[lane].SetInputWeight(currentSlot, 1.0f);
                _actionMixers[lane].SetInputWeight(nextSlot, 0.0f);
                StartClipBlend(lane, currentSlot, nextSlot, request.BlendSeconds);
                if (_targetLane != lane)
                {
                    StartLaneBlend(lane, request.BlendSeconds);
                }
            }
            else
            {
                ClearLane(lane);
                CreateActionClip(lane, 0, in request);
                _activeClipSlots[lane] = 0;
                _actionMixers[lane].SetInputWeight(0, 1.0f);
                StartLaneBlend(lane, request.BlendSeconds);
            }

            _activeLane = lane;
            _nextActionSequence++;
            if (_nextActionSequence == 0)
            {
                _nextActionSequence = 1;
            }

            return new AnimationActionPlayback(
                _nextActionSequence,
                request.Clip.length / request.PlaybackSpeed);
        }

        public void StopAction(float blendSeconds)
        {
            if (_disposed || !IsActionPlaying)
            {
                return;
            }

            FinishClipBlend();
            StartLaneBlend(-1, Mathf.Max(0.0f, blendSeconds));
        }

        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            float clampedDeltaTime = Mathf.Max(0.0f, deltaTime);
            UpdateClipBlend(clampedDeltaTime);
            UpdateLaneBlend(clampedDeltaTime);
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();
            _controllerPlayable.SetFloat(_bindings.SpeedMultiplier.Hash, Mathf.Max(0.0f, speed));
        }

        public void RefreshBindings()
        {
            ThrowIfDisposed();
            _clipDurations.Clear();
            CacheClipDurations();
            ValidateContract();
        }

        public void ApplyControllerOverrides(IReadOnlyList<AnimationClip> clips)
        {
            ThrowIfDisposed();
            _loadoutController.ApplyOverrides(_baselineOverrides);
            if (clips != null)
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null)
                    {
                        continue;
                    }

                    if (!_overrideNames.Contains(clip.name))
                    {
                        Debug.LogWarning(
                            $"Animator controller has no loadout clip slot named '{clip.name}'.",
                            _animator);
                        continue;
                    }

                    _loadoutController[clip.name] = clip;
                }
            }

            RefreshBindings();
        }

        internal void SetFloat(int parameterHash, float value)
        {
            ThrowIfDisposed();
            _controllerPlayable.SetFloat(parameterHash, value);
        }

        internal void RequireFloat(MecanimParameterBinding binding)
        {
            RequireParameter(binding, AnimatorControllerParameterType.Float);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopAction(0.0f);
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                ClearLane(lane);
            }

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_animator != null && _animator.runtimeAnimatorController == _loadoutController)
            {
                _animator.runtimeAnimatorController = _sourceController;
            }

            if (_loadoutController != null)
            {
                Object.Destroy(_loadoutController);
            }

            _disposed = true;
        }

        private void CreateGraph()
        {
            _graph = PlayableGraph.Create($"{_animator.name}.CharacterAnimation");
            _controllerPlayable = AnimatorControllerPlayable.Create(_graph, _loadoutController);
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 3);
            _actionMixers[0] = AnimationMixerPlayable.Create(_graph, ClipSlotCount);
            _actionMixers[1] = AnimationMixerPlayable.Create(_graph, ClipSlotCount);

            _graph.Connect(_controllerPlayable, 0, _layerMixer, ControllerInput);
            _graph.Connect(_actionMixers[0], 0, _layerMixer, FullBodyActionInput);
            _graph.Connect(_actionMixers[1], 0, _layerMixer, MaskedActionInput);
            _layerMixer.SetInputWeight(ControllerInput, 1.0f);
            _layerMixer.SetInputWeight(FullBodyActionInput, 0.0f);
            _layerMixer.SetInputWeight(MaskedActionInput, 0.0f);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(_graph, "CharacterAnimation", _animator);
            output.SetSourcePlayable(_layerMixer);
            _graph.Play();
        }

        private void CreateActionClip(
            int lane,
            int slot,
            in AnimationClipActionRequest request)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, request.Clip);
            playable.SetApplyFootIK(request.ApplyFootIk);
            playable.SetTime(0.0d);
            playable.SetSpeed(request.PlaybackSpeed);
            _graph.Connect(playable, 0, _actionMixers[lane], slot);
            _actionClips[lane, slot] = playable;
            _actionClipValid[lane, slot] = true;
        }

        private void StartClipBlend(
            int lane,
            int fromSlot,
            int toSlot,
            float duration)
        {
            _clipBlendLane = lane;
            _clipBlendFromSlot = fromSlot;
            _clipBlendToSlot = toSlot;
            _clipBlendDuration = Mathf.Max(0.0f, duration);
            _clipBlendElapsed = 0.0f;
            if (_clipBlendDuration <= 0.0f)
            {
                FinishClipBlend();
            }
        }

        private void UpdateClipBlend(float deltaTime)
        {
            if (_clipBlendLane < 0)
            {
                return;
            }

            _clipBlendElapsed += deltaTime;
            float progress = _clipBlendDuration > 0.0f
                ? Mathf.Clamp01(_clipBlendElapsed / _clipBlendDuration)
                : 1.0f;
            AnimationMixerPlayable mixer = _actionMixers[_clipBlendLane];
            mixer.SetInputWeight(_clipBlendFromSlot, 1.0f - progress);
            mixer.SetInputWeight(_clipBlendToSlot, progress);
            if (progress >= 1.0f)
            {
                FinishClipBlend();
            }
        }

        private void FinishClipBlend()
        {
            if (_clipBlendLane < 0)
            {
                return;
            }

            int lane = _clipBlendLane;
            int fromSlot = _clipBlendFromSlot;
            int toSlot = _clipBlendToSlot;
            _actionMixers[lane].SetInputWeight(toSlot, 1.0f);
            DestroyClip(lane, fromSlot);
            _activeClipSlots[lane] = toSlot;
            _clipBlendLane = -1;
            _clipBlendFromSlot = -1;
            _clipBlendToSlot = -1;
            _clipBlendDuration = 0.0f;
            _clipBlendElapsed = 0.0f;
        }

        private void StartLaneBlend(int targetLane, float duration)
        {
            _targetLane = targetLane;
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneStartWeights[lane] = _laneWeights[lane];
                _laneTargetWeights[lane] = lane == targetLane ? 1.0f : 0.0f;
            }

            _laneBlendDuration = Mathf.Max(0.0f, duration);
            _laneBlendElapsed = 0.0f;
            if (_laneBlendDuration <= 0.0f)
            {
                ApplyLaneBlend(1.0f);
                FinishLaneBlend();
            }
        }

        private void UpdateLaneBlend(float deltaTime)
        {
            if (_laneBlendDuration <= 0.0f)
            {
                return;
            }

            _laneBlendElapsed += deltaTime;
            float progress = Mathf.Clamp01(_laneBlendElapsed / _laneBlendDuration);
            ApplyLaneBlend(progress);
            if (progress >= 1.0f)
            {
                FinishLaneBlend();
            }
        }

        private void ApplyLaneBlend(float progress)
        {
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneWeights[lane] = Mathf.Lerp(
                    _laneStartWeights[lane],
                    _laneTargetWeights[lane],
                    progress);
                _layerMixer.SetInputWeight(lane + FullBodyActionInput, _laneWeights[lane]);
            }
        }

        private void FinishLaneBlend()
        {
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneWeights[lane] = _laneTargetWeights[lane];
                _layerMixer.SetInputWeight(lane + FullBodyActionInput, _laneWeights[lane]);
                if (lane != _targetLane)
                {
                    ClearLane(lane);
                }
            }

            _activeLane = _targetLane;
            _laneBlendDuration = 0.0f;
            _laneBlendElapsed = 0.0f;
        }

        private void ClearLane(int lane)
        {
            if (_clipBlendLane == lane)
            {
                _clipBlendLane = -1;
                _clipBlendFromSlot = -1;
                _clipBlendToSlot = -1;
            }

            for (int slot = 0; slot < ClipSlotCount; slot++)
            {
                DestroyClip(lane, slot);
            }

            _activeClipSlots[lane] = -1;
        }

        private void DestroyClip(int lane, int slot)
        {
            if (!_actionClipValid[lane, slot])
            {
                return;
            }

            _graph.Disconnect(_actionMixers[lane], slot);
            AnimationClipPlayable playable = _actionClips[lane, slot];
            if (playable.IsValid())
            {
                _graph.DestroyPlayable(playable);
            }

            _actionClipValid[lane, slot] = false;
            _actionMixers[lane].SetInputWeight(slot, 0.0f);
        }

        private void CacheClipDurations()
        {
            AnimationClip[] clips = _loadoutController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null)
                {
                    _clipDurations[Animator.StringToHash(clip.name)] = clip.length;
                }
            }
        }

        private float GetClipDuration(int stateHash)
        {
            return _clipDurations.TryGetValue(stateHash, out float duration)
                ? duration
                : 0.0f;
        }

        private void ValidateContract()
        {
            RequireParameter(_bindings.MovementType, AnimatorControllerParameterType.Float);
            RequireParameter(_bindings.MovementDirection, AnimatorControllerParameterType.Float);
            RequireParameter(_bindings.VerticalSpeed, AnimatorControllerParameterType.Float);
            RequireParameter(_bindings.SpeedMultiplier, AnimatorControllerParameterType.Float);
        }

        private void RequireParameter(
            MecanimParameterBinding binding,
            AnimatorControllerParameterType expectedType)
        {
            int parameterCount = _controllerPlayable.GetParameterCount();
            for (int i = 0; i < parameterCount; i++)
            {
                AnimatorControllerParameter parameter = _controllerPlayable.GetParameter(i);
                if (parameter.nameHash == binding.Hash && parameter.type == expectedType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator '{_animator.name}' requires {expectedType} parameter '{binding.Name}'.");
        }

        private static float Damp(
            float current,
            float target,
            ref float velocity,
            float smoothTime,
            float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return current;
            }

            return Mathf.SmoothDamp(
                current,
                target,
                ref velocity,
                smoothTime,
                float.PositiveInfinity,
                deltaTime);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MecanimCharacterAnimationDriver));
            }
        }
    }
}
