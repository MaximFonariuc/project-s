using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    internal enum CombatActionEndReason
    {
        None,
        Completed,
        Transitioned,
        Landed,
        Interrupted,
        Reaction,
        Cancelled,
        Disabled,
        Failed
    }

    internal readonly struct CombatActionSessionResult
    {
        public CombatActionRunResult RunResult { get; }
        public CombatActionEndReason EndReason { get; }
        public GameObject LastConfirmedTarget { get; }

        public CombatActionSessionResult(
            CombatActionRunResult runResult,
            CombatActionEndReason endReason,
            GameObject lastConfirmedTarget)
        {
            RunResult = runResult;
            EndReason = endReason;
            LastConfirmedTarget = lastConfirmedTarget;
        }
    }

    internal delegate CombatActionEndReason CombatActionFrameHandler(
        in CombatActionFrame frame);

    internal sealed class CombatActionSession : ICombatActionObserver
    {
        private readonly IPlayerCombatActor _actor;
        private readonly IPlayerWeaponRig _weaponRig;
        private readonly CombatActionExecutor _executor;
        private readonly CombatAttackExecution _attackExecution = new CombatAttackExecution();
        private readonly CombatActionOutcome _outcome = new CombatActionOutcome();

        private CombatActionFrameHandler _frameHandler;
        private EWeaponType _weaponType;
        private ulong _nextAttackExecutionId;
        private CombatActionEndReason _requestedEndReason;

        public event Action<bool> HitWindowChanged;

        public PlayerCombatAction Action { get; private set; }
        public CombatActionFrame CurrentFrame { get; private set; }
        public CombatActionOutcome Outcome => _outcome;
        public bool IsActive => Action != null;
        public bool IsHitWindowOpen { get; private set; }
        public CombatActionEndReason LastEndReason { get; private set; }

        public CombatActionSession(
            IPlayerCombatActor actor,
            IPlayerWeaponRig weaponRig,
            CombatActionExecutor executor)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _weaponRig = weaponRig ?? throw new ArgumentNullException(nameof(weaponRig));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public async UniTask<CombatActionSessionResult> ExecuteAsync(
            PlayerCombatAction action,
            EWeaponType weaponType,
            Transform previousConfirmedHitTarget,
            CombatActionFrameHandler frameHandler,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (frameHandler == null)
            {
                throw new ArgumentNullException(nameof(frameHandler));
            }

            if (IsActive || _executor.IsRunning)
            {
                throw new InvalidOperationException("A combat action session is already active.");
            }

            try
            {
                Begin(action, weaponType, frameHandler);
                CombatActionRunResult runResult = await _executor.ExecuteAsync(
                    action.Definition,
                    action.Definition.DurationSeconds,
                    previousConfirmedHitTarget,
                    this,
                    cancellationToken);
                CombatActionEndReason endReason = ResolveEndReason(runResult);
                LastEndReason = endReason;
                return new CombatActionSessionResult(
                    runResult,
                    endReason,
                    _outcome.LastConfirmedTarget);
            }
            catch (OperationCanceledException)
            {
                LastEndReason = _requestedEndReason == CombatActionEndReason.None
                    ? CombatActionEndReason.Cancelled
                    : _requestedEndReason;
                throw;
            }
            catch
            {
                LastEndReason = CombatActionEndReason.Failed;
                throw;
            }
            finally
            {
                End();
            }
        }

        public void Abort(CombatActionEndReason reason)
        {
            if (!IsActive)
            {
                return;
            }

            _requestedEndReason = reason == CombatActionEndReason.None
                ? CombatActionEndReason.Interrupted
                : reason;
            try
            {
                SetHitWindow(false);
            }
            finally
            {
                _executor.Abort();
                if (!_executor.IsRunning)
                {
                    End();
                }
            }
        }

        public bool TryRecord(in CombatHitResult hit)
        {
            return _outcome.TryRecord(in hit);
        }

        CombatActionDirective ICombatActionObserver.Observe(in CombatActionFrame frame)
        {
            CurrentFrame = frame;
            SetHitWindow(frame.IsHitboxOpen);

            CombatActionEndReason requestedReason = _frameHandler != null
                ? _frameHandler(in frame)
                : CombatActionEndReason.None;
            if (requestedReason == CombatActionEndReason.None)
            {
                return CombatActionDirective.Continue;
            }

            _requestedEndReason = requestedReason;
            return CombatActionDirective.Interrupt;
        }

        private void Begin(
            PlayerCombatAction action,
            EWeaponType weaponType,
            CombatActionFrameHandler frameHandler)
        {
            _nextAttackExecutionId++;
            if (_nextAttackExecutionId == 0)
            {
                _nextAttackExecutionId = 1;
            }

            Action = action;
            CurrentFrame = default;
            _weaponType = weaponType;
            _frameHandler = frameHandler;
            _requestedEndReason = CombatActionEndReason.None;
            LastEndReason = CombatActionEndReason.None;

            try
            {
                _attackExecution.Begin(
                    new AttackExecutionId(_nextAttackExecutionId),
                    action.Id,
                    action.Definition.HitDefinition,
                    _actor.Root.gameObject);
                _outcome.Begin(_attackExecution.Id, action.Id);
            }
            catch
            {
                End();
                throw;
            }
        }

        private void End()
        {
            if (!IsActive)
            {
                return;
            }

            try
            {
                SetHitWindow(false);
            }
            finally
            {
                try
                {
                    _weaponRig.CloseHitbox();
                }
                finally
                {
                    _attackExecution.End();
                    _outcome.End();
                    Action = null;
                    CurrentFrame = default;
                    _weaponType = EWeaponType.None;
                    _frameHandler = null;
                    _requestedEndReason = CombatActionEndReason.None;
                }
            }
        }

        private CombatActionEndReason ResolveEndReason(CombatActionRunResult runResult)
        {
            if (_requestedEndReason != CombatActionEndReason.None)
            {
                return _requestedEndReason;
            }

            return runResult == CombatActionRunResult.Completed
                ? CombatActionEndReason.Completed
                : CombatActionEndReason.Interrupted;
        }

        private void SetHitWindow(bool open)
        {
            if (IsHitWindowOpen == open)
            {
                return;
            }

            IsHitWindowOpen = open;
            if (open && _attackExecution.IsActive && _weaponType != EWeaponType.None)
            {
                _weaponRig.OpenHitbox(_attackExecution);
            }
            else
            {
                _weaponRig.CloseHitbox();
            }

            HitWindowChanged?.Invoke(open);
        }
    }
}
