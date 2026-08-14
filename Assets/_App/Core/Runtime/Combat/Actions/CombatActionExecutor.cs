using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    internal sealed class CombatActionExecutor
    {
        private readonly ICombatClock _clock;
        private readonly IRootMotionPolicyTarget _body;
        private readonly IDamageImmunityTarget _damageImmunity;
        private readonly CombatMotionController _motion;

        private CombatActionDefinition _definition;
        private CombatActionFrame _currentFrame;
        private RootMotionPolicy _previousRootMotionPolicy;
        private bool _previousDamageImmunity;
        private float _duration;
        private float _elapsed;
        private int _nextExecutionId;
        private int _activeExecutionId;

        public bool IsRunning => _activeExecutionId != 0;
        public PlayerMovementIntent MovementIntent =>
            IsRunning && _currentFrame.IsMovementConstrained
                ? _definition.MovementIntent
                : PlayerMovementIntent.Unrestricted;

        public CombatActionExecutor(
            ICombatClock clock,
            IRootMotionPolicyTarget body,
            IDamageImmunityTarget damageImmunity,
            CombatMotionController motion)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _damageImmunity = damageImmunity ?? throw new ArgumentNullException(nameof(damageImmunity));
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        }

        public async UniTask<CombatActionRunResult> ExecuteAsync(
            CombatActionDefinition definition,
            float duration,
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

            int executionId = Begin(definition, duration);
            try
            {
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

                    await UniTask.Yield(cancellationToken);
                    Advance(executionId, Mathf.Max(0.0f, _clock.DeltaTime));
                }
            }
            finally
            {
                Finish(executionId);
            }
        }

        public void Abort()
        {
            if (IsRunning)
            {
                Finish(_activeExecutionId);
            }
        }

        private int Begin(CombatActionDefinition definition, float duration)
        {
            _definition = definition;
            _duration = duration;
            _elapsed = 0.0f;
            _previousRootMotionPolicy = _body.RootMotionPolicy;
            _previousDamageImmunity = _damageImmunity.IsDamageImmune;
            _body.SetRootMotionPolicy(definition.RootMotionPolicy);

            int executionId = ++_nextExecutionId;
            if (executionId == 0)
            {
                executionId = ++_nextExecutionId;
            }

            _activeExecutionId = executionId;
            _currentFrame = CreateFrame(-1.0f, 0.0f);
            _motion.Begin(definition.Motion, duration);
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

            _body.SetRootMotionPolicy(_previousRootMotionPolicy);
            _damageImmunity.SetDamageImmune(_previousDamageImmunity);
            _motion.End();
            _definition = default;
            _currentFrame = default;
            _duration = 0.0f;
            _elapsed = 0.0f;
            _activeExecutionId = 0;
        }

        private void ApplyDamageImmunity(bool immune)
        {
            bool expected = _previousDamageImmunity || immune;
            if (_damageImmunity.IsDamageImmune != expected)
            {
                _damageImmunity.SetDamageImmune(expected);
            }
        }
    }
}
