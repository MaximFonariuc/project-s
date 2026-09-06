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
		private readonly Character _character;
		private readonly PlayerAnimationCoordinator _animation;
		private readonly IPlayerLocomotionState _locomotionState;
		private readonly LayerMask _ladderMask;

		private Ladder _activeLadder;
		private EClimbingState _climbingState;
		private ERotationMode _previousRotationMode;

		private CancellationTokenSource _stepCts;
		private CancellationTokenSource _lifecycleCts;
		private bool _enabled;
		private bool _reportedMissingPresentation;

		private const float STEP_HEIGHT = 0.52f;
		private const float EXIT_CLIMB_FRAMES = 35f;
		private const float EXIT_MOVE_FRAMES = 15f;
		private const float EXIT_TOTAL_FRAMES = 55f;

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
			_locomotionState.SpeedMode == PlayerSpeedMode.Sprint ? 1.5f : 1.0f;

		public LadderTraversalAction(
			Character character,
			PlayerAnimationCoordinator animation,
			IPlayerLocomotionState locomotionState,
			LadderTraversalSettings settings)
		{
			_character = character ?? throw new ArgumentNullException(nameof(character));
			_animation = animation ?? throw new ArgumentNullException(nameof(animation));
			_locomotionState = locomotionState ?? throw new ArgumentNullException(nameof(locomotionState));
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			_ladderMask = settings.LadderMask;
		}

		private bool CanClimb()
		{
			if (_character.IsCrouched() || !HasLadderPresentation())
			{
				return false;
			}

			var overlapped = _character.CharacterMovement.OverlapTest(_ladderMask, QueryTriggerInteraction.Collide, out int count);
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

		public void BeginClimb()
		{
			if (!_enabled || _lifecycleCts == null)
			{
				return;
			}

			ClimbAsync(_lifecycleCts.Token).Forget();
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
				_character.SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Climbing);

				float distToBottom = Vector3.Distance(_character.GetPosition(), _activeLadder.BottomPoint.position);
				float distToTop = Vector3.Distance(_character.GetPosition(), _activeLadder.TopPoint.position);
				bool enterFromTop = distToTop < distToBottom;

				_climbingState = EClimbingState.Grabbing;

				Transform snapPos = _activeLadder.ClosestPointOnPath(_character.GetPosition(), out _);

				if (enterFromTop)
				{
					Vector3 charForward = _character.transform.forward;
					Vector3 snapForward = snapPos.forward;

					float angle = Vector3.Angle(charForward, snapForward);

					if (angle <= 90f)
					{
						await SmoothAlignToLadder(snapPos, 0.25f, false, token);
						await PerformEnterTop(PlayerAnimationSlots.LadderEnterTopFront, 40f, 18f, true, token);
					}
					else
					{
						await SmoothAlignToLadder(snapPos, 0.25f, true, token);
						await PerformEnterTop(PlayerAnimationSlots.LadderEnterTopBack, 15f, 35f, false, token);
					}
				}
				else
				{
					await SmoothAlignToLadder(snapPos, 0.25f, false, token);
					AnimationPlayback playback = Play(PlayerAnimationSlots.LadderEnterBottom);
					await WaitForClimbEnter(playback.Duration, token);
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

		private async UniTask PerformEnterTop(AnimationSlotId slot, float moveFrames, float climbFrames, bool invertRot = false, CancellationToken token = default)
		{
			float clipLen = Play(slot).Duration;

			float totalFrames = moveFrames + climbFrames;
			float moveDur = clipLen * (moveFrames / totalFrames);
			float climbDur = clipLen * (climbFrames / totalFrames);

			Vector3 start = _character.GetPosition();
			Vector3 moveTarget = start - _activeLadder.transform.forward * STEP_HEIGHT;

			Quaternion startRot = _character.transform.rotation;
			Quaternion targetRot = invertRot ? startRot * Quaternion.Euler(0f, 180f, 0f) : startRot;

			if (invertRot)
			{
				await LerpMovementAndRotation(start, moveTarget, startRot, targetRot, moveDur, token);
			}
			else
			{
				await LerpMovement(start, moveTarget, moveDur, token);
			}

			start = _character.GetPosition();
			Vector3 climbTarget = start - _activeLadder.transform.up * (STEP_HEIGHT * 1.5f);

			await LerpMovement(start, climbTarget, climbDur, token);
			_character.TeleportPosition(climbTarget);

			await WaitForClimbEnter(token: token);
		}

		private async UniTask SmoothAlignToLadder(
			Transform snapPos,
			float duration,
			bool invertRotation,
			CancellationToken token)
		{
			Vector3 startPos = _character.GetPosition();
			Quaternion startRot = _character.transform.rotation;

			Vector3 targetPos = snapPos.position;
			Quaternion targetRot = invertRotation ? snapPos.rotation * Quaternion.Euler(0f, 180f, 0f) : snapPos.rotation;

			float t = 0f;
			while (t < 1f)
			{
				token.ThrowIfCancellationRequested();
				t += Time.deltaTime / duration;
				_character.TeleportPosition(Vector3.Lerp(startPos, targetPos, t), false, true);
				_character.SetRotation(Quaternion.Slerp(startRot, targetRot, t));
				await UniTask.Yield(token);
			}

			_character.TeleportPosition(targetPos, false, true);
			_character.SetRotation(targetRot);
		}

		private async UniTask LerpMovementAndRotation(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float duration, CancellationToken token)
		{
			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime / duration;
				_character.TeleportPosition(Vector3.Lerp(fromPos, toPos, t));
				_character.SetRotation(Quaternion.Slerp(fromRot, toRot, t));
				await UniTask.Yield(token);
			}
			_character.TeleportPosition(toPos);
			_character.SetRotation(toRot);
		}

		private async UniTask WaitForClimbEnter(
			float duration = 0,
			CancellationToken token = default)
		{
			await UniTask.WaitForSeconds(duration * 0.9f, cancellationToken: token);
			_character.SetRotation(_activeLadder.transform.rotation);
			_climbingState = EClimbingState.Grabbed;
			Play(PlayerAnimationSlots.LadderIdle);
		}

		public void OnClimbInput(Vector2 input)
		{
			if (!_character.IsClimbing() || _climbingState != EClimbingState.Grabbed)
			{
				return;
			}
			
			if (_stepCts is { IsCancellationRequested: false })
			{
				return;
			}

			float topY = _activeLadder.TopAnchorPoint.y;
			float botY = _activeLadder.BottomAnchorPoint.y;
			float charY = _character.GetPosition().y;

			if (input.y > 0.5f)
			{
				if (charY < topY - STEP_HEIGHT * 0.5f)
				{
					Step(+1).Forget();
				}
				else if (Mathf.Abs(charY - topY) <= 0.05f)
				{
					BeginExit(true);
				}
			}
			else if (input.y < -0.5f)
			{
				if (charY > botY + STEP_HEIGHT * 0.5f)
				{
					Step(-1).Forget();
				}
				else if (Mathf.Abs(charY - botY) <= 0.05f)
				{
					BeginExit(false);
				}
			}
			else
			{
				Play(PlayerAnimationSlots.LadderIdle);
				_animation.SetPlaybackSpeed(PlayerAnimationSource.Traversal, 1.0f);
			}
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

		private async UniTask Step(int direction)
		{
			_stepCts?.Cancel();
			var stepCancellation = new CancellationTokenSource();
			_stepCts = stepCancellation;
			var token = stepCancellation.Token;
			
			try
			{
				AnimationSlotId slot = direction > 0
					? PlayerAnimationSlots.LadderAscend
					: PlayerAnimationSlots.LadderDescend;
				float speedFactor = SpeedFactor;
				_animation.SetPlaybackSpeed(PlayerAnimationSource.Traversal, speedFactor);
				float clipLen = Play(slot).Duration / speedFactor;

				Vector3 start = _character.GetPosition();
				Vector3 target = start + _activeLadder.transform.up * (STEP_HEIGHT * direction);

				float topY = _activeLadder.TopAnchorPoint.y;
				float botY = _activeLadder.BottomAnchorPoint.y;
				target.y = Mathf.Clamp(target.y, botY, topY);

				await LerpMovement(start, target, clipLen, token);

				switch (direction)
				{
					case > 0 when Mathf.Abs(target.y - topY) < 0.05f:
						await PlayExitAndRelease(true, token);
						break;
					case < 0 when Mathf.Abs(target.y - botY) < 0.05f:
						await PlayExitAndRelease(false, token);
						break;
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception exception)
			{
				AbortClimb(exception);
			}
			finally
			{
				if (ReferenceEquals(_stepCts, stepCancellation))
				{
					_stepCts = null;
				}

				stepCancellation.Dispose();
			}
		}

		private async UniTask PlayExitAndRelease(bool exitTop = true, CancellationToken token = default)
		{
			_climbingState = EClimbingState.Releasing;

			AnimationSlotId slot = exitTop
				? PlayerAnimationSlots.LadderExitTop
				: PlayerAnimationSlots.LadderExitBottom;
			float clipLen = Play(slot).Duration;
			Vector3 start = _character.GetPosition();

			if (exitTop)
			{
				float climbDur = clipLen * (EXIT_CLIMB_FRAMES / EXIT_TOTAL_FRAMES);
				float moveDur = clipLen * (EXIT_MOVE_FRAMES / EXIT_TOTAL_FRAMES);

				Vector3 climbTarget = start + _activeLadder.transform.up * (STEP_HEIGHT * 1.5f);

				await LerpMovement(start, climbTarget, climbDur, token);

				Vector3 moveTarget = climbTarget + _activeLadder.transform.forward * STEP_HEIGHT;
				await LerpMovement(climbTarget, moveTarget, moveDur, token);
				_character.TeleportPosition(moveTarget, false, true);
			}
			else
			{	
				await LerpMovement(start, _activeLadder.BottomAnchorPoint, clipLen, token);
			}

			_character.SetRotation(_activeLadder.transform.rotation);

			_climbingState = EClimbingState.None;
			_character.SetMovementMode(EMovementMode.Walking);
			_character.SetRotationMode(_previousRotationMode);
		}

		private async UniTask LerpMovement(Vector3 from, Vector3 to, float duration, CancellationToken token)
		{
			float t = 0f;
			while (t < duration)
			{
				token.ThrowIfCancellationRequested();
				t += Time.deltaTime;
				float lerp = Mathf.Clamp01(t / duration);
				Vector3 newPos = Vector3.Lerp(from, to, lerp);
				_character.TeleportPosition(newPos, false, false);
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

		private void AbortClimb(Exception exception)
		{
			Debug.LogException(exception, _character);
			RestoreTraversalState();
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
				slot);
			if (!playback.IsValid || playback.Duration <= 0.0f)
			{
				throw new InvalidOperationException(
					$"Ladder animation slot '{slot}' or its duration is not configured.");
			}

			return playback;
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

		private void CancelOperations()
		{
			CancellationTokenSource lifecycleCancellation = _lifecycleCts;
			_lifecycleCts = null;
			lifecycleCancellation?.Cancel();
			lifecycleCancellation?.Dispose();

			CancellationTokenSource stepCancellation = _stepCts;
			_stepCts = null;
			stepCancellation?.Cancel();
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
			_animation.ReleaseAll(PlayerAnimationSource.Traversal);
		}
	}
}
