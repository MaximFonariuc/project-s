using System;
using System.Collections.Generic;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    /// <summary>
    /// Adapts Project S semantic animation commands to the historical XController contract.
    /// The controller owns its clips, blend trees and exit transitions; this adapter owns
    /// the semantic entry crossfades. Gameplay remains the authoritative state owner.
    /// </summary>
    internal sealed class XControllerCharacterAnimationDriver :
        ICharacterAnimationDriver,
        ICharacterLookAnimationDriver,
        IDisposable
    {
        private const float GaitDampSeconds = 0.33f;
        private const float DirectionDampSeconds = 0.12f;
        private const float DirectionEpsilon = 0.0001f;
        private const float DodgeCrossFadeSeconds = 0.10f;
        private const float AttackCrossFadeSeconds = 0.12f;
        private const float WeaponCrossFadeSeconds = 0.15f;
        private const float ActionCancelCrossFadeSeconds = 0.12f;
        private const float AttackBodyLookWeight = 0.15f;
        private const float AttackHeadLookWeight = 0.35f;
        private const float WeaponBodyLookWeight = 0.35f;
        private const float WeaponHeadLookWeight = 0.65f;

        private const int DodgeActionId = 1;
        private const int LightAttackActionId = 2;
        private const int WeaponTransitionActionId = 3;
        private const int ReactionActionId = 4;
        private const int AirAttackActionId = 5;
        private const int SpecialAttackActionId = 6;
        private const int HeavyAttackActionId = 7;
        private const int LauncherActionId = 8;
        private const int AirChaseActionId = 9;

        private static readonly int OnGroundHash = Animator.StringToHash(
            XControllerAnimationContract.OnGroundParameterName);
        private static readonly int MovementDirectionHash = Animator.StringToHash(
            XControllerAnimationContract.MovementDirectionParameterName);
        private static readonly int MovementTypeHash = Animator.StringToHash(
            XControllerAnimationContract.MovementTypeParameterName);
        private static readonly int FallingDurationHash = Animator.StringToHash(
            XControllerAnimationContract.FallingDurationParameterName);
        private static readonly int SpeedMultiplierHash = Animator.StringToHash(
            XControllerAnimationContract.SpeedMultiplierParameterName);
        private static readonly int MoveStateHash = Animator.StringToHash(
            XControllerAnimationContract.MoveStatePath);
        private static readonly int RollForwardStateHash = Animator.StringToHash(
            XControllerAnimationContract.RollForwardStatePath);
        private static readonly int RollBackwardStateHash = Animator.StringToHash(
            XControllerAnimationContract.RollBackwardStatePath);
        private static readonly int[] LightAttackStateHashes = CreateStateHashes(
            XControllerAnimationContract.LightAttackStatePaths);
        private static readonly int[] ReactionStateHashes = CreateStateHashes(
            XControllerAnimationContract.ReactionStatePaths);
        private static readonly int[] AirAttackStateHashes = CreateStateHashes(
            XControllerAnimationContract.AirAttackStatePaths);
        private static readonly int SpecialAttackStateHash = Animator.StringToHash(
            XControllerAnimationContract.SpecialAttackStatePath);
        private static readonly int HeavyAttackStateHash = Animator.StringToHash(
            XControllerAnimationContract.HeavyAttackStatePath);
        private static readonly int LauncherStateHash = Animator.StringToHash(
            XControllerAnimationContract.LauncherStatePath);
        private static readonly int AirChaseStateHash = Animator.StringToHash(
            XControllerAnimationContract.AirChaseStatePath);
        private static readonly int WeaponSheatheStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponSheatheStatePath);
        private static readonly int WeaponUnsheatheStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponUnsheatheStatePath);
        private static readonly int WeaponWaitingStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponWaitingStatePath);

        private readonly Animator _animator;
        private readonly XControllerLookPoseController _lookPose;
        private readonly int _switchWeaponLayerIndex;

        private int _nextSequence;
        private float _lastAirborneVerticalSpeed;
        private int _actionLayerIndex = -1;
        private bool _wasGrounded = true;
        private bool _disposed;

        public XControllerCharacterAnimationDriver(Animator animator)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _lookPose = new XControllerLookPoseController(_animator);
            _switchWeaponLayerIndex = _animator.GetLayerIndex(
                XControllerAnimationContract.SwitchWeaponLayerName);

            ValidateAnimator();
            _animator.applyRootMotion = false;
        }

        public void ApplyLook(in CharacterLookFrame frame, float deltaTime)
        {
            ThrowIfDisposed();
            _lookPose.Apply(in frame, deltaTime);
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
            float fallingVelocity = ResolveFallingVelocity(frame);

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
            // This value drives transition direction and landing thresholds;
            // damping would delay the jump/fall sign change and soften impacts.
            _animator.SetFloat(FallingDurationHash, fallingVelocity);
            _animator.SetBool(OnGroundHash, frame.IsGrounded);
            _wasGrounded = frame.IsGrounded;
            _lookPose.SetGrounded(frame.IsGrounded);
        }

        public AnimationActionPlayback PlayAction(in AnimationActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid ||
                !TryResolveActionState(
                    request,
                    out int layerIndex,
                    out int stateHash,
                    out float crossFadeSeconds))
            {
                return AnimationActionPlayback.Invalid;
            }

            _animator.CrossFadeInFixedTime(
                stateHash,
                crossFadeSeconds,
                layerIndex,
                0.0f);
            SetActionPresentationPolicy(request.Action.Value, layerIndex);

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

            ClearActionPresentationPolicy();
        }

        public void CancelAction()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            if (_actionLayerIndex == _switchWeaponLayerIndex)
            {
                _animator.CrossFadeInFixedTime(
                    WeaponWaitingStateHash,
                    ActionCancelCrossFadeSeconds,
                    _switchWeaponLayerIndex,
                    0.0f);
            }
            else if (_animator.HasState(
                         XControllerAnimationContract.BaseLayerIndex,
                         MoveStateHash))
            {
                _animator.CrossFadeInFixedTime(
                    MoveStateHash,
                    ActionCancelCrossFadeSeconds,
                    XControllerAnimationContract.BaseLayerIndex,
                    0.0f);
            }

            ClearActionPresentationPolicy();
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
            _lastAirborneVerticalSpeed = 0.0f;
            _wasGrounded = true;
            _lookPose.Reset();
            ClearActionPresentationPolicy();
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

            return frame.ForwardSpeed < -DirectionEpsilon ? -1.0f : 1.0f;
        }

        private void SetActionPresentationPolicy(int actionId, int layerIndex)
        {
            float bodyWeight = actionId switch
            {
                DodgeActionId => 0.0f,
                LightAttackActionId => AttackBodyLookWeight,
                AirAttackActionId => AttackBodyLookWeight,
                SpecialAttackActionId => AttackBodyLookWeight,
                HeavyAttackActionId => AttackBodyLookWeight,
                LauncherActionId => AttackBodyLookWeight,
                AirChaseActionId => XControllerLookPoseController.AirborneBodyWeight,
                WeaponTransitionActionId => WeaponBodyLookWeight,
                ReactionActionId => 0.0f,
                _ => 1.0f
            };
            float headWeight = actionId switch
            {
                DodgeActionId => 0.0f,
                LightAttackActionId => AttackHeadLookWeight,
                AirAttackActionId => AttackHeadLookWeight,
                SpecialAttackActionId => AttackHeadLookWeight,
                HeavyAttackActionId => AttackHeadLookWeight,
                LauncherActionId => AttackHeadLookWeight,
                AirChaseActionId => XControllerLookPoseController.AirborneHeadWeight,
                WeaponTransitionActionId => WeaponHeadLookWeight,
                ReactionActionId => 0.0f,
                _ => 1.0f
            };
            _lookPose.SetActionWeights(bodyWeight, headWeight);
            _actionLayerIndex = layerIndex;
        }

        private void ClearActionPresentationPolicy()
        {
            _lookPose.SetActionWeights(1.0f, 1.0f);
            _actionLayerIndex = -1;
        }

        private float ResolveFallingVelocity(in CharacterLocomotionFrame frame)
        {
            if (!frame.IsGrounded)
            {
                _lastAirborneVerticalSpeed = frame.VerticalSpeed;
                return frame.VerticalSpeed;
            }

            // XController chooses a landing state from FallingDuration on the
            // same update that OnGround becomes true. Preserve the final airborne
            // velocity for that update instead of erasing the landing severity.
            return _wasGrounded ? 0.0f : _lastAirborneVerticalSpeed;
        }

        private bool TryResolveActionState(
            in AnimationActionRequest request,
            out int layerIndex,
            out int stateHash,
            out float crossFadeSeconds)
        {
            layerIndex = XControllerAnimationContract.BaseLayerIndex;
            switch (request.Action.Value)
            {
                case DodgeActionId:
                    bool backward = request.Variant == 1;
                    stateHash = backward ? RollBackwardStateHash : RollForwardStateHash;
                    crossFadeSeconds = DodgeCrossFadeSeconds;
                    break;

                case LightAttackActionId when request.Variant >= 0 &&
                                              request.Variant < LightAttackStateHashes.Length:
                    stateHash = LightAttackStateHashes[request.Variant];
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case AirAttackActionId when request.Variant >= 0 &&
                                             request.Variant < AirAttackStateHashes.Length:
                    stateHash = AirAttackStateHashes[request.Variant];
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case SpecialAttackActionId when request.Variant == 0:
                    stateHash = SpecialAttackStateHash;
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case HeavyAttackActionId when request.Variant == 0:
                    stateHash = HeavyAttackStateHash;
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case LauncherActionId when request.Variant == 0:
                    stateHash = LauncherStateHash;
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case AirChaseActionId when request.Variant == 0:
                    stateHash = AirChaseStateHash;
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                case WeaponTransitionActionId when request.Variant is 0 or 1:
                    layerIndex = _switchWeaponLayerIndex;
                    stateHash = request.Variant == 1
                        ? WeaponUnsheatheStateHash
                        : WeaponSheatheStateHash;
                    crossFadeSeconds = WeaponCrossFadeSeconds;
                    break;

                case ReactionActionId when request.Variant >= 0 &&
                                           request.Variant < ReactionStateHashes.Length:
                    stateHash = ReactionStateHashes[request.Variant];
                    crossFadeSeconds = AttackCrossFadeSeconds;
                    break;

                default:
                    layerIndex = -1;
                    stateHash = 0;
                    crossFadeSeconds = 0.0f;
                    return false;
            }

            return layerIndex >= 0 && _animator.HasState(layerIndex, stateHash);
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

            IReadOnlyList<XControllerParameterRequirement> parameters =
                XControllerAnimationContract.RequiredParameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                XControllerParameterRequirement requirement = parameters[i];
                RequireParameter(
                    Animator.StringToHash(requirement.Name),
                    requirement.Type,
                    requirement.Name);
            }

            RequireLayer(XControllerAnimationContract.BodyLookLayerName);
            RequireLayer(XControllerAnimationContract.HeadLookLayerName);
            RequireLayer(XControllerAnimationContract.SwitchWeaponLayerName);

            IReadOnlyList<string> baseStates =
                XControllerAnimationContract.RequiredBaseLayerStatePaths;
            for (int i = 0; i < baseStates.Count; i++)
            {
                string statePath = baseStates[i];
                RequireState(
                    Animator.StringToHash(statePath),
                    statePath);
            }

            IReadOnlyList<string> switchWeaponStates =
                XControllerAnimationContract.RequiredSwitchWeaponStatePaths;
            for (int i = 0; i < switchWeaponStates.Count; i++)
            {
                string statePath = switchWeaponStates[i];
                RequireState(
                    _switchWeaponLayerIndex,
                    Animator.StringToHash(statePath),
                    statePath);
            }
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
            RequireState(
                XControllerAnimationContract.BaseLayerIndex,
                stateHash,
                stateName);
        }

        private void RequireState(int layerIndex, int stateHash, string stateName)
        {
            if (layerIndex < 0 || !_animator.HasState(layerIndex, stateHash))
            {
                throw new InvalidOperationException(
                    $"Animator Controller on '{_animator.name}' is missing required state '{stateName}'.");
            }
        }

        private void RequireLayer(string layerName)
        {
            if (_animator.GetLayerIndex(layerName) < 0)
            {
                throw new InvalidOperationException(
                    $"Animator Controller on '{_animator.name}' is missing required layer '{layerName}'.");
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

        private static int[] CreateStateHashes(IReadOnlyList<string> statePaths)
        {
            var hashes = new int[statePaths.Count];
            for (int i = 0; i < statePaths.Count; i++)
            {
                hashes[i] = Animator.StringToHash(statePaths[i]);
            }

            return hashes;
        }
    }
}
