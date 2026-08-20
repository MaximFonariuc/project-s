using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public static class MecanimCharacterParameters
    {
        public const string MovementType = "MovementType";
        public const string MovementDirection = "MovementDirection";
        public const string FallingDuration = "FallingDuration";
        public const string OnGround = "OnGround";
        public const string Action = "Action";
        public const string ActionId = "ActionId";
        public const string ActionVariant = "ActionVariant";
        public const string CancelAction = "CancelAction";
    }

    /// <summary>
    /// The single runtime writer for the conventional Animator Controller contract.
    /// Gameplay sends semantic frames and commands; the controller asset owns states,
    /// blend trees and transition rules.
    /// </summary>
    public sealed class MecanimCharacterAnimationDriver : ICharacterAnimationDriver, IDisposable
    {
        private const float LocomotionDampSeconds = 0.33f;
        private const float DirectionDampSeconds = 0.1f;
        private const float VerticalDampSeconds = 0.1f;

        private static readonly int MovementTypeHash = Animator.StringToHash(
            MecanimCharacterParameters.MovementType);
        private static readonly int MovementDirectionHash = Animator.StringToHash(
            MecanimCharacterParameters.MovementDirection);
        private static readonly int FallingDurationHash = Animator.StringToHash(
            MecanimCharacterParameters.FallingDuration);
        private static readonly int OnGroundHash = Animator.StringToHash(
            MecanimCharacterParameters.OnGround);
        private static readonly int ActionHash = Animator.StringToHash(
            MecanimCharacterParameters.Action);
        private static readonly int ActionIdHash = Animator.StringToHash(
            MecanimCharacterParameters.ActionId);
        private static readonly int ActionVariantHash = Animator.StringToHash(
            MecanimCharacterParameters.ActionVariant);
        private static readonly int CancelActionHash = Animator.StringToHash(
            MecanimCharacterParameters.CancelAction);

        private readonly Animator _animator;
        private int _nextSequence;
        private bool _disposed;

        public Animator Animator => _animator;

        public MecanimCharacterAnimationDriver(Animator animator)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));

            ValidateAnimator();
            _animator.applyRootMotion = false;
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            ThrowIfDisposed();

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            float planarMagnitude = Mathf.Sqrt(
                frame.ForwardSpeed * frame.ForwardSpeed +
                frame.LateralSpeed * frame.LateralSpeed);
            float inverseMagnitude = planarMagnitude > 0.0001f
                ? 1.0f / planarMagnitude
                : 0.0f;
            float movementType = planarMagnitude > 0.01f
                ? (float)frame.Gait
                : 0.0f;

            _animator.SetFloat(
                MovementTypeHash,
                movementType,
                LocomotionDampSeconds,
                safeDeltaTime);
            _animator.SetFloat(
                MovementDirectionHash,
                frame.ForwardSpeed * inverseMagnitude,
                DirectionDampSeconds,
                safeDeltaTime);
            _animator.SetFloat(
                FallingDurationHash,
                frame.VerticalSpeed,
                VerticalDampSeconds,
                safeDeltaTime);
            _animator.SetBool(OnGroundHash, frame.IsGrounded);
        }

        public AnimationActionPlayback PlayAction(in AnimationActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid)
            {
                return AnimationActionPlayback.Invalid;
            }

            _animator.ResetTrigger(CancelActionHash);
            _animator.SetInteger(ActionIdHash, request.Action.Value);
            _animator.SetInteger(ActionVariantHash, request.Variant);
            _animator.ResetTrigger(ActionHash);
            _animator.SetTrigger(ActionHash);

            _nextSequence++;
            if (_nextSequence == 0)
            {
                _nextSequence++;
            }

            return new AnimationActionPlayback(_nextSequence, request.ExpectedDuration);
        }

        public void CancelAction()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            _animator.ResetTrigger(ActionHash);
            _animator.SetTrigger(CancelActionHash);
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();
            _animator.speed = Mathf.Max(0.0f, speed);
        }

        public void Reset()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            _animator.ResetTrigger(ActionHash);
            _animator.ResetTrigger(CancelActionHash);
            _animator.SetFloat(MovementTypeHash, 0.0f);
            _animator.SetFloat(MovementDirectionHash, 0.0f);
            _animator.SetFloat(FallingDurationHash, 0.0f);
            _animator.SetBool(OnGroundHash, true);
            _animator.SetInteger(ActionIdHash, 0);
            _animator.SetInteger(ActionVariantHash, 0);
            _animator.speed = 1.0f;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Reset();
            _disposed = true;
        }

        private void ValidateAnimator()
        {
            if (_animator.avatar == null || !_animator.avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a valid Generic or Humanoid Avatar.");
            }

            if (_animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a RuntimeAnimatorController.");
            }

            var parameters = new Dictionary<int, AnimatorControllerParameterType>();
            AnimatorControllerParameter[] configured = _animator.parameters;
            for (int i = 0; i < configured.Length; i++)
            {
                parameters[configured[i].nameHash] = configured[i].type;
            }

            RequireParameter(parameters, MovementTypeHash, AnimatorControllerParameterType.Float);
            RequireParameter(parameters, MovementDirectionHash, AnimatorControllerParameterType.Float);
            RequireParameter(parameters, FallingDurationHash, AnimatorControllerParameterType.Float);
            RequireParameter(parameters, OnGroundHash, AnimatorControllerParameterType.Bool);
            RequireParameter(parameters, ActionHash, AnimatorControllerParameterType.Trigger);
            RequireParameter(parameters, ActionIdHash, AnimatorControllerParameterType.Int);
            RequireParameter(parameters, ActionVariantHash, AnimatorControllerParameterType.Int);
            RequireParameter(parameters, CancelActionHash, AnimatorControllerParameterType.Trigger);
        }

        private void RequireParameter(
            IReadOnlyDictionary<int, AnimatorControllerParameterType> parameters,
            int parameterHash,
            AnimatorControllerParameterType expectedType)
        {
            if (parameters.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType) &&
                actualType == expectedType)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Animator Controller on '{_animator.name}' is missing a required " +
                $"{expectedType} parameter (hash {parameterHash}). Rebuild the controller through its editor setup tool.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MecanimCharacterAnimationDriver));
            }

            if (_animator == null)
            {
                throw new InvalidOperationException("The Animator owner has been destroyed.");
            }
        }
    }
}
