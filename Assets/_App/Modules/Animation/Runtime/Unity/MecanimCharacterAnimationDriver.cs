using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public static class MecanimCharacterParameters
    {
        public const string PlanarSpeed = "PlanarSpeed";
        public const string NormalizedSpeed = "NormalizedSpeed";
        public const string MoveX = "MoveX";
        public const string MoveY = "MoveY";
        public const string VerticalSpeed = "VerticalSpeed";
        public const string Grounded = "Grounded";
        public const string LocomotionPlaybackRate = "LocomotionPlaybackRate";

        // Historical XController contract. The driver writes both this contract
        // and the semantic contract above when matching parameters are present.
        public const string MovementType = "MovementType";
        public const string MovementDirection = "MovementDirection";
        public const string FallingDuration = "FallingDuration";
        public const string OnGround = "OnGround";
        public const string SpeedMultiplier = "SpeedMultiplier";
        public const string JumpStarting = "JumpStarting";
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
        // Presentation may lag behind the motor slightly. A shorter value made an
        // abrupt 5 -> 0 speed change jump across most of the walk/run blend in one
        // rendered frame, producing a visible pose pop even though the state
        // transition itself was blended.
        private const float SpeedDampSeconds = 0.18f;
        private const float LegacyGaitDampSeconds = 0.33f;
        private const float DirectionDampSeconds = 0.08f;
        private const float VerticalDampSeconds = 0.04f;
        private const float DirectionEpsilon = 0.0001f;

        private static readonly int PlanarSpeedHash = Animator.StringToHash(
            MecanimCharacterParameters.PlanarSpeed);
        private static readonly int NormalizedSpeedHash = Animator.StringToHash(
            MecanimCharacterParameters.NormalizedSpeed);
        private static readonly int MoveXHash = Animator.StringToHash(
            MecanimCharacterParameters.MoveX);
        private static readonly int MoveYHash = Animator.StringToHash(
            MecanimCharacterParameters.MoveY);
        private static readonly int VerticalSpeedHash = Animator.StringToHash(
            MecanimCharacterParameters.VerticalSpeed);
        private static readonly int GroundedHash = Animator.StringToHash(
            MecanimCharacterParameters.Grounded);
        private static readonly int LocomotionPlaybackRateHash = Animator.StringToHash(
            MecanimCharacterParameters.LocomotionPlaybackRate);

        private static readonly int MovementTypeHash = Animator.StringToHash(
            MecanimCharacterParameters.MovementType);
        private static readonly int MovementDirectionHash = Animator.StringToHash(
            MecanimCharacterParameters.MovementDirection);
        private static readonly int FallingDurationHash = Animator.StringToHash(
            MecanimCharacterParameters.FallingDuration);
        private static readonly int OnGroundHash = Animator.StringToHash(
            MecanimCharacterParameters.OnGround);
        private static readonly int SpeedMultiplierHash = Animator.StringToHash(
            MecanimCharacterParameters.SpeedMultiplier);

        private static readonly int JumpStartingHash = Animator.StringToHash(
            MecanimCharacterParameters.JumpStarting);
        private static readonly int ActionHash = Animator.StringToHash(
            MecanimCharacterParameters.Action);
        private static readonly int ActionIdHash = Animator.StringToHash(
            MecanimCharacterParameters.ActionId);
        private static readonly int ActionVariantHash = Animator.StringToHash(
            MecanimCharacterParameters.ActionVariant);
        private static readonly int CancelActionHash = Animator.StringToHash(
            MecanimCharacterParameters.CancelAction);

        private readonly Animator _animator;
        private readonly IReadOnlyDictionary<int, AnimatorControllerParameterType> _parameters;
        private int _nextSequence;
        private bool _disposed;

        public Animator Animator => _animator;

        public MecanimCharacterAnimationDriver(Animator animator)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));

            _parameters = ReadParameters();
            _animator.applyRootMotion = false;
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            ThrowIfDisposed();

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            float planarMagnitude = Mathf.Sqrt(
                frame.ForwardSpeed * frame.ForwardSpeed +
                frame.LateralSpeed * frame.LateralSpeed);
            float inverseMagnitude = planarMagnitude > DirectionEpsilon
                ? 1.0f / planarMagnitude
                : 0.0f;

            SetFloat(
                PlanarSpeedHash,
                planarMagnitude,
                SpeedDampSeconds,
                safeDeltaTime);
            SetFloat(
                NormalizedSpeedHash,
                frame.NormalizedSpeed,
                SpeedDampSeconds,
                safeDeltaTime);
            SetFloat(
                MoveXHash,
                frame.LateralSpeed * inverseMagnitude,
                DirectionDampSeconds,
                safeDeltaTime);
            SetFloat(
                MoveYHash,
                frame.ForwardSpeed * inverseMagnitude,
                DirectionDampSeconds,
                safeDeltaTime);
            SetFloat(
                VerticalSpeedHash,
                frame.VerticalSpeed,
                VerticalDampSeconds,
                safeDeltaTime);
            SetFloat(LocomotionPlaybackRateHash, 1.0f);
            SetBool(GroundedHash, frame.IsGrounded);
            SetBool(JumpStartingHash, frame.IsJumpStarting);

            float movementDirection = Mathf.Abs(frame.ForwardSpeed) > DirectionEpsilon
                ? Mathf.Clamp(frame.NormalizedSpeed * Mathf.Sign(frame.ForwardSpeed), -1.0f, 1.0f)
                : 0.0f;
            float movementType = planarMagnitude > DirectionEpsilon
                ? (float)frame.Gait
                : 0.0f;
            SetFloat(MovementTypeHash, movementType, LegacyGaitDampSeconds, safeDeltaTime);
            SetFloat(MovementDirectionHash, movementDirection, DirectionDampSeconds, safeDeltaTime);
            SetFloat(
                FallingDurationHash,
                frame.IsGrounded ? 0.0f : frame.VerticalSpeed,
                VerticalDampSeconds,
                safeDeltaTime);
            SetBool(OnGroundHash, frame.IsGrounded);
        }

        public AnimationActionPlayback PlayAction(in AnimationActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid)
            {
                return AnimationActionPlayback.Invalid;
            }

            ResetTrigger(CancelActionHash);
            SetInteger(ActionIdHash, request.Action.Value);
            SetInteger(ActionVariantHash, request.Variant);
            ResetTrigger(ActionHash);
            SetTrigger(ActionHash);

            _nextSequence++;
            if (_nextSequence == 0)
            {
                _nextSequence++;
            }

            return new AnimationActionPlayback(_nextSequence, request.ExpectedDuration);
        }

        public void CompleteAction()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            ResetTrigger(ActionHash);
            ResetTrigger(CancelActionHash);
            SetInteger(ActionIdHash, 0);
            SetInteger(ActionVariantHash, 0);
        }

        public void CancelAction()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            ResetTrigger(ActionHash);
            SetTrigger(CancelActionHash);
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();
            float safeSpeed = Mathf.Max(0.0f, speed);
            _animator.speed = safeSpeed;
            SetFloat(SpeedMultiplierHash, safeSpeed);
        }

        public void Reset()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            ResetTrigger(ActionHash);
            ResetTrigger(CancelActionHash);
            SetFloat(PlanarSpeedHash, 0.0f);
            SetFloat(NormalizedSpeedHash, 0.0f);
            SetFloat(MoveXHash, 0.0f);
            SetFloat(MoveYHash, 0.0f);
            SetFloat(VerticalSpeedHash, 0.0f);
            SetFloat(LocomotionPlaybackRateHash, 1.0f);
            SetBool(GroundedHash, true);
            SetBool(JumpStartingHash, false);
            SetInteger(ActionIdHash, 0);
            SetInteger(ActionVariantHash, 0);
            SetFloat(MovementTypeHash, 0.0f);
            SetFloat(MovementDirectionHash, 0.0f);
            SetFloat(FallingDurationHash, 0.0f);
            SetBool(OnGroundHash, true);
            SetFloat(SpeedMultiplierHash, 1.0f);
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

        private IReadOnlyDictionary<int, AnimatorControllerParameterType> ReadParameters()
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
            return parameters;
        }

        private bool HasParameter(int parameterHash, AnimatorControllerParameterType expectedType)
        {
            return _parameters.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType) &&
                   actualType == expectedType;
        }

        private void SetFloat(int parameterHash, float value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Float))
            {
                _animator.SetFloat(parameterHash, value);
            }
        }

        private void SetFloat(int parameterHash, float value, float dampTime, float deltaTime)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Float))
            {
                _animator.SetFloat(parameterHash, value, dampTime, deltaTime);
            }
        }

        private void SetBool(int parameterHash, bool value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Bool))
            {
                _animator.SetBool(parameterHash, value);
            }
        }

        private void SetInteger(int parameterHash, int value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Int))
            {
                _animator.SetInteger(parameterHash, value);
            }
        }

        private void SetTrigger(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterHash);
            }
        }

        private void ResetTrigger(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(parameterHash);
            }
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
