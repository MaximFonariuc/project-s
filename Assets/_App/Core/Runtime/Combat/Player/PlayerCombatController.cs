using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModularInput;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCombatController :
        IPlayerCombatCommands,
        IPlayerCombatState,
        IDisposable
    {
        private const int CommandInboxCapacity = 8;

        private readonly IPlayerInput _input;
        private readonly IPlayerCombatActor _actor;
        private readonly PlayerCombatAvailability _availability;
        private readonly IPlayerLocomotionEvents _locomotionEvents;
        private readonly IPlayerCombatAnimation _animation;
        private readonly IPlayerWeaponRig _weaponRig;
        private readonly PlayerCombatMovesetCatalog _movesets;
        private readonly ICombatHitEvents _outgoingHits;
        private readonly CombatActionExecutor _actionExecutor;
        private readonly PlayerCombatActionPipeline _actionPipeline;
        private readonly PlayerWeaponTransitionSession _weaponTransitionSession;
        private readonly PlayerCombatOperationLifetime _operationLifetime;
        private readonly IStateMachine<PlayerCombatPhase> _stateMachine;
        private readonly EWeaponType _preferredWeapon;
        private readonly ECharacterType _characterType;
        private readonly Action<EWeaponType> _weaponCommitHandler;
        private readonly PlayerCombatCommandInbox _commandInbox =
            new PlayerCombatCommandInbox(CommandInboxCapacity);
        private bool _enabled;
        private bool _disposed;

        public event Action<PlayerCombatPhase> PhaseChanged;
        public event Action<EWeaponType> WeaponChanged;
        public event Action<int?> ActiveComboIndexChanged;
        public event Action<bool> HitWindowChanged;

        public PlayerCombatPhase Phase =>
            _stateMachine.CurrentStateId ?? PlayerCombatPhase.Ready;
        public EWeaponType CurrentWeapon { get; private set; }
        public int? ActiveComboIndex => _actionPipeline.ActiveComboIndex;
        public bool IsHitWindowOpen => _actionPipeline.IsHitWindowOpen;

        public PlayerMovementIntent MovementIntent
        {
            get
            {
                PlayerMovementIntent intent = _actionExecutor.MovementIntent;
                if (!intent.FaceMoveDirection ||
                    _actor.RotationMode == ERotationMode.OrientRotationToMovement)
                {
                    return intent;
                }

                return new PlayerMovementIntent(
                    intent.TranslationScale,
                    intent.AllowJump,
                    intent.AllowDodge,
                    false);
            }
        }

        internal PlayerCombatController(
            IPlayerInput input,
            IPlayerCombatActor actor,
            PlayerCombatAvailability availability,
            IPlayerLocomotionEvents locomotionEvents,
            IPlayerCombatAnimation animation,
            IPlayerWeaponRig weaponRig,
            PlayerCombatMovesetCatalog movesets,
            ICombatHitEvents outgoingHits,
            CombatActionExecutor actionExecutor,
            PlayerCombatActionPipeline actionPipeline,
            PlayerWeaponTransitionSession weaponTransitionSession,
            PlayerCombatOperationLifetime operationLifetime,
            IStateMachine<PlayerCombatPhase> stateMachine,
            ECharacterType characterType,
            EWeaponType startingWeapon)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _locomotionEvents = locomotionEvents ?? throw new ArgumentNullException(nameof(locomotionEvents));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _weaponRig = weaponRig ?? throw new ArgumentNullException(nameof(weaponRig));
            _movesets = movesets ?? throw new ArgumentNullException(nameof(movesets));
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
            _actionPipeline = actionPipeline ??
                throw new ArgumentNullException(nameof(actionPipeline));
            _weaponTransitionSession = weaponTransitionSession ??
                throw new ArgumentNullException(nameof(weaponTransitionSession));
            _operationLifetime = operationLifetime ??
                throw new ArgumentNullException(nameof(operationLifetime));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _preferredWeapon = startingWeapon == EWeaponType.None
                ? EWeaponType.Unarmed
                : startingWeapon;
            CurrentWeapon = _preferredWeapon;
            _characterType = characterType;
            _weaponCommitHandler = CommitWeapon;
            _actionPipeline.ActiveComboIndexChanged += OnActiveComboIndexChanged;
            _actionPipeline.HitWindowChanged += OnHitWindowChanged;
        }

        public void Enable()
        {
            ThrowIfDisposed();
            if (_enabled)
            {
                return;
            }

            _enabled = true;
            try
            {
                _animation.ApplyLoadout(_characterType, CurrentWeapon);
                CommitWeapon(CurrentWeapon);

                _stateMachine.StateChanged += OnStateChanged;
                if (!_stateMachine.IsRunning)
                {
                    _stateMachine.Start<PlayerCombatReadyState>();
                }

                _input.CombatActionRequested += OnCombatActionRequested;
                _input.SwitchCombatStateRequested += OnSwitchWeaponRequested;
                _locomotionEvents.JumpAccepted += OnJumpAccepted;
                _locomotionEvents.DodgeAccepted += OnDodgeAccepted;
                _outgoingHits.HitResolved += OnOutgoingHitResolved;
            }
            catch
            {
                Disable();
                throw;
            }
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.CombatActionRequested -= OnCombatActionRequested;
            _input.SwitchCombatStateRequested -= OnSwitchWeaponRequested;
            _locomotionEvents.JumpAccepted -= OnJumpAccepted;
            _locomotionEvents.DodgeAccepted -= OnDodgeAccepted;
            _outgoingHits.HitResolved -= OnOutgoingHitResolved;
            _stateMachine.StateChanged -= OnStateChanged;
            _enabled = false;

            CancelActiveAction(CombatActionEndReason.Disabled);
            _commandInbox.Clear();
            _actionPipeline.Reset();
            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }
        }

        public bool TryExecute(PlayerCombatCommandId commandId)
        {
            if (!_enabled)
            {
                return false;
            }

            PlayerCombatCommandRequest request =
                _actionPipeline.CaptureCommand(commandId);
            return _commandInbox.TryEnqueue(in request);
        }

        public void Tick(float scaledDeltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            _actionExecutor.Advance(scaledDeltaTime);
            ConsumeQueuedCommands();

            if (_commandInbox.ConsumeWeaponSwitchRequest())
            {
                ToggleWeaponAsync(_preferredWeapon).Forget();
            }

            _actionExecutor.Observe();
        }

        private bool TryExecuteNow(in PlayerCombatCommandRequest request)
        {
            if (!_actionPipeline.TryAccept(
                    in request,
                    Phase,
                    CurrentWeapon,
                    out PlayerCombatAction action))
            {
                return false;
            }

            if (action == null)
            {
                return true;
            }

            CancellationTokenSource cancellation = BeginAction();
            RunActionPipelineAsync(action, cancellation).Forget();
            return true;
        }

        private void ConsumeQueuedCommands()
        {
            while (_commandInbox.TryDequeue(out PlayerCombatCommandRequest request))
            {
                TryExecuteNow(in request);
            }
        }

        public async UniTask<bool> ToggleWeaponAsync(EWeaponType targetWeapon)
        {
            if (!_enabled || !_availability.CanSwitchWeapon ||
                targetWeapon == EWeaponType.None ||
                !_movesets.Supports(targetWeapon) ||
                !_weaponRig.CanEquip(targetWeapon) ||
                Phase != PlayerCombatPhase.Ready)
            {
                return false;
            }

            if (targetWeapon == EWeaponType.Unarmed && CurrentWeapon == EWeaponType.Unarmed)
            {
                return true;
            }

            CancellationTokenSource cancellation = BeginAction();
            _stateMachine.ChangeState<PlayerCombatWeaponSwitchState, EWeaponType>(targetWeapon);

            try
            {
                return await _weaponTransitionSession.ExecuteAsync(
                    CurrentWeapon,
                    targetWeapon,
                    _weaponCommitHandler,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _actor.Root);
                return false;
            }
            finally
            {
                CompleteAction(cancellation);
            }
        }

        public void InterruptAttack()
        {
            InterruptAttack(CombatActionEndReason.Interrupted);
        }

        internal void InterruptAttack(CombatActionEndReason reason)
        {
            PlayerCombatPhase phase = Phase;
            if (phase == PlayerCombatPhase.Ready)
            {
                return;
            }

            CancelActiveAction(reason);
            _commandInbox.Clear();
            _actionPipeline.Reset();
            if (phase is PlayerCombatPhase.Executing or PlayerCombatPhase.Recovery)
            {
                _stateMachine.ChangeState<PlayerCombatInterruptedState>();
            }

            ReturnToReady();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disable();
            _actionPipeline.ActiveComboIndexChanged -= OnActiveComboIndexChanged;
            _actionPipeline.HitWindowChanged -= OnHitWindowChanged;
            PhaseChanged = null;
            WeaponChanged = null;
            ActiveComboIndexChanged = null;
            HitWindowChanged = null;
            _disposed = true;
        }

        private async UniTaskVoid RunActionPipelineAsync(
            PlayerCombatAction initialAction,
            CancellationTokenSource cancellation)
        {
            try
            {
                await _actionPipeline.ExecuteAsync(
                    initialAction,
                    CurrentWeapon,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _actor.Root);
            }
            finally
            {
                CompleteAction(cancellation);
            }
        }

        private CancellationTokenSource BeginAction()
        {
            return _operationLifetime.Begin();
        }

        private void CompleteAction(CancellationTokenSource cancellation)
        {
            if (!_operationLifetime.TryComplete(cancellation))
            {
                return;
            }

            _actionPipeline.Reset();
            _animation.Release();
            ReturnToReady();
        }

        private void CancelActiveAction(CombatActionEndReason reason)
        {
            _operationLifetime.Cancel(reason);
        }

        private void ReturnToReady()
        {
            if (_stateMachine.IsRunning && Phase != PlayerCombatPhase.Ready)
            {
                _stateMachine.ChangeState<PlayerCombatReadyState>();
            }
        }

        private void CommitWeapon(EWeaponType weaponType)
        {
            _weaponRig.ApplyWeapon(weaponType);
            if (CurrentWeapon == weaponType)
            {
                return;
            }

            CurrentWeapon = weaponType;
            SafeEvent.Publish(WeaponChanged, weaponType, _actor.Root);
        }

        private void OnStateChanged(PlayerCombatPhase previous, PlayerCombatPhase current)
        {
            SafeEvent.Publish(PhaseChanged, current, _actor.Root);
        }

        private void OnCombatActionRequested(PlayerCombatCommandId commandId)
        {
            TryExecute(commandId);
        }

        private void OnSwitchWeaponRequested()
        {
            _commandInbox.RequestWeaponSwitch();
        }

        private void OnDodgeAccepted()
        {
            InterruptAttack();
        }

        private void OnJumpAccepted()
        {
            InterruptAttack();
        }

        private void OnOutgoingHitResolved(CombatHitResult hit)
        {
            _actionPipeline.TryRecord(in hit);
        }

        private void OnActiveComboIndexChanged(int? comboIndex)
        {
            SafeEvent.Publish(ActiveComboIndexChanged, comboIndex, _actor.Root);
        }

        private void OnHitWindowChanged(bool open)
        {
            SafeEvent.Publish(HitWindowChanged, open, _actor.Root);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerCombatController));
            }
        }

    }
}
