using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public sealed class MecanimCharacterAnimationDriver : ICharacterAnimationDriver
    {
        private readonly Animator _animator;
        private readonly MecanimCharacterAnimationBindings _bindings;
        private readonly Dictionary<int, float> _clipDurations = new Dictionary<int, float>();

        public Animator Animator => _animator;

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

            RefreshBindings();
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            float signedMovement = frame.NormalizedSpeed;
            if (Mathf.Abs(frame.ForwardSpeed) > 0.0001f)
            {
                signedMovement *= Mathf.Sign(frame.ForwardSpeed);
            }

            _animator.SetFloat(
                _bindings.MovementType.Hash,
                frame.NormalizedSpeed > 0.0001f ? (float)frame.Gait : 0.0f,
                0.33f,
                deltaTime);
            _animator.SetFloat(_bindings.MovementDirection.Hash, signedMovement, 0.1f, deltaTime);
            _animator.SetFloat(
                _bindings.VerticalSpeed.Hash,
                frame.Mode == CharacterLocomotionMode.Airborne ? frame.VerticalSpeed : 0.0f,
                0.1f,
                deltaTime);
        }

        public AnimationPlayback CrossFade(
            AnimationStateId state,
            AnimationLayerId layer,
            float transitionSeconds,
            float expectedDuration = 0.0f)
        {
            if (!state.IsValid || layer.Index >= _animator.layerCount)
            {
                return AnimationPlayback.Invalid;
            }

            if (!_animator.HasState(layer.Index, state.Hash))
            {
                Debug.LogError(
                    $"Animator '{_animator.name}' has no state hash '{state.Hash}' on layer {layer.Index}.",
                    _animator);
                return AnimationPlayback.Invalid;
            }

            _animator.CrossFadeInFixedTime(
                state.Hash,
                Mathf.Max(0.0f, transitionSeconds),
                layer.Index,
                0.0f);

            float duration = expectedDuration > 0.0f
                ? expectedDuration
                : GetClipDuration(state.Hash);
            return new AnimationPlayback(state, layer, duration);
        }

        public void SetPlaybackSpeed(float speed)
        {
            _animator.SetFloat(_bindings.SpeedMultiplier.Hash, Mathf.Max(0.0f, speed));
        }

        public void RefreshBindings()
        {
            _clipDurations.Clear();
            CacheClipDurations();
            ValidateContract();
        }

        private void CacheClipDurations()
        {
            RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a RuntimeAnimatorController.");
            }

            AnimationClip[] clips = controller.animationClips;
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
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == binding.Hash && parameter.type == expectedType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator '{_animator.name}' requires {expectedType} parameter '{binding.Name}'.");
        }
    }
}
