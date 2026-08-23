using System;
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
        private const int BaseLayerIndex = 0;
        private const string SwitchWeaponLayerName = "SwitchWeapon Layer";
        private const string BodyLookLayerName = "BodyLook";
        private const string HeadLookLayerName = "HeadLook";
        private const float GaitDampSeconds = 0.33f;
        private const float DirectionDampSeconds = 0.12f;
        private const float DirectionEpsilon = 0.0001f;
        private const float DodgeCrossFadeSeconds = 0.10f;
        private const float AttackCrossFadeSeconds = 0.12f;
        private const float WeaponCrossFadeSeconds = 0.15f;
        private const float ActionCancelCrossFadeSeconds = 0.12f;
        private const float BodyHorizontalPoseDegrees = 52.0f;
        private const float BodyVerticalPoseDegrees = 50.0f;
        private const float HeadHorizontalPoseDegrees = 90.0f;
        private const float HeadVerticalPoseDegrees = 90.0f;
        private const float BodyHorizontalLimit = 0.45f;
        private const float BodyVerticalLimit = 0.30f;
        private const float HeadHorizontalLimit = 0.60f;
        private const float HeadVerticalLimit = 0.45f;
        private const float BodyHorizontalShare = 0.35f;
        private const float BodyVerticalShare = 0.30f;
        private const float BodyResponseSpeed = 3.5f;
        private const float HeadResponseSpeed = 8.0f;
        private const float AirborneBodyLookWeight = 0.0f;
        private const float AirborneHeadLookWeight = 0.45f;
        private const float AttackBodyLookWeight = 0.15f;
        private const float AttackHeadLookWeight = 0.35f;
        private const float WeaponBodyLookWeight = 0.35f;
        private const float WeaponHeadLookWeight = 0.65f;

        private const int DodgeActionId = 1;
        private const int LightAttackActionId = 2;
        private const int WeaponTransitionActionId = 3;
        private const int ReactionActionId = 4;

        private static readonly int OnGroundHash = Animator.StringToHash("OnGround");
        private static readonly int MovementDirectionHash = Animator.StringToHash("MovementDirection");
        private static readonly int MovementTypeHash = Animator.StringToHash("MovementType");
        private static readonly int FallingDurationHash = Animator.StringToHash("FallingDuration");
        private static readonly int SpeedMultiplierHash = Animator.StringToHash("SpeedMultiplier");
        private static readonly int BodyLookXHash = Animator.StringToHash("BodyLookX");
        private static readonly int BodyLookYHash = Animator.StringToHash("BodyLookY");
        private static readonly int HeadLookXHash = Animator.StringToHash("HeadLookX");
        private static readonly int HeadLookYHash = Animator.StringToHash("HeadLookY");

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
        private static readonly int[] ReactionStateHashes =
        {
            Animator.StringToHash("Base Layer.Reaction.HitStun"),
            Animator.StringToHash("Base Layer.Reaction.Knockdown"),
            Animator.StringToHash("Base Layer.Reaction.Airborne"),
            Animator.StringToHash("Base Layer.Reaction.Dead")
        };
        private static readonly int WeaponSheatheStateHash = Animator.StringToHash(
            "SwitchWeapon Layer.TwoHanded_Sheathe");
        private static readonly int WeaponUnsheatheStateHash = Animator.StringToHash(
            "SwitchWeapon Layer.TwoHanded_Unsheathe");
        private static readonly int WeaponWaitingStateHash = Animator.StringToHash(
            "SwitchWeapon Layer.Waiting");

        private readonly Animator _animator;
        private readonly int _switchWeaponLayerIndex;

        private int _nextSequence;
        private float _lastAirborneVerticalSpeed;
        private float _bodyLookX;
        private float _bodyLookY;
        private float _headLookX;
        private float _headLookY;
        private float _actionBodyLookWeight = 1.0f;
        private float _actionHeadLookWeight = 1.0f;
        private int _actionLayerIndex = -1;
        private bool _wasGrounded = true;
        private bool _isGrounded = true;
        private bool _disposed;

        public XControllerCharacterAnimationDriver(Animator animator)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _switchWeaponLayerIndex = _animator.GetLayerIndex(SwitchWeaponLayerName);

            ValidateAnimator();
            _animator.applyRootMotion = false;
        }

        public void ApplyLook(in CharacterLookFrame frame, float deltaTime)
        {
            ThrowIfDisposed();

            float bodyWeight = _actionBodyLookWeight *
                               (_isGrounded ? 1.0f : AirborneBodyLookWeight);
            float headWeight = _actionHeadLookWeight *
                               (_isGrounded ? 1.0f : AirborneHeadLookWeight);
            ResolveLookTargets(
                in frame,
                bodyWeight,
                headWeight,
                out float bodyTargetX,
                out float bodyTargetY,
                out float headTargetX,
                out float headTargetY);

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            _bodyLookX = Damp(_bodyLookX, bodyTargetX, BodyResponseSpeed, safeDeltaTime);
            _bodyLookY = Damp(_bodyLookY, bodyTargetY, BodyResponseSpeed, safeDeltaTime);
            _headLookX = Damp(_headLookX, headTargetX, HeadResponseSpeed, safeDeltaTime);
            _headLookY = Damp(_headLookY, headTargetY, HeadResponseSpeed, safeDeltaTime);

            _animator.SetFloat(BodyLookXHash, _bodyLookX);
            _animator.SetFloat(BodyLookYHash, _bodyLookY);
            _animator.SetFloat(HeadLookXHash, _headLookX);
            _animator.SetFloat(HeadLookYHash, _headLookY);
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
            _isGrounded = frame.IsGrounded;
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
            else if (_animator.HasState(BaseLayerIndex, MoveStateHash))
            {
                _animator.CrossFadeInFixedTime(
                    MoveStateHash,
                    ActionCancelCrossFadeSeconds,
                    BaseLayerIndex,
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
            _animator.SetFloat(BodyLookXHash, 0.0f);
            _animator.SetFloat(BodyLookYHash, 0.0f);
            _animator.SetFloat(HeadLookXHash, 0.0f);
            _animator.SetFloat(HeadLookYHash, 0.0f);
            _animator.SetBool(OnGroundHash, true);
            _animator.speed = 1.0f;
            _lastAirborneVerticalSpeed = 0.0f;
            _bodyLookX = 0.0f;
            _bodyLookY = 0.0f;
            _headLookX = 0.0f;
            _headLookY = 0.0f;
            _wasGrounded = true;
            _isGrounded = true;
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

        private static void ResolveLookTargets(
            in CharacterLookFrame frame,
            float bodyWeight,
            float headWeight,
            out float bodyX,
            out float bodyY,
            out float headX,
            out float headY)
        {
            float yawDegrees = Mathf.Clamp(frame.YawDegrees, -180.0f, 180.0f);
            float pitchDegrees = Mathf.Clamp(frame.PitchDegrees, -89.0f, 89.0f);

            float bodyYawDegrees = Mathf.Clamp(
                yawDegrees * BodyHorizontalShare,
                -BodyHorizontalPoseDegrees * BodyHorizontalLimit,
                BodyHorizontalPoseDegrees * BodyHorizontalLimit) * bodyWeight;
            float bodyPitchDegrees = Mathf.Clamp(
                pitchDegrees * BodyVerticalShare,
                -BodyVerticalPoseDegrees * BodyVerticalLimit,
                BodyVerticalPoseDegrees * BodyVerticalLimit) * bodyWeight;
            float headYawDegrees = Mathf.Clamp(
                yawDegrees - bodyYawDegrees,
                -HeadHorizontalPoseDegrees * HeadHorizontalLimit,
                HeadHorizontalPoseDegrees * HeadHorizontalLimit);
            float headPitchDegrees = Mathf.Clamp(
                pitchDegrees - bodyPitchDegrees,
                -HeadVerticalPoseDegrees * HeadVerticalLimit,
                HeadVerticalPoseDegrees * HeadVerticalLimit);

            bodyX = bodyYawDegrees / BodyHorizontalPoseDegrees;
            bodyY = bodyPitchDegrees / BodyVerticalPoseDegrees;
            headX = headYawDegrees / HeadHorizontalPoseDegrees * headWeight;
            headY = headPitchDegrees / HeadVerticalPoseDegrees * headWeight;
        }

        private static float Damp(float current, float target, float responseSpeed, float deltaTime)
        {
            float interpolation = 1.0f - Mathf.Exp(-responseSpeed * deltaTime);
            return Mathf.Lerp(current, target, interpolation);
        }

        private void SetActionPresentationPolicy(int actionId, int layerIndex)
        {
            _actionBodyLookWeight = actionId switch
            {
                DodgeActionId => 0.0f,
                LightAttackActionId => AttackBodyLookWeight,
                WeaponTransitionActionId => WeaponBodyLookWeight,
                ReactionActionId => 0.0f,
                _ => 1.0f
            };
            _actionHeadLookWeight = actionId switch
            {
                DodgeActionId => 0.0f,
                LightAttackActionId => AttackHeadLookWeight,
                WeaponTransitionActionId => WeaponHeadLookWeight,
                ReactionActionId => 0.0f,
                _ => 1.0f
            };
            _actionLayerIndex = layerIndex;
        }

        private void ClearActionPresentationPolicy()
        {
            _actionBodyLookWeight = 1.0f;
            _actionHeadLookWeight = 1.0f;
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
            layerIndex = BaseLayerIndex;
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
            RequireParameter(
                BodyLookXHash,
                AnimatorControllerParameterType.Float,
                "BodyLookX");
            RequireParameter(
                BodyLookYHash,
                AnimatorControllerParameterType.Float,
                "BodyLookY");
            RequireParameter(
                HeadLookXHash,
                AnimatorControllerParameterType.Float,
                "HeadLookX");
            RequireParameter(
                HeadLookYHash,
                AnimatorControllerParameterType.Float,
                "HeadLookY");
            RequireLayer(BodyLookLayerName);
            RequireLayer(HeadLookLayerName);
            RequireLayer(SwitchWeaponLayerName);

            RequireState(MoveStateHash, "Base Layer.Locomotion.Move");
            RequireState(RollForwardStateHash, "Base Layer.Locomotion.Roll.RollFront");
            RequireState(RollBackwardStateHash, "Base Layer.Locomotion.Roll.RollBack");
            for (int i = 0; i < LightAttackStateHashes.Length; i++)
            {
                RequireState(
                    LightAttackStateHashes[i],
                    $"Base Layer.Attack.BaseAttack{i + 1}");
            }
            for (int i = 0; i < ReactionStateHashes.Length; i++)
            {
                string stateName = i switch
                {
                    0 => "HitStun",
                    1 => "Knockdown",
                    2 => "Airborne",
                    3 => "Dead",
                    _ => throw new ArgumentOutOfRangeException(nameof(i), i, null)
                };
                RequireState(
                    ReactionStateHashes[i],
                    $"Base Layer.Reaction.{stateName}");
            }
            RequireState(
                _switchWeaponLayerIndex,
                WeaponSheatheStateHash,
                "SwitchWeapon Layer.TwoHanded_Sheathe");
            RequireState(
                _switchWeaponLayerIndex,
                WeaponUnsheatheStateHash,
                "SwitchWeapon Layer.TwoHanded_Unsheathe");
            RequireState(
                _switchWeaponLayerIndex,
                WeaponWaitingStateHash,
                "SwitchWeapon Layer.Waiting");
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
            RequireState(BaseLayerIndex, stateHash, stateName);
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
    }
}
