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
        private readonly ICombatHitEvents _outgoingHits;
        private readonly CombatInputBuffer _inputBuffer;
        private readonly CombatActionExecutor _actionExecutor;
        private readonly CombatActionSession _actionSession;
        private readonly PlayerWeaponTransitionSession _weaponTransitionSession;
        private readonly PlayerCombatActionGraph _actionGraph;
        private readonly IStateMachine<PlayerCombatPhase> _stateMachine;
        private readonly EWeaponType _preferredWeapon;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample> _bufferedTransitionPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample> _bufferedEntryPredicate;
        private readonly CombatActionFrameHandler _combatFrameHandler;
        private readonly Action<EWeaponType> _weaponCommitHandler;
        private readonly PlayerCombatCommandInbox _commandInbox =
            new PlayerCombatCommandInbox(CommandInboxCapacity);
        private CancellationTokenSource _actionCancellation;
        private PlayerCombatAction _acceptedBufferedAction;
        private bool _enabled;

        public event Action<PlayerCombatPhase> PhaseChanged;
        public event Action<EWeaponType> WeaponChanged;
        public event Action<int> ComboIndexChanged;
        public event Action<bool> HitWindowChanged;

        public PlayerCombatPhase Phase =>
            _stateMachine.CurrentStateId ?? PlayerCombatPhase.Ready;
        public EWeaponType CurrentWeapon { get; private set; }
        public int ComboIndex { get; private set; }
        public bool IsHitWindowOpen => _actionSession.IsHitWindowOpen;

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
            ICombatHitEvents outgoingHits,
            CombatInputBuffer inputBuffer,
            CombatActionExecutor actionExecutor,
            CombatActionSession actionSession,
            PlayerWeaponTransitionSession weaponTransitionSession,
            PlayerCombatActionGraph actionGraph,
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
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
            _actionSession = actionSession ?? throw new ArgumentNullException(nameof(actionSession));
            _weaponTransitionSession = weaponTransitionSession ??
                throw new ArgumentNullException(nameof(weaponTransitionSession));
            _actionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _preferredWeapon = startingWeapon == EWeaponType.None
                ? EWeaponType.Unarmed
                : startingWeapon;
            CurrentWeapon = _preferredWeapon;
            _characterType = characterType;
            _bufferedTransitionPredicate = CanAcceptBufferedTransition;
            _bufferedEntryPredicate = CanAcceptBufferedEntry;
            _combatFrameHandler = ObserveCombatFrame;
            _weaponCommitHandler = CommitWeapon;
            _actionSession.HitWindowChanged += OnHitWindowChanged;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

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
            _enabled = true;
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
            _inputBuffer.Clear();
            _acceptedBufferedAction = null;
            SetComboIndex(0);
            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }
        }

        public bool TryExecute(PlayerCombatCommandId commandId)
        {
            return _enabled && _commandInbox.TryEnqueue(commandId);
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

        private bool TryExecuteNow(PlayerCombatCommandId commandId)
        {
            if (!_availability.CanEnterAction || Phase == PlayerCombatPhase.WeaponSwitch)
            {
                return false;
            }

            if (Phase is PlayerCombatPhase.Executing or PlayerCombatPhase.Recovery)
            {
                return _actionSession.Action != null &&
                       _inputBuffer.Store(
                           commandId,
                           _actionSession.Action.Id,
                           _actionExecutor.IsRunning
                               ? _actionExecutor.CurrentFrame.NormalizedTime
                               : _actionSession.CurrentFrame.NormalizedTime);
            }

            if (Phase != PlayerCombatPhase.Ready ||
                !_actionGraph.TryResolveEntry(
                    commandId,
                    GetEnvironment(),
                    out PlayerCombatAction action) ||
                !_availability.CanUseAction(action, CurrentWeapon))
            {
                return false;
            }

            CancellationTokenSource cancellation = BeginAction();
            RunActionPipelineAsync(action, cancellation).Forget();
            return true;
        }

        private void ConsumeQueuedCommands()
        {
            while (_commandInbox.TryDequeue(out PlayerCombatCommandId commandId))
            {
                TryExecuteNow(commandId);
            }
        }

        public async UniTask<bool> ToggleWeaponAsync(EWeaponType targetWeapon)
        {
            if (!_enabled || !_availability.CanSwitchWeapon ||
                targetWeapon == EWeaponType.None ||
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
            _inputBuffer.Clear();
            _acceptedBufferedAction = null;
            SetComboIndex(0);
            if (phase is PlayerCombatPhase.Executing or PlayerCombatPhase.Recovery)
            {
                _stateMachine.ChangeState<PlayerCombatInterruptedState>();
            }

            ReturnToReady();
        }

        public void Dispose()
        {
            Disable();
            _actionSession.HitWindowChanged -= OnHitWindowChanged;
            PhaseChanged = null;
            WeaponChanged = null;
            ComboIndexChanged = null;
            HitWindowChanged = null;
        }

        private async UniTaskVoid RunActionPipelineAsync(
            PlayerCombatAction initialAction,
            CancellationTokenSource cancellation)
        {
            try
            {
                PlayerCombatAction action = initialAction;
                Transform previousConfirmedHitTarget = null;
                while (action != null &&
                       _availability.CanUseAction(action, CurrentWeapon))
                {
                    if (!_animation.TryPlayAction(
                            _characterType,
                            CurrentWeapon,
                            action,
                            out _))
                    {
                        break;
                    }

                    _acceptedBufferedAction = null;
                    EnterExecuting(action);
                    SetComboIndex(
                        action.Presentation.Kind is PlayerCombatPresentationKind.ComboStep or
                            PlayerCombatPresentationKind.AirComboStep
                            ? action.Presentation.ComboIndex
                            : 0);

                    CombatActionSessionResult sessionResult = await _actionSession.ExecuteAsync(
                        action,
                        CurrentWeapon,
                        previousConfirmedHitTarget,
                        _combatFrameHandler,
                        cancellation.Token);

                    if (sessionResult.EndReason == CombatActionEndReason.Landed)
                    {
                        _animation.Cancel();
                        break;
                    }

                    _availability.RecordUse(action);

                    PlayerCombatAction acceptedAction = _acceptedBufferedAction;
                    Transform nextMotionTarget = ResolveNextMotionTarget(
                        acceptedAction,
                        previousConfirmedHitTarget,
                        sessionResult.LastConfirmedTarget);
                    action = acceptedAction;
                    previousConfirmedHitTarget = nextMotionTarget;
                    if (action == null)
                    {
                        TryConsumeBufferedEntry(out action);
                        previousConfirmedHitTarget = null;
                    }
                }
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

        private CombatActionEndReason ObserveCombatFrame(in CombatActionFrame frame)
        {
            if (ShouldInterruptAirborneActionOnLanding())
            {
                return CombatActionEndReason.Landed;
            }

            if (frame.IsInRecovery && Phase == PlayerCombatPhase.Executing)
            {
                _stateMachine.ChangeState<PlayerCombatRecoveryState, PlayerCombatAction>(
                    _actionSession.Action);
            }

            if (!_inputBuffer.TryConsume(
                    _bufferedTransitionPredicate,
                    out PlayerCombatCommandSample command) ||
                !_actionGraph.TryResolveTransition(
                    _actionSession.Action,
                    in command,
                    GetEnvironment(),
                    in frame,
                    _actionSession.Outcome,
                    out _acceptedBufferedAction))
            {
                return CombatActionEndReason.None;
            }

            return CombatActionEndReason.Transitioned;
        }

        private bool CanAcceptBufferedTransition(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            PlayerCombatCommandSample command = entry.Action;
            CombatActionFrame currentFrame = _actionSession.CurrentFrame;
            return _availability.CanEnterAction &&
                   _actionGraph.TryResolveTransition(
                       _actionSession.Action,
                       in command,
                       GetEnvironment(),
                       in currentFrame,
                       _actionSession.Outcome,
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, CurrentWeapon);
        }

        private bool CanAcceptBufferedEntry(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            return _availability.CanEnterAction &&
                   _actionGraph.TryResolveEntry(
                       entry.Action.CommandId,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, CurrentWeapon);
        }

        private bool TryConsumeBufferedEntry(out PlayerCombatAction action)
        {
            if (_availability.CanEnterAction &&
                _inputBuffer.TryConsume(
                    _bufferedEntryPredicate,
                    out PlayerCombatCommandSample command) &&
                _actionGraph.TryResolveEntry(
                    command.CommandId,
                    GetEnvironment(),
                    out action) &&
                _availability.CanUseAction(action, CurrentWeapon))
            {
                return true;
            }

            action = null;
            return false;
        }

        private Transform ResolveNextMotionTarget(
            PlayerCombatAction nextAction,
            Transform carriedTarget,
            GameObject lastConfirmedTarget)
        {
            if (nextAction == null ||
                !nextAction.Definition.Motion.UsesPreviousConfirmedHitTarget)
            {
                return null;
            }

            return lastConfirmedTarget != null
                ? lastConfirmedTarget.transform
                : carriedTarget;
        }

        private PlayerCombatEnvironment GetEnvironment()
        {
            return _actor.IsFalling
                ? PlayerCombatEnvironment.Airborne
                : PlayerCombatEnvironment.Grounded;
        }

        private bool ShouldInterruptAirborneActionOnLanding()
        {
            return _actionSession.Action != null &&
                   _actionSession.Action.Supports(PlayerCombatEnvironment.Airborne) &&
                   !_actionSession.Action.Supports(PlayerCombatEnvironment.Grounded) &&
                   GetEnvironment() == PlayerCombatEnvironment.Grounded;
        }

        private CancellationTokenSource BeginAction()
        {
            CancelActiveAction(CombatActionEndReason.Cancelled);
            _actionCancellation = new CancellationTokenSource();
            return _actionCancellation;
        }

        private void CompleteAction(CancellationTokenSource cancellation)
        {
            if (!ReferenceEquals(_actionCancellation, cancellation))
            {
                return;
            }

            _actionCancellation = null;
            cancellation.Dispose();
            _inputBuffer.Clear();
            _acceptedBufferedAction = null;
            SetComboIndex(0);
            _animation.Release();
            ReturnToReady();
        }

        private void CancelActiveAction(CombatActionEndReason reason)
        {
            _animation.Cancel();
            CancellationTokenSource cancellation = _actionCancellation;
            if (cancellation == null)
            {
                _actionSession.Abort(reason);
                _weaponTransitionSession.Abort();
                return;
            }

            _actionCancellation = null;
            cancellation.Cancel();
            _actionSession.Abort(reason);
            _weaponTransitionSession.Abort();
            cancellation.Dispose();
        }

        private void EnterExecuting(PlayerCombatAction action)
        {
            _stateMachine.ChangeState<PlayerCombatExecutingState, PlayerCombatAction>(action);
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
            WeaponChanged?.Invoke(weaponType);
        }

        private void SetComboIndex(int comboIndex)
        {
            if (ComboIndex == comboIndex)
            {
                return;
            }

            ComboIndex = comboIndex;
            ComboIndexChanged?.Invoke(comboIndex);
        }

        private void OnStateChanged(PlayerCombatPhase previous, PlayerCombatPhase current)
        {
            PhaseChanged?.Invoke(current);
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
            _actionSession.TryRecord(in hit);
        }

        private void OnHitWindowChanged(bool open)
        {
            HitWindowChanged?.Invoke(open);
        }

    }
}
