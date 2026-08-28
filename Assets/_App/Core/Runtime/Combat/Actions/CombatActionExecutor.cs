using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    internal sealed class CombatActionExecutor
    {
        private readonly ICombatClock _clock;
        private readonly IDamageImmunityTarget _damageImmunity;
        private readonly CombatMotionController _motion;

        private CombatActionDefinition _definition;
        private CombatActionFrame _currentFrame;
        private IDisposable _damageImmunityLease;
        private float _duration;
        private float _elapsed;
        private int _nextExecutionId;
        private int _activeExecutionId;

        public bool IsRunning => _activeExecutionId != 0;
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
                    movement.FaceMoveDirection);
            }
        }

        public CombatActionExecutor(
            ICombatClock clock,
            IDamageImmunityTarget damageImmunity,
            CombatMotionController motion)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _damageImmunity = damageImmunity ?? throw new ArgumentNullException(nameof(damageImmunity));
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        }

        public async UniTask<CombatActionRunResult> ExecuteAsync(
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

            int executionId = 0;
            try
            {
                executionId = Begin(definition, duration, previousConfirmedHitTarget);
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (observer != null &&
                        observer.Observe(in _currentFrame) == CombatActionDirective.Interrupt)
                    {
                        return CombatActionRunResult.Interrupted;
                    }

                    if (_currentFrame.IsComplete)
                    {
                        return CombatActionRunResult.Completed;
                    }

                    // Yield can resume again in the same rendered frame. Since the
                    // executor advances by Time.deltaTime, that would count one
                    // frame more than once and desynchronize gameplay from clips.
                    await UniTask.NextFrame(cancellationToken);
                    Advance(executionId, Mathf.Max(0.0f, _clock.DeltaTime));
                }
            }
            finally
            {
                if (executionId != 0)
                {
                    Finish(executionId);
                }
                else
                {
                    // Begin marks the executor active before it configures the
                    // motion adapter. Roll back that partial initialization if
                    // any adapter rejects the action during startup.
                    Abort();
                }
            }
        }

        public void Abort()
        {
            if (IsRunning)
            {
                Finish(_activeExecutionId);
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

        private void Advance(int executionId, float deltaTime)
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
            return new CombatActionFrame(
                normalizedTime,
                _definition.HitboxWindow.Contains(normalizedTime),
                _definition.ChainWindow.Contains(normalizedTime),
                _definition.CancelWindow.Contains(normalizedTime),
                !_definition.MovementConstraintWindow.IsEnabled ||
                _definition.MovementConstraintWindow.Contains(normalizedTime),
                _definition.InvulnerabilityWindow.Contains(normalizedTime),
                normalizedTime >= _definition.RecoveryStart,
                _definition.HasCommit &&
                previousTime < _definition.CommitTime &&
                normalizedTime >= _definition.CommitTime,
                normalizedTime >= _definition.CompletionTime);
        }

        private void Finish(int executionId)
        {
            if (_activeExecutionId != executionId)
            {
                return;
            }

            ReleaseDamageImmunity();
            _motion.End();
            _definition = default;
            _currentFrame = default;
            _duration = 0.0f;
            _elapsed = 0.0f;
            _activeExecutionId = 0;
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
