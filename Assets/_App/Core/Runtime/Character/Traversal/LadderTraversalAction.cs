using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
	internal sealed class LadderTraversalAction
	{
		private readonly Character _character;
		private readonly AnimationController _animationController;
		private readonly LayerMask _ladderMask;

		private Ladder _activeLadder;
		private EClimbingState _climbingState;
		private ERotationMode _previousRotationMode;

		private CancellationTokenSource _stepCts;
		private CancellationTokenSource _lifecycleCts;
		private bool _enabled;

		private const float STEP_HEIGHT = 0.52f;
		private const float BLEND_TIME = 0.15f;

		private const float EXIT_CLIMB_FRAMES = 35f;
		private const float EXIT_MOVE_FRAMES = 15f;
		private const float EXIT_TOTAL_FRAMES = 55f;
		
		private float SpeedFactor => _animationController.IsSprinting ? 1.5f : 1f;

		public LadderTraversalAction(
			Character character,
			AnimationController animationController,
			LadderTraversalSettings settings)
		{
			_character = character ?? throw new ArgumentNullException(nameof(character));
			_animationController = animationController ?? throw new ArgumentNullException(nameof(animationController));
			if (settings == null)
			{
				throw new ArgumentNullException(nameof(settings));
			}

			_ladderMask = settings.LadderMask;
		}

		private bool CanClimb()
		{
			if (_character.IsCrouched())
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
						await PerformEnterTop(AnimationHashes.LadderEnterTopFront, 40f, 18f, true, token);
					}
					else
					{
						await SmoothAlignToLadder(snapPos, 0.25f, true, token);
						await PerformEnterTop(AnimationHashes.LadderEnterTopBack, 15f, 35f, false, token);
					}
				}
				else
				{
					await SmoothAlignToLadder(snapPos, 0.25f, false, token);
					_animationController.CrossFade(AnimationHashes.LadderEnterBottom, BLEND_TIME, AnimationController.BASE_LAYER_INDEX);
					float clipLen = _animationController.GetAnimationClipLength(AnimationHashes.LadderEnterBottom);
					await WaitForClimbEnter(clipLen, token);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private async UniTask PerformEnterTop(int animHash, float moveFrames, float climbFrames, bool invertRot = false, CancellationToken token = default)
		{
			_animationController.CrossFade(animHash, BLEND_TIME, AnimationController.BASE_LAYER_INDEX);
			float clipLen = _animationController.GetAnimationClipLength(animHash);

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

			await WaitForClimbEnter();
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
			_animationController.CrossFade(AnimationHashes.LadderIdle, BLEND_TIME, AnimationController.BASE_LAYER_INDEX);
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
				_animationController.CrossFade(AnimationHashes.LadderIdle, BLEND_TIME, AnimationController.BASE_LAYER_INDEX);
				_animationController.SetSpeed();
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
		}

		private async UniTask Step(int direction)
		{
			_stepCts?.Cancel();
			_stepCts?.Dispose();
			_stepCts = new CancellationTokenSource();
			
			var token = _stepCts.Token;
			
			try
			{
				int animHash = direction > 0 ? AnimationHashes.LadderAscend : AnimationHashes.LadderDescend;
				var speedFactor = _animationController.IsSprinting ? 1.5f : 1f;
				_animationController.SetSpeed(speedFactor);
				float clipLen = _animationController.GetAnimationClipLength(animHash) / speedFactor;

				_animationController.CrossFade(animHash, 0f, AnimationController.BASE_LAYER_INDEX);

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
			finally
			{
				_stepCts?.Dispose();
				_stepCts = null;
			}
		}

		private async UniTask PlayExitAndRelease(bool exitTop = true, CancellationToken token = default)
		{
			_climbingState = EClimbingState.Releasing;

			int animHash = exitTop ? AnimationHashes.LadderExitTop : AnimationHashes.LadderExitBottom;
			_animationController.CrossFade(animHash, BLEND_TIME, AnimationController.BASE_LAYER_INDEX);

			float clipLen = _animationController.GetAnimationClipLength(animHash);
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
				_character.RootMotionController.RootMovementUpdate(out var _, out var _);
				_character.EnableGroundConstraint(true);
				_character.SetRotationMode(_previousRotationMode);
				_animationController.SetSpeed();
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

		public void Disable()
		{
			if (!_enabled)
			{
				return;
			}

			_lifecycleCts?.Cancel();

			if (_character.IsClimbing())
			{
				_character.SetMovementMode(
					_character.IsGrounded() ? EMovementMode.Walking : EMovementMode.Falling);
			}

			_character.MovementModeChanged -= OnMovementModeChanged;
			_stepCts?.Cancel();
			_stepCts?.Dispose();
			_stepCts = null;
			_lifecycleCts?.Dispose();
			_lifecycleCts = null;
			_activeLadder = null;
			_climbingState = EClimbingState.None;
			_enabled = false;
		}
	}
}
