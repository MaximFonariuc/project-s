using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModularAnimation;
using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    internal sealed class LadderTraversalAction
    {
        private enum ClimbMotion
        {
            None,
            Ascend,
            Descend
        }

        private readonly Character _character;
        private readonly PlayerAnimationCoordinator _animation;
        private readonly IPlayerLocomotionState _locomotionState;
        private readonly IPlayerLadderClimbAnimation _climbAnimation;
        private readonly LayerMask _ladderMask;
        private readonly float[] _slotDurations;

        private readonly float _ascendCycleRise;
        private readonly float _approachSpeed;
        private readonly float _minApproachSeconds;
        private readonly float _maxApproachSeconds;
        private readonly float _approachTurnSpeed;
        private readonly float _climbRateBlendSeconds;
        private readonly float _hangBlendLeadSeconds;
        private readonly AnimationCurve _exitTopRiseProfile;
        private readonly AnimationCurve _exitTopForwardProfile;
        private readonly AnimationCurve _enterTopDescentProfile;
        private readonly AnimationCurve _enterTopBackwardProfile;

        private Ladder _activeLadder;
        private EClimbingState _climbingState;
        private ERotationMode _previousRotationMode;
        private ClimbMotion _motion;
        private float _stepHeight;
        private float _pathLength;
        private float _climbCycles;
        private float _climbVelocity;

        private CancellationTokenSource _lifecycleCts;
        private bool _enabled;
        private bool _reportedMissingPresentation;

        private const float EXIT_BOTTOM_SHARE = 25f / 40f;
        private const float ANCHOR_EPSILON = 0.02f;
        private const float CLIMB_INPUT_THRESHOLD = 0.5f;
        private const float SPRINT_CLIMB_RATE = 1.5f;

        private static readonly AnimationSlotId[] RequiredPresentationSlots =
        {
            PlayerAnimationSlots.LadderEnterTopFront,
            PlayerAnimationSlots.LadderEnterTopBack,
            PlayerAnimationSlots.LadderEnterBottom,
            PlayerAnimationSlots.LadderIdle,
            PlayerAnimationSlots.LadderAscend,
            PlayerAnimationSlots.LadderDescend,
            PlayerAnimationSlots.LadderExitTop,
            PlayerAnimationSlots.LadderExitBottom
        };

        private float SpeedFactor =>
            _locomotionState.SpeedMode == PlayerSpeedMode.Sprint ? SPRINT_CLIMB_RATE : 1.0f;

        private float TopCycles =>
            _stepHeight > 0.0f ? _pathLength / _stepHeight : 0.0f;

        private float AnchorEpsilonCycles =>
            _stepHeight > 0.0f ? ANCHOR_EPSILON / _stepHeight : 0.0f;

        public bool IsGripActive =>
            _activeLadder != null &&
            (_climbingState == EClimbingState.Grabbed ||
             _climbingState == EClimbingState.Releasing);

        public Ladder ActiveLadder => _activeLadder;

        public LadderTraversalAction(
            Character character,
            PlayerAnimationCoordinator animation,
            IPlayerLocomotionState locomotionState,
            IPlayerLadderClimbAnimation climbAnimation,
            LadderTraversalSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _locomotionState = locomotionState ??
                throw new ArgumentNullException(nameof(locomotionState));
            _climbAnimation = climbAnimation ??
                throw new ArgumentNullException(nameof(climbAnimation));
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _ladderMask = settings.LadderMask;
            _ascendCycleRise = settings.AscendCycleRise;
            _approachSpeed = settings.ApproachSpeed;
            _minApproachSeconds = settings.MinApproachSeconds;
            _maxApproachSeconds = settings.MaxApproachSeconds;
            _approachTurnSpeed = settings.ApproachTurnSpeed;
            _climbRateBlendSeconds = settings.ClimbRateBlendSeconds;
            _hangBlendLeadSeconds = settings.HangBlendLeadSeconds;
            _exitTopRiseProfile = settings.ExitTopRiseProfile;
            _exitTopForwardProfile = settings.ExitTopForwardProfile;
            _enterTopDescentProfile = settings.EnterTopDescentProfile;
            _enterTopBackwardProfile = settings.EnterTopBackwardProfile;
            _slotDurations = new[]
            {
                settings.EnterTopFrontSeconds,
                settings.EnterTopBackSeconds,
                settings.EnterBottomSeconds,
                settings.IdleSeconds,
                settings.AscendSeconds,
                settings.DescendSeconds,
                settings.ExitTopSeconds,
                settings.ExitBottomSeconds
            };
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _lifecycleCts = new CancellationTokenSource();
            _character.MovementModeChanged += OnMovementModeChanged;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            CancelOperations();
            RestoreTraversalState();
            _character.MovementModeChanged -= OnMovementModeChanged;
            _enabled = false;
        }

        public void Interrupt()
        {
            if (!_enabled)
            {
                return;
            }

            CancelOperations();
            RestoreTraversalState();
            _lifecycleCts = new CancellationTokenSource();
        }

        public void BeginClimb()
        {
            if (!_enabled || _lifecycleCts == null)
            {
                return;
            }

            ClimbAsync(_lifecycleCts.Token).Forget();
        }

        public void OnClimbInput(Vector2 input)
        {
            if (_activeLadder == null ||
                _climbingState != EClimbingState.Grabbed ||
                !_character.IsClimbing())
            {
                return;
            }

            if (TryExitAtAnchor(input.y))
            {
                return;
            }

            UpdateClimbVelocity(ResolveClimbDirection(input.y));
            AdvanceClimb();
            if (_climbingState == EClimbingState.Grabbed)
            {
                PresentClimb();
            }
        }

        private bool TryExitAtAnchor(float axis)
        {
            if (axis > CLIMB_INPUT_THRESHOLD && _climbCycles >= TopCycles - AnchorEpsilonCycles)
            {
                BeginExit(true);
                return true;
            }

            if (axis < -CLIMB_INPUT_THRESHOLD && _climbCycles <= AnchorEpsilonCycles)
            {
                BeginExit(false);
                return true;
            }

            return false;
        }

        private static int ResolveClimbDirection(float axis)
        {
            if (axis > CLIMB_INPUT_THRESHOLD)
            {
                return 1;
            }

            return axis < -CLIMB_INPUT_THRESHOLD ? -1 : 0;
        }

        private void UpdateClimbVelocity(int direction)
        {
            float peakRate = SpeedFactor;
            _climbVelocity = Mathf.MoveTowards(
                _climbVelocity,
                direction * peakRate,
                Time.deltaTime * peakRate / Mathf.Max(0.01f, _climbRateBlendSeconds));
        }

        private void AdvanceClimb()
        {
            if (_climbVelocity == 0.0f)
            {
                return;
            }

            float cycleSeconds = ResolveSlotDuration(_climbVelocity > 0.0f
                ? PlayerAnimationSlots.LadderAscend
                : PlayerAnimationSlots.LadderDescend);
            if (cycleSeconds <= 0.0f)
            {
                return;
            }

            _climbCycles = Mathf.Clamp(
                _climbCycles + _climbVelocity * Time.deltaTime / cycleSeconds,
                0.0f,
                TopCycles);
            _character.TeleportPosition(
                _activeLadder.PointAtHeight(_climbCycles * _stepHeight),
                false,
                false);

            if (_climbVelocity > 0.0f && _climbCycles >= TopCycles - AnchorEpsilonCycles)
            {
                BeginExit(true);
            }
            else if (_climbVelocity < 0.0f && _climbCycles <= AnchorEpsilonCycles)
            {
                BeginExit(false);
            }
        }

        private void PresentClimb()
        {
            ApplyClimbPhases();
            ClimbMotion motion = ResolveClimbMotion();
            if (motion == _motion)
            {
                return;
            }

            _motion = motion;
            Play(motion == ClimbMotion.Ascend
                ? PlayerAnimationSlots.LadderAscend
                : PlayerAnimationSlots.LadderDescend);
        }

        private void ApplyClimbPhases()
        {
            _climbAnimation.SetClimbPhases(
                Mathf.Repeat(_climbCycles, 1.0f),
                Mathf.Repeat(-_climbCycles, 1.0f));
        }

        private ClimbMotion ResolveClimbMotion()
        {
            if (_climbVelocity > 0.0f)
            {
                return ClimbMotion.Ascend;
            }

            return _climbVelocity < 0.0f ? ClimbMotion.Descend : _motion;
        }

        private bool CanClimb()
        {
            if (_character.IsCrouched() || !HasLadderPresentation())
            {
                return false;
            }

            Collider[] overlapped = _character.CharacterMovement.OverlapTest(
                _ladderMask,
                QueryTriggerInteraction.Collide,
                out int count);
            if (count == 0)
            {
                return false;
            }

            if (!overlapped[0].TryGetComponent(out Ladder ladder))
            {
                return false;
            }

            _activeLadder = ladder;
            return true;
        }

        private async UniTask ClimbAsync(CancellationToken token)
        {
            if (_character.IsClimbing() || !CanClimb())
            {
                return;
            }

            try
            {
                token.ThrowIfCancellationRequested();
                _character.SetMovementMode(
                    EMovementMode.Custom,
                    (int)ECustomMovementMode.Climbing);

                _climbingState = EClimbingState.Grabbing;
                _stepHeight = _activeLadder.ResolveStepHeight(_ascendCycleRise);
                _pathLength = _activeLadder.ResolvePathLength(_stepHeight);

                float height = _activeLadder.HeightAlongPath(_character.GetPosition());
                if (height > _pathLength * 0.5f)
                {
                    await ApproachLadder(_activeLadder.TopPoint.position, token);
                    await PerformEnterTop(token);
                }
                else
                {
                    await ApproachLadder(_activeLadder.PointAtHeight(0.0f), token);
                    AnimationPlayback playback = Play(PlayerAnimationSlots.LadderEnterBottom);
                    await UniTask.WhenAll(
                        UniTask.WaitForSeconds(playback.Duration, cancellationToken: token),
                        BlendIntoHang(0.0f, playback.Duration, token));
                    CompleteGrab(0.0f);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AbortClimb(exception);
            }
        }

        private async UniTask PerformEnterTop(CancellationToken token)
        {
            AnimationPlayback playback = Play(PlayerAnimationSlots.LadderEnterTopBack);
            await UniTask.WhenAll(
                FollowRootProfile(
                    _character.GetPosition(),
                    _activeLadder.PointAtHeight(_pathLength),
                    playback.Duration,
                    _enterTopDescentProfile,
                    _enterTopBackwardProfile,
                    token),
                BlendIntoHang(_pathLength, playback.Duration, token));
            CompleteGrab(_pathLength);
        }

        private async UniTask BlendIntoHang(float height, float entryDuration, CancellationToken token)
        {
            float lead = Mathf.Clamp(_hangBlendLeadSeconds, 0.0f, entryDuration);
            await UniTask.WaitForSeconds(entryDuration - lead, cancellationToken: token);
            _climbCycles = _stepHeight > 0.0f ? height / _stepHeight : 0.0f;
            _climbVelocity = 0.0f;
            _motion = ClimbMotion.Ascend;
            ApplyClimbPhases();
            Play(PlayerAnimationSlots.LadderAscend);
        }

        private void CompleteGrab(float height)
        {
            _character.SetRotation(_activeLadder.ClimbRotation);
            _character.TeleportPosition(_activeLadder.PointAtHeight(height), false, false);
            _climbingState = EClimbingState.Grabbed;
        }

        private async UniTask ApproachLadder(Vector3 targetPosition, CancellationToken token)
        {
            Vector3 startPosition = _character.GetPosition();
            Quaternion startRotation = _character.transform.rotation;
            Quaternion targetRotation = _activeLadder.ClimbRotation;
            float duration = Mathf.Clamp(
                Mathf.Max(
                    Vector3.Distance(startPosition, targetPosition) / _approachSpeed,
                    Quaternion.Angle(startRotation, targetRotation) / _approachTurnSpeed),
                _minApproachSeconds,
                _maxApproachSeconds);

            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float blend = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(elapsed / duration));
                _character.TeleportPosition(
                    Vector3.Lerp(startPosition, targetPosition, blend),
                    false,
                    true);
                _character.SetRotation(Quaternion.Slerp(startRotation, targetRotation, blend));
                await UniTask.Yield(token);
            }

            _character.TeleportPosition(targetPosition, false, true);
            _character.SetRotation(targetRotation);
        }

        private void BeginExit(bool exitTop)
        {
            if (!_enabled || _lifecycleCts == null)
            {
                return;
            }

            ExitAndReleaseAsync(exitTop, _lifecycleCts.Token).Forget();
        }

        private async UniTask ExitAndReleaseAsync(bool exitTop, CancellationToken token)
        {
            try
            {
                await PlayExitAndRelease(exitTop, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AbortClimb(exception);
            }
        }

        private async UniTask PlayExitAndRelease(bool exitTop, CancellationToken token)
        {
            _climbingState = EClimbingState.Releasing;
            _motion = ClimbMotion.None;
            _climbVelocity = 0.0f;

            AnimationPlayback playback = Play(exitTop
                ? PlayerAnimationSlots.LadderExitTop
                : PlayerAnimationSlots.LadderExitBottom);
            Vector3 start = _character.GetPosition();
            Vector3 target = exitTop
                ? _activeLadder.TopPoint.position
                : _activeLadder.BottomPoint.position;

            if (exitTop)
            {
                await FollowRootProfile(
                    start,
                    target,
                    playback.Duration,
                    _exitTopRiseProfile,
                    _exitTopForwardProfile,
                    token);
            }
            else
            {
                await LerpMovement(start, target, playback.Duration * EXIT_BOTTOM_SHARE, token);
            }

            _character.SetRotation(_activeLadder.ClimbRotation);
            _climbingState = EClimbingState.None;
            _character.SetMovementMode(EMovementMode.Walking);
            _character.SetRotationMode(_previousRotationMode);
        }

        private async UniTask FollowRootProfile(
            Vector3 from,
            Vector3 to,
            float duration,
            AnimationCurve axisProfile,
            AnimationCurve planarProfile,
            CancellationToken token)
        {
            Vector3 axis = _activeLadder.ClimbAxis.normalized;
            Vector3 travel = to - from;
            float axial = Vector3.Dot(travel, axis);
            Vector3 planar = travel - axis * axial;

            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                _character.TeleportPosition(
                    from +
                    axis * (axial * SampleProfile(axisProfile, normalizedTime)) +
                    planar * SampleProfile(planarProfile, normalizedTime),
                    false,
                    false);
                await UniTask.Yield(token);
            }

            _character.TeleportPosition(to, false, true);
        }

        private static float SampleProfile(AnimationCurve profile, float normalizedTime)
        {
            return profile == null || profile.length == 0
                ? normalizedTime
                : profile.Evaluate(normalizedTime);
        }

        private async UniTask LerpMovement(
            Vector3 from,
            Vector3 to,
            float duration,
            CancellationToken token)
        {
            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                _character.TeleportPosition(
                    Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration)),
                    false,
                    false);
                await UniTask.Yield(token);
            }

            _character.TeleportPosition(to, false, true);
        }

        private void OnMovementModeChanged(EMovementMode prevMode, int prevCustom)
        {
            if (_character.IsClimbing())
            {
                _character.StopJumping();
                _character.EnableGroundConstraint(false);

                _previousRotationMode = _character.RotationMode;
                _character.SetRotationMode(ERotationMode.Custom);
            }
            else if (prevMode == EMovementMode.Custom &&
                     prevCustom == (int)ECustomMovementMode.Climbing)
            {
                _character.EnableGroundConstraint(true);
                _character.SetRotationMode(_previousRotationMode);
                _animation.ReleaseAll(PlayerAnimationSource.Traversal);
            }
        }

        private void AbortClimb(Exception exception)
        {
            Debug.LogException(exception, _character);
            RestoreTraversalState();
        }

        private float ResolveSlotDuration(AnimationSlotId slot)
        {
            for (int i = 0; i < RequiredPresentationSlots.Length; i++)
            {
                if (RequiredPresentationSlots[i] == slot)
                {
                    return _slotDurations[i];
                }
            }

            return 0.0f;
        }

        private bool HasLadderPresentation()
        {
            for (int i = 0; i < RequiredPresentationSlots.Length; i++)
            {
                if (_animation.Supports(RequiredPresentationSlots[i]))
                {
                    continue;
                }

                if (!_reportedMissingPresentation)
                {
                    _reportedMissingPresentation = true;
                    Debug.LogWarning(
                        "Ladder traversal is enabled but the animation backend does not " +
                        $"support slot '{RequiredPresentationSlots[i]}'. Climbing is disabled " +
                        "until the slot is mapped.",
                        _character);
                }

                return false;
            }

            return true;
        }

        private AnimationPlayback Play(AnimationSlotId slot)
        {
            AnimationPlayback playback = _animation.CrossFade(
                PlayerAnimationSource.Traversal,
                slot,
                ResolveSlotDuration(slot));
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                throw new InvalidOperationException(
                    $"Ladder animation slot '{slot}' or its duration is not configured.");
            }

            return playback;
        }

        private void CancelOperations()
        {
            CancellationTokenSource lifecycleCancellation = _lifecycleCts;
            _lifecycleCts = null;
            lifecycleCancellation?.Cancel();
            lifecycleCancellation?.Dispose();
        }

        private void RestoreTraversalState()
        {
            if (_character.IsClimbing())
            {
                _character.SetMovementMode(
                    _character.IsGrounded() ? EMovementMode.Walking : EMovementMode.Falling);
            }

            _activeLadder = null;
            _climbingState = EClimbingState.None;
            _motion = ClimbMotion.None;
            _climbVelocity = 0.0f;
            _climbCycles = 0.0f;
            _animation.ReleaseAll(PlayerAnimationSource.Traversal);
        }
    }
}