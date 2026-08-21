using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    /// <summary>
    /// Adapts Project S semantic animation commands to the historical XController contract.
    /// The controller owns its blend trees and transitions; gameplay remains the state owner.
    /// </summary>
    internal sealed class XControllerCharacterAnimationDriver : ICharacterAnimationDriver, IDisposable
    {
        private const int BaseLayerIndex = 0;
        private const float GaitDampSeconds = 0.33f;
        private const float DirectionDampSeconds = 0.10f;
        private const float VerticalDampSeconds = 0.10f;
        private const float ActionTransitionSeconds = 0.10f;
        private const float DirectionEpsilon = 0.0001f;

        private const int DodgeActionId = 1;
        private const int LightAttackActionId = 2;

        private static readonly int OnGroundHash = Animator.StringToHash("OnGround");
        private static readonly int MovementDirectionHash = Animator.StringToHash("MovementDirection");
        private static readonly int MovementTypeHash = Animator.StringToHash("MovementType");
        private static readonly int FallingDurationHash = Animator.StringToHash("FallingDuration");
        private static readonly int SpeedMultiplierHash = Animator.StringToHash("SpeedMultiplier");

        private static readonly int MoveStateHash = Animator.StringToHash(
            "Base Layer.Locomotion.Move");
        private static readonly int RollForwardStateHash = Animator.StringToHash(
            "Base Layer.Locomotion.Roll.RollFront");
        private static readonly int RollBackwardStateHash = Animator.StringToHash(
            "Base Layer.Locomotion.Roll.RollBack");
        private static readonly int[] LightAttackStateHashes =
        {
            Animator.StringToHash("Base Layer.Attack.BaseAttack1"),
            Animator.StringToHash("Base Layer.Attack.BaseAttack2"),
            Animator.StringToHash("Base Layer.Attack.BaseAttack3"),
            Animator.StringToHash("Base Layer.Attack.BaseAttack4")
        };

        private readonly Animator _animator;
        private int _nextSequence;
        private bool _disposed;

        public XControllerCharacterAnimationDriver(Animator animator)
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
            float planarSpeed = Mathf.Sqrt(
                frame.ForwardSpeed * frame.ForwardSpeed +
                frame.LateralSpeed * frame.LateralSpeed);
            float movementType = planarSpeed > DirectionEpsilon
                ? (float)frame.Gait
                : 0.0f;
            float movementDirection = ResolveMovementDirection(frame, planarSpeed);

            _animator.SetFloat(
                MovementTypeHash,
                movementType,
                GaitDampSeconds,
                safeDeltaTime);
            _animator.SetFloat(
                MovementDirectionHash,
                movementDirection,
                DirectionDampSeconds,
                safeDeltaTime);
            _animator.SetFloat(
                FallingDurationHash,
                frame.IsGrounded ? 0.0f : frame.VerticalSpeed,
                VerticalDampSeconds,
                safeDeltaTime);
            _animator.SetBool(OnGroundHash, frame.IsGrounded);
        }

        public AnimationActionPlayback PlayAction(in AnimationActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid || !TryResolveActionState(request, out int stateHash))
            {
                return AnimationActionPlayback.Invalid;
            }

            _animator.CrossFadeInFixedTime(
                stateHash,
                ActionTransitionSeconds,
                BaseLayerIndex);

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

            _animator.CrossFadeInFixedTime(
                MoveStateHash,
                ActionTransitionSeconds,
                BaseLayerIndex);
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();

            float safeSpeed = Mathf.Max(0.0f, speed);
            _animator.speed = safeSpeed;
            _animator.SetFloat(SpeedMultiplierHash, safeSpeed);
        }

        public void Reset()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            _animator.SetFloat(MovementTypeHash, 0.0f);
            _animator.SetFloat(MovementDirectionHash, 0.0f);
            _animator.SetFloat(FallingDurationHash, 0.0f);
            _animator.SetFloat(SpeedMultiplierHash, 1.0f);
            _animator.SetBool(OnGroundHash, true);
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

        private static float ResolveMovementDirection(
            in CharacterLocomotionFrame frame,
            float planarSpeed)
        {
            if (planarSpeed <= DirectionEpsilon)
            {
                return 0.0f;
            }

            float directionSign = frame.ForwardSpeed < -DirectionEpsilon ? -1.0f : 1.0f;
            return Mathf.Clamp01(frame.NormalizedSpeed) * directionSign;
        }

        private bool TryResolveActionState(
            in AnimationActionRequest request,
            out int stateHash)
        {
            switch (request.Action.Value)
            {
                case DodgeActionId:
                    stateHash = request.Variant == 1
                        ? RollBackwardStateHash
                        : RollForwardStateHash;
                    break;

                case LightAttackActionId when request.Variant >= 0 &&
                                              request.Variant < LightAttackStateHashes.Length:
                    stateHash = LightAttackStateHashes[request.Variant];
                    break;

                default:
                    stateHash = 0;
                    return false;
            }

            return _animator.HasState(BaseLayerIndex, stateHash);
        }

        private void ValidateAnimator()
        {
            if (_animator.avatar == null ||
                !_animator.avatar.isValid ||
                !_animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a valid Humanoid Avatar for XController.");
            }

            if (_animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires XController.");
            }

            RequireParameter(OnGroundHash, AnimatorControllerParameterType.Bool, "OnGround");
            RequireParameter(
                MovementDirectionHash,
                AnimatorControllerParameterType.Float,
                "MovementDirection");
            RequireParameter(
                MovementTypeHash,
                AnimatorControllerParameterType.Float,
                "MovementType");
            RequireParameter(
                FallingDurationHash,
                AnimatorControllerParameterType.Float,
                "FallingDuration");
            RequireParameter(
                SpeedMultiplierHash,
                AnimatorControllerParameterType.Float,
                "SpeedMultiplier");

            RequireState(MoveStateHash, "Base Layer.Locomotion.Move");
            RequireState(RollForwardStateHash, "Base Layer.Locomotion.Roll.RollFront");
            RequireState(RollBackwardStateHash, "Base Layer.Locomotion.Roll.RollBack");
            RequireState(LightAttackStateHashes[0], "Base Layer.Attack.BaseAttack1");
        }

        private void RequireParameter(
            int parameterHash,
            AnimatorControllerParameterType expectedType,
            string parameterName)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == parameterHash && parameter.type == expectedType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator Controller on '{_animator.name}' is missing required " +
                $"{expectedType} parameter '{parameterName}'.");
        }

        private void RequireState(int stateHash, string stateName)
        {
            if (!_animator.HasState(BaseLayerIndex, stateHash))
            {
                throw new InvalidOperationException(
                    $"Animator Controller on '{_animator.name}' is missing required state '{stateName}'.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(XControllerCharacterAnimationDriver));
            }

            if (_animator == null)
            {
                throw new InvalidOperationException("The Animator owner has been destroyed.");
            }
        }
    }
}
