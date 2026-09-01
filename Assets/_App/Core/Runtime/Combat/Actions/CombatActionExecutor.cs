using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    internal sealed class CombatActionExecutor
    {
        private readonly IDamageImmunityTarget _damageImmunity;
        private readonly CombatMotionController _motion;

        private CombatActionDefinition _definition;
        private CombatActionFrame _currentFrame;
        private ICombatActionObserver _observer;
        private UniTaskCompletionSource<CombatActionRunResult> _completion;
        private CancellationToken _cancellationToken;
        private IDisposable _damageImmunityLease;
        private float _duration;
        private float _elapsed;
        private int _nextExecutionId;
        private int _activeExecutionId;

        public bool IsRunning => _activeExecutionId != 0;
        public CombatActionFrame CurrentFrame => _currentFrame;
        public PlayerMovementIntent MovementIntent
        {
            get
            {
                if (!IsRunning)
                {
                    return PlayerMovementIntent.Unrestricted;
                }

                PlayerMovementIntent movement = _currentFrame.IsMovementConstrained
                    ? _definition.MovementIntent
                    : PlayerMovementIntent.Unrestricted;
                return new PlayerMovementIntent(
                    movement.TranslationScale,
                    _definition.MovementIntent.AllowJump && _currentFrame.CanCancel,
                    _definition.MovementIntent.AllowDodge && _currentFrame.CanCancel,
                    _definition.MovementIntent.AllowGuard && _currentFrame.CanCancel,
                    movement.FaceMoveDirection);
            }
        }

        public CombatActionExecutor(
            IDamageImmunityTarget damageImmunity,
            CombatMotionController motion)
        {
            _damageImmunity = damageImmunity ?? throw new ArgumentNullException(nameof(damageImmunity));
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        }

        public UniTask<CombatActionRunResult> ExecuteAsync(
            CombatActionDefinition definition,
            float duration,
            Transform previousConfirmedHitTarget,
            ICombatActionObserver observer,
            CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A combat action is already running.");
            }

            if (duration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            var completion = new UniTaskCompletionSource<CombatActionRunResult>();
            UniTask<CombatActionRunResult> task = completion.Task;
            _observer = observer;
            _completion = completion;
            _cancellationToken = cancellationToken;

            try
            {
                Begin(definition, duration, previousConfirmedHitTarget);
                Observe();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }

            return task;
        }

        public void Advance(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                Cancel();
                return;
            }

            AdvanceFrame(_activeExecutionId, Mathf.Max(0.0f, deltaTime));
        }

        public void Observe()
        {
            if (!IsRunning)
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                Cancel();
                return;
            }

            try
            {
                if (_observer != null &&
                    _observer.Observe(in _currentFrame) == CombatActionDirective.Interrupt)
                {
                    Complete(CombatActionRunResult.Interrupted);
                }
                else if (_currentFrame.IsComplete)
                {
                    Complete(CombatActionRunResult.Completed);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        public void Abort()
        {
            if (IsRunning)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    Cancel();
                }
                else
                {
                    Complete(CombatActionRunResult.Interrupted);
                }
            }
        }

        private int Begin(
            CombatActionDefinition definition,
            float duration,
            Transform previousConfirmedHitTarget)
        {
            _definition = definition;
            _duration = duration;
            _elapsed = 0.0f;

            int executionId = ++_nextExecutionId;
            if (executionId == 0)
            {
                executionId = ++_nextExecutionId;
            }

            _activeExecutionId = executionId;
            _currentFrame = CreateFrame(-1.0f, 0.0f);
            _motion.Begin(definition.Motion, duration, previousConfirmedHitTarget);
            ApplyDamageImmunity(_currentFrame.IsInvulnerable);
            return executionId;
        }

        private void AdvanceFrame(int executionId, float deltaTime)
        {
            if (_activeExecutionId != executionId)
            {
                return;
            }

            float previousTime = _currentFrame.NormalizedTime;
            _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
            float normalizedTime = Mathf.Clamp01(_elapsed / _duration);
            _currentFrame = CreateFrame(previousTime, normalizedTime);
            _motion.Advance(normalizedTime);
            ApplyDamageImmunity(_currentFrame.IsInvulnerable);
        }

        private CombatActionFrame CreateFrame(float previousTime, float normalizedTime)
        {
            bool hitboxCatchUpPulse =
                _definition.HitboxWindow.WasFullyCrossed(previousTime, normalizedTime);
            bool chainCatchUpPulse =
                _definition.ChainWindow.WasFullyCrossed(previousTime, normalizedTime);
            bool cancelCatchUpPulse =
                _definition.CancelWindow.WasFullyCrossed(previousTime, normalizedTime);
            return new CombatActionFrame(
                normalizedTime,
                _definition.HitboxWindow.Contains(normalizedTime) || hitboxCatchUpPulse,
                hitboxCatchUpPulse,
                _definition.ChainWindow.Contains(normalizedTime) || chainCatchUpPulse,
                _definition.CancelWindow.Contains(normalizedTime) || cancelCatchUpPulse,
                !_definition.MovementConstraintWindow.IsEnabled ||
                _definition.MovementConstraintWindow.Contains(normalizedTime),
                _definition.InvulnerabilityWindow.Contains(normalizedTime),
                normalizedTime >= _definition.RecoveryStart,
                _definition.HasCommit &&
                previousTime < _definition.CommitTime &&
                normalizedTime >= _definition.CommitTime,
                normalizedTime >= _definition.CompletionTime && !hitboxCatchUpPulse);
        }

        private void Finish(int executionId)
        {
            if (_activeExecutionId != executionId)
            {
                return;
            }

            try
            {
                ReleaseDamageImmunity();
                _motion.End();
            }
            finally
            {
                _definition = default;
                _currentFrame = default;
                _duration = 0.0f;
                _elapsed = 0.0f;
                _activeExecutionId = 0;
            }
        }

        private void Complete(CombatActionRunResult result)
        {
            UniTaskCompletionSource<CombatActionRunResult> completion = _completion;
            try
            {
                Finish(_activeExecutionId);
            }
            catch (Exception exception)
            {
                ClearOperation();
                completion?.TrySetException(exception);
                return;
            }

            ClearOperation();
            completion?.TrySetResult(result);
        }

        private void Cancel()
        {
            UniTaskCompletionSource<CombatActionRunResult> completion = _completion;
            CancellationToken cancellationToken = _cancellationToken;
            try
            {
                Finish(_activeExecutionId);
            }
            catch (Exception exception)
            {
                ClearOperation();
                completion?.TrySetException(exception);
                return;
            }

            ClearOperation();
            completion?.TrySetCanceled(cancellationToken);
        }

        private void Fail(Exception exception)
        {
            UniTaskCompletionSource<CombatActionRunResult> completion = _completion;
            try
            {
                if (IsRunning)
                {
                    Finish(_activeExecutionId);
                }
            }
            catch (Exception cleanupException)
            {
                exception = new AggregateException(exception, cleanupException);
            }

            ClearOperation();
            completion?.TrySetException(exception);
        }

        private void ClearOperation()
        {
            _observer = null;
            _completion = null;
            _cancellationToken = default;
        }

        private void ApplyDamageImmunity(bool immune)
        {
            if (immune)
            {
                _damageImmunityLease ??= _damageImmunity.AcquireDamageImmunity();
                return;
            }

            ReleaseDamageImmunity();
        }

        private void ReleaseDamageImmunity()
        {
            _damageImmunityLease?.Dispose();
            _damageImmunityLease = null;
        }
    }
}
