using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _App
{
    internal sealed class PlayerWeaponTransitionSession : ICombatActionObserver
    {
        private readonly IPlayerCombatAnimation _animation;
        private readonly CombatActionExecutor _executor;
        private readonly PlayerWeaponTransitionAction _transitionAction;
        private readonly ECharacterType _characterType;

        private Action<EWeaponType> _commitHandler;
        private EWeaponType _committedWeapon;
        private EWeaponType _pendingWeapon;
        private bool _commitApplied;

        public bool IsActive { get; private set; }

        public PlayerWeaponTransitionSession(
            IPlayerCombatAnimation animation,
            CombatActionExecutor executor,
            PlayerWeaponTransitionAction transitionAction,
            ECharacterType characterType)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _transitionAction = transitionAction ??
                throw new ArgumentNullException(nameof(transitionAction));
            _characterType = characterType;
        }

        public async UniTask<bool> ExecuteAsync(
            EWeaponType currentWeapon,
            EWeaponType targetWeapon,
            Action<EWeaponType> commitHandler,
            CancellationToken cancellationToken)
        {
            if (targetWeapon == EWeaponType.None)
            {
                throw new ArgumentOutOfRangeException(nameof(targetWeapon));
            }

            if (commitHandler == null)
            {
                throw new ArgumentNullException(nameof(commitHandler));
            }

            if (IsActive || _executor.IsRunning)
            {
                throw new InvalidOperationException("A weapon transition is already active.");
            }

            IsActive = true;
            _committedWeapon = currentWeapon;
            _commitHandler = commitHandler;
            try
            {
                if (targetWeapon == EWeaponType.Unarmed &&
                    currentWeapon == EWeaponType.Unarmed)
                {
                    return true;
                }

                if (targetWeapon == EWeaponType.Unarmed || currentWeapon == targetWeapon)
                {
                    return await RunStepAsync(
                        currentWeapon,
                        false,
                        EWeaponType.Unarmed,
                        cancellationToken);
                }

                if (currentWeapon != EWeaponType.Unarmed)
                {
                    bool sheathed = await RunStepAsync(
                        currentWeapon,
                        false,
                        EWeaponType.Unarmed,
                        cancellationToken);
                    if (!sheathed)
                    {
                        return false;
                    }
                }

                return await RunStepAsync(
                    targetWeapon,
                    true,
                    targetWeapon,
                    cancellationToken);
            }
            finally
            {
                EWeaponType committedWeapon = _committedWeapon;
                ClearOperation();
                // A cancelled draw restores the last weapon that crossed the
                // gameplay commit frame, not the visually requested loadout.
                _animation.ApplyLoadout(_characterType, committedWeapon);
            }
        }

        public void Abort()
        {
            if (IsActive)
            {
                _executor.Abort();
            }
        }

        CombatActionDirective ICombatActionObserver.Observe(in CombatActionFrame frame)
        {
            if (!IsActive || _commitApplied || !frame.CommitReached)
            {
                return CombatActionDirective.Continue;
            }

            EWeaponType previousWeapon = _committedWeapon;
            _commitApplied = true;
            _committedWeapon = _pendingWeapon;
            try
            {
                _commitHandler?.Invoke(_pendingWeapon);
            }
            catch
            {
                _committedWeapon = previousWeapon;
                _commitApplied = false;
                throw;
            }

            return CombatActionDirective.Continue;
        }

        private async UniTask<bool> RunStepAsync(
            EWeaponType animationWeapon,
            bool draw,
            EWeaponType resultingWeapon,
            CancellationToken cancellationToken)
        {
            if (draw)
            {
                _animation.ApplyLoadout(_characterType, animationWeapon);
            }

            if (!_animation.TryPlayWeaponTransition(
                    animationWeapon,
                    draw,
                    _transitionAction.Definition.DurationSeconds,
                    out _))
            {
                return false;
            }

            _pendingWeapon = resultingWeapon;
            _commitApplied = false;
            CombatActionRunResult result = await _executor.ExecuteAsync(
                _transitionAction.Definition,
                _transitionAction.Definition.DurationSeconds,
                null,
                this,
                cancellationToken);
            return result == CombatActionRunResult.Completed && _commitApplied;
        }

        private void ClearOperation()
        {
            IsActive = false;
            _commitHandler = null;
            _committedWeapon = EWeaponType.None;
            _pendingWeapon = EWeaponType.None;
            _commitApplied = false;
        }
    }
}
