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
        private const float DefenseCrossFadeSeconds = 0.10f;
        private const float ActionCancelCrossFadeSeconds = 0.12f;
        private const float AttackBodyLookWeight = 0.15f;
        private const float AttackHeadLookWeight = 0.35f;
        private const float WeaponBodyLookWeight = 0.35f;
        private const float WeaponHeadLookWeight = 0.65f;
        private const float MinimumActionPlaybackRate = 0.5f;
        private const float MaximumActionPlaybackRate = 2.0f;

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
        private static readonly int[] ReactionStateHashes = CreateStateHashes(
            XControllerAnimationContract.ReactionStatePaths);
        private static readonly int WeaponSheatheStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponSheatheStatePath);
        private static readonly int WeaponDrawStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponDrawStatePath);
        private static readonly int WeaponWaitingStateHash = Animator.StringToHash(
            XControllerAnimationContract.WeaponWaitingStatePath);
        private static readonly int DefenseWaitingStateHash = Animator.StringToHash(
            XControllerAnimationContract.DefenseWaitingStatePath);

        private readonly Animator _animator;
        private readonly XControllerLookPoseController _lookPose;
        private readonly int _switchWeaponLayerIndex;
        private readonly int _defenseLayerIndex;
        private readonly Dictionary<string, AnimationClip> _activeClipsBySlot =
            new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

        private int _nextSequence;
        private float _lastAirborneVerticalSpeed;
        private RuntimeAnimatorController _cachedClipController;
        private float _actionPlaybackRate = 1.0f;
        private float _timePlaybackRate = 1.0f;
        private int _actionLayerIndex = -1;
        private int _actionStateHash;
        private int _releasedActionLayerIndex = -1;
        private int _releasedActionStateHash;
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
            _defenseLayerIndex = _animator.GetLayerIndex(
                XControllerAnimationContract.DefenseLayerName);

            ValidateAnimator();
            _animator.applyRootMotion = false;
        }

        public void ApplyLook(in CharacterLookFrame frame, float deltaTime)
        {
            ThrowIfDisposed();
            TryReleaseCompletedActionPolicy();
            _lookPose.Apply(in frame, deltaTime);
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            ThrowIfDisposed();
            TryReleaseCompletedActionPolicy();

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            float planarSpeed = Mathf.Sqrt(
                frame.ForwardSpeed * frame.ForwardSpeed +
                frame.LateralSpeed * frame.LateralSpeed);
            float movementType = planarSpeed > DirectionEpsilon
                ? (float)frame.Gait
                : 0.0f;
            float movementDirection = ResolveMovementDirection(frame, planarSpeed);
            float fallingVelocity = ResolveFallingVelocity(frame);

            if (frame.IsMovementLocked)
            {
                // Landing recovery must neutralize these parameters immediately.
                // XController has input-driven exits from landing states, so normal
                // damping would allow stale movement to skip the recovery clip.
                _animator.SetFloat(MovementTypeHash, 0.0f);
                _animator.SetFloat(MovementDirectionHash, 0.0f);
            }
            else
            {
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
            }
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
                    out string statePath,
                    out float crossFadeSeconds,
                    out float normalizedTimeOffset))
            {
                return AnimationActionPlayback.Invalid;
            }

            NeutralizeReplacedActionLayer(layerIndex);
            ClearReleasedActionTracking();
            _actionPlaybackRate = ResolveActionPlaybackRate(in request, statePath);
            ApplyPlaybackRate();
            _animator.CrossFadeInFixedTime(
                stateHash,
                crossFadeSeconds,
                layerIndex,
                normalizedTimeOffset);
            SetActionPresentationPolicy(request.Action, layerIndex, stateHash);

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

            ReleaseActionPlaybackPolicy();
        }

        public void CancelAction()
        {
            if (_disposed || _animator == null)
            {
                return;
            }

            bool cancelSwitchWeaponLayer =
                _actionLayerIndex == _switchWeaponLayerIndex ||
                _releasedActionLayerIndex == _switchWeaponLayerIndex;
            bool cancelDefenseLayer =
                _actionLayerIndex == _defenseLayerIndex ||
                _releasedActionLayerIndex == _defenseLayerIndex;
            ResetActionPlaybackRate();
            ClearActionPresentationPolicy();

            if (cancelSwitchWeaponLayer)
            {
                ReturnSwitchWeaponLayerToWaiting();
            }
            else if (cancelDefenseLayer)
            {
                ReturnDefenseLayerToWaiting();
            }
            else
            {
                ReturnBaseLayerToMove();
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();

            _timePlaybackRate = Mathf.Max(0.0f, speed);
            _animator.SetFloat(SpeedMultiplierHash, _timePlaybackRate);
            ApplyPlaybackRate();
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
            _actionPlaybackRate = 1.0f;
            _timePlaybackRate = 1.0f;
            ClearReleasedActionTracking();
            ApplyPlaybackRate();
            _lastAirborneVerticalSpeed = 0.0f;
            _wasGrounded = true;
            _lookPose.Reset();
            ClearActionPresentationPolicy();
            ReturnSwitchWeaponLayerToWaiting();
            ReturnDefenseLayerToWaiting();
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

        private void SetActionPresentationPolicy(
            AnimationActionId action,
            int layerIndex,
            int stateHash)
        {
            float bodyWeight;
            float headWeight;
            if (action == PlayerAnimatorActions.Dodge ||
                action == PlayerAnimatorActions.Reaction ||
                IsDefenseAction(action))
            {
                bodyWeight = 0.0f;
                headWeight = 0.0f;
            }
            else if (action == PlayerAnimatorActions.AirChase)
            {
                bodyWeight = XControllerLookPoseController.AirborneBodyWeight;
                headWeight = XControllerLookPoseController.AirborneHeadWeight;
            }
            else if (action == PlayerAnimatorActions.WeaponTransition)
            {
                bodyWeight = WeaponBodyLookWeight;
                headWeight = WeaponHeadLookWeight;
            }
            else if (IsAttackAction(action))
            {
                bodyWeight = AttackBodyLookWeight;
                headWeight = AttackHeadLookWeight;
            }
            else
            {
                bodyWeight = 1.0f;
                headWeight = 1.0f;
            }

            _lookPose.SetActionWeights(bodyWeight, headWeight);
            _actionLayerIndex = layerIndex;
            _actionStateHash = stateHash;
        }

        private static bool IsAttackAction(AnimationActionId action)
        {
            return action == PlayerAnimatorActions.LightAttack ||
                   action == PlayerAnimatorActions.AirAttack ||
                   action == PlayerAnimatorActions.SpecialAttack ||
                   action == PlayerAnimatorActions.HeavyAttack ||
                   action == PlayerAnimatorActions.Launcher;
        }

        private static bool IsDefenseAction(AnimationActionId action)
        {
            return action == PlayerAnimatorActions.Guard ||
                   action == PlayerAnimatorActions.Block ||
                   action == PlayerAnimatorActions.Parry;
        }

        private void ClearActionPresentationPolicy()
        {
            _lookPose.SetActionWeights(1.0f, 1.0f);
            _actionLayerIndex = -1;
            _actionStateHash = 0;
        }

        private void NeutralizeReplacedActionLayer(int nextLayerIndex)
        {
            NeutralizeLayer(_actionLayerIndex, nextLayerIndex);
            if (_releasedActionLayerIndex != _actionLayerIndex)
            {
                NeutralizeLayer(_releasedActionLayerIndex, nextLayerIndex);
            }
        }

        private void NeutralizeLayer(int previousLayerIndex, int nextLayerIndex)
        {
            if (previousLayerIndex < 0 || previousLayerIndex == nextLayerIndex)
            {
                return;
            }

            if (previousLayerIndex == _switchWeaponLayerIndex)
            {
                ReturnSwitchWeaponLayerToWaiting();
            }
            else if (previousLayerIndex == _defenseLayerIndex)
            {
                ReturnDefenseLayerToWaiting();
            }
            else if (previousLayerIndex == XControllerAnimationContract.BaseLayerIndex)
            {
                // Overlay actions cannot replace a full-body action by changing
                // their own layer. Return the base layer to locomotion first.
                ReturnBaseLayerToMove();
            }
        }

        private void ReturnSwitchWeaponLayerToWaiting()
        {
            if (_switchWeaponLayerIndex < 0)
            {
                return;
            }

            _animator.CrossFadeInFixedTime(
                WeaponWaitingStateHash,
                ActionCancelCrossFadeSeconds,
                _switchWeaponLayerIndex,
                0.0f);
        }

        private void ReturnDefenseLayerToWaiting()
        {
            if (_defenseLayerIndex < 0)
            {
                return;
            }

            _animator.CrossFadeInFixedTime(
                DefenseWaitingStateHash,
                ActionCancelCrossFadeSeconds,
                _defenseLayerIndex,
                0.0f);
        }

        private void ReturnBaseLayerToMove()
        {
            if (!_animator.HasState(
                    XControllerAnimationContract.BaseLayerIndex,
                    MoveStateHash))
            {
                return;
            }

            _animator.CrossFadeInFixedTime(
                MoveStateHash,
                ActionCancelCrossFadeSeconds,
                XControllerAnimationContract.BaseLayerIndex,
                0.0f);
        }

        private void ReleaseActionPlaybackPolicy()
        {
            if (_actionLayerIndex >= 0 &&
                _actionStateHash != 0)
            {
                _releasedActionLayerIndex = _actionLayerIndex;
                _releasedActionStateHash = _actionStateHash;
                ClearActiveActionTracking();
            }
            else
            {
                ResetActionPlaybackRate();
                ClearActionPresentationPolicy();
            }
        }

        private void TryReleaseCompletedActionPolicy()
        {
            if (_releasedActionLayerIndex < 0)
            {
                return;
            }

            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(
                _releasedActionLayerIndex);
            bool releasedStateIsActive =
                current.fullPathHash == _releasedActionStateHash;
            if (_animator.IsInTransition(_releasedActionLayerIndex))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(
                    _releasedActionLayerIndex);
                releasedStateIsActive |=
                    next.fullPathHash == _releasedActionStateHash;
            }

            if (releasedStateIsActive)
            {
                return;
            }

            ResetActionPlaybackRate();
            ClearActionPresentationPolicy();
        }

        private void ResetActionPlaybackRate()
        {
            _actionPlaybackRate = 1.0f;
            ClearReleasedActionTracking();
            ApplyPlaybackRate();
        }

        private void ClearReleasedActionTracking()
        {
            _releasedActionLayerIndex = -1;
            _releasedActionStateHash = 0;
        }

        private void ClearActiveActionTracking()
        {
            _actionLayerIndex = -1;
            _actionStateHash = 0;
        }

        private void ApplyPlaybackRate()
        {
            _animator.speed = _actionPlaybackRate * _timePlaybackRate;
        }

        private float ResolveActionPlaybackRate(
            in AnimationActionRequest request,
            string statePath)
        {
            if ((!IsAttackAction(request.Action) &&
                 request.Action != PlayerAnimatorActions.Dodge) ||
                request.ExpectedDuration <= 0.0f ||
                string.IsNullOrEmpty(statePath))
            {
                return 1.0f;
            }

            string clipSlot = statePath.Substring(statePath.LastIndexOf('.') + 1);
            RefreshActiveClipSlots();
            if (!_activeClipsBySlot.TryGetValue(clipSlot, out AnimationClip clip) ||
                clip == null ||
                clip.length <= 0.0f)
            {
                return 1.0f;
            }

            return Mathf.Clamp(
                clip.length / request.ExpectedDuration,
                MinimumActionPlaybackRate,
                MaximumActionPlaybackRate);
        }

        private void RefreshActiveClipSlots()
        {
            RuntimeAnimatorController controller =
                _animator.runtimeAnimatorController;
            if (controller == _cachedClipController)
            {
                return;
            }

            _activeClipsBySlot.Clear();
            _cachedClipController = controller;
            if (controller == null)
            {
                return;
            }

            if (controller is AnimatorOverrideController animationOverride)
            {
                var overrides =
                    new List<KeyValuePair<AnimationClip, AnimationClip>>(
                        animationOverride.overridesCount);
                animationOverride.GetOverrides(overrides);
                for (int i = 0; i < overrides.Count; i++)
                {
                    KeyValuePair<AnimationClip, AnimationClip> pair = overrides[i];
                    if (pair.Key != null)
                    {
                        _activeClipsBySlot[pair.Key.name] =
                            pair.Value != null ? pair.Value : pair.Key;
                    }
                }

                return;
            }

            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null)
                {
                    _activeClipsBySlot[clip.name] = clip;
                }
            }
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
            out string statePath,
            out float crossFadeSeconds,
            out float normalizedTimeOffset)
        {
            layerIndex = XControllerAnimationContract.BaseLayerIndex;
            normalizedTimeOffset = 0.0f;
            if (XControllerAnimationContract.TryGetCombatActionStatePath(
                    request.Action,
                    request.Variant,
                    out string combatStatePath))
            {
                statePath = combatStatePath;
                stateHash = Animator.StringToHash(combatStatePath);
                crossFadeSeconds = AttackCrossFadeSeconds;
                return _animator.HasState(layerIndex, stateHash);
            }

            if (XControllerAnimationContract.TryGetDefenseActionStatePath(
                    request.Action,
                    request.Variant,
                    out string defenseStatePath,
                    out normalizedTimeOffset))
            {
                statePath = defenseStatePath;
                if (request.Action == PlayerAnimatorActions.Guard)
                {
                    layerIndex = _defenseLayerIndex;
                }
                stateHash = Animator.StringToHash(defenseStatePath);
                crossFadeSeconds = DefenseCrossFadeSeconds;
                return _animator.HasState(layerIndex, stateHash);
            }

            if (request.Action == PlayerAnimatorActions.Dodge &&
                request.Variant is 0 or 1)
            {
                statePath = request.Variant == 1
                    ? XControllerAnimationContract.RollBackwardStatePath
                    : XControllerAnimationContract.RollForwardStatePath;
                stateHash = request.Variant == 1
                    ? RollBackwardStateHash
                    : RollForwardStateHash;
                crossFadeSeconds = DodgeCrossFadeSeconds;
            }
            else if (request.Action == PlayerAnimatorActions.WeaponTransition &&
                     request.Variant is 0 or 1)
            {
                statePath = request.Variant == 1
                    ? XControllerAnimationContract.WeaponDrawStatePath
                    : XControllerAnimationContract.WeaponSheatheStatePath;
                layerIndex = _switchWeaponLayerIndex;
                stateHash = request.Variant == 1
                    ? WeaponDrawStateHash
                    : WeaponSheatheStateHash;
                crossFadeSeconds = WeaponCrossFadeSeconds;
            }
            else if (request.Action == PlayerAnimatorActions.Reaction &&
                     request.Variant >= 0 &&
                     request.Variant < ReactionStateHashes.Length)
            {
                statePath = XControllerAnimationContract.ReactionStatePaths[
                    request.Variant];
                stateHash = ReactionStateHashes[request.Variant];
                crossFadeSeconds = AttackCrossFadeSeconds;
            }
            else
            {
                layerIndex = -1;
                stateHash = 0;
                statePath = null;
                crossFadeSeconds = 0.0f;
                normalizedTimeOffset = 0.0f;
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
            RequireLayer(XControllerAnimationContract.DefenseLayerName);

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

            IReadOnlyList<string> defenseStates =
                XControllerAnimationContract.RequiredDefenseLayerStatePaths;
            for (int i = 0; i < defenseStates.Count; i++)
            {
                string statePath = defenseStates[i];
                RequireState(
                    _defenseLayerIndex,
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
