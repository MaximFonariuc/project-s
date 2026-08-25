using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModularAnimation;
using ModularInput;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCombatController :
        IPlayerCombatCommands,
        IPlayerCombatState,
        ICombatActionObserver,
        IDisposable
    {
        private enum ActionObservationMode
        {
            None,
            CombatAction,
            WeaponTransition
        }

        private readonly IPlayerInput _input;
        private readonly IPlayerCombatActor _actor;
        private readonly ICharacterReactionState _reactionState;
        private readonly IPlayerLocomotionEvents _locomotionEvents;
        private readonly IPlayerCombatAnimation _animation;
        private readonly IPlayerWeaponRig _weaponRig;
        private readonly ICombatHitEvents _outgoingHits;
        private readonly CombatInputBuffer _inputBuffer;
        private readonly CombatActionExecutor _actionExecutor;
        private readonly PlayerCombatActionGraph _actionGraph;
        private readonly PlayerWeaponTransitionAction _weaponTransitionAction;
        private readonly IStateMachine<PlayerCombatPhase> _stateMachine;
        private readonly ICombatClock _clock;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandId> _bufferedTransitionPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandId> _bufferedEntryPredicate;
        private readonly Dictionary<PlayerCombatActionId, float> _lastActionUseTime =
            new Dictionary<PlayerCombatActionId, float>();
        private readonly CombatAttackExecution _attackExecution = new CombatAttackExecution();
        private readonly CombatActionOutcome _actionOutcome = new CombatActionOutcome();

        private CancellationTokenSource _actionCancellation;
        private ulong _nextAttackExecutionId;
        private ActionObservationMode _observationMode;
        private CombatActionFrame _observedFrame;
        private PlayerCombatAction _currentAction;
        private PlayerCombatAction _acceptedBufferedAction;
        private EWeaponType _pendingWeapon;
        private bool _weaponApplied;
        private bool _enabled;

        public event Action<PlayerCombatPhase> PhaseChanged;
        public event Action<EWeaponType> WeaponChanged;
        public event Action<int> ComboIndexChanged;
        public event Action<bool> HitWindowChanged;

        public PlayerCombatPhase Phase =>
            _stateMachine.CurrentStateId ?? PlayerCombatPhase.Ready;
        public EWeaponType CurrentWeapon { get; private set; } = EWeaponType.Unarmed;
        public int ComboIndex { get; private set; }
        public bool IsHitWindowOpen { get; private set; }

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
            ICharacterReactionState reactionState,
            IPlayerLocomotionEvents locomotionEvents,
            IPlayerCombatAnimation animation,
            IPlayerWeaponRig weaponRig,
            ICombatHitEvents outgoingHits,
            CombatInputBuffer inputBuffer,
            CombatActionExecutor actionExecutor,
            PlayerCombatActionGraph actionGraph,
            PlayerWeaponTransitionAction weaponTransitionAction,
            IStateMachine<PlayerCombatPhase> stateMachine,
            ICombatClock clock,
            ECharacterType characterType)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _reactionState = reactionState ?? throw new ArgumentNullException(nameof(reactionState));
            _locomotionEvents = locomotionEvents ?? throw new ArgumentNullException(nameof(locomotionEvents));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _weaponRig = weaponRig ?? throw new ArgumentNullException(nameof(weaponRig));
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
            _actionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            _weaponTransitionAction = weaponTransitionAction ??
                throw new ArgumentNullException(nameof(weaponTransitionAction));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _characterType = characterType;
            _bufferedTransitionPredicate = CanAcceptBufferedTransition;
            _bufferedEntryPredicate = CanAcceptBufferedEntry;
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
            _locomotionEvents.DodgeAccepted -= OnDodgeAccepted;
            _outgoingHits.HitResolved -= OnOutgoingHitResolved;
            _stateMachine.StateChanged -= OnStateChanged;
            _enabled = false;

            CancelActiveAction();
            _inputBuffer.Clear();
            _currentAction = null;
            _acceptedBufferedAction = null;
            SetHitWindow(false);
            SetComboIndex(0);
            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }
        }

        public bool TryExecute(PlayerCombatCommandId commandId)
        {
            if (!_enabled || string.IsNullOrEmpty(commandId.Value) ||
                !CanEnterCombatAction() ||
                Phase == PlayerCombatPhase.WeaponSwitch)
            {
                return false;
            }

            if (Phase is PlayerCombatPhase.Executing or PlayerCombatPhase.Recovery)
            {
                return _inputBuffer.Store(commandId);
            }

            if (Phase != PlayerCombatPhase.Ready ||
                !_actionGraph.TryResolveEntry(
                    commandId,
                    GetEnvironment(),
                    out PlayerCombatAction action) ||
                !CanUseAction(action))
            {
                return false;
            }

            CancellationTokenSource cancellation = BeginAction();
            RunActionPipelineAsync(action, cancellation).Forget();
            return true;
        }

        public async UniTask<bool> ToggleWeaponAsync(EWeaponType targetWeapon)
        {
            if (!_enabled || _reactionState.BlocksActions || targetWeapon == EWeaponType.None ||
                Phase != PlayerCombatPhase.Ready ||
                _actor.IsFalling)
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
                if (targetWeapon == EWeaponType.Unarmed)
                {
                    return await RunWeaponTransitionAsync(
                        CurrentWeapon,
                        false,
                        EWeaponType.Unarmed,
                        cancellation.Token);
                }

                if (CurrentWeapon == targetWeapon)
                {
                    return await RunWeaponTransitionAsync(
                        CurrentWeapon,
                        false,
                        EWeaponType.Unarmed,
                        cancellation.Token);
                }

                if (CurrentWeapon != EWeaponType.Unarmed)
                {
                    bool sheathed = await RunWeaponTransitionAsync(
                        CurrentWeapon,
                        false,
                        EWeaponType.Unarmed,
                        cancellation.Token);
                    if (!sheathed)
                    {
                        return false;
                    }
                }

                return await RunWeaponTransitionAsync(
                    targetWeapon,
                    true,
                    targetWeapon,
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
            PlayerCombatPhase phase = Phase;
            if (phase == PlayerCombatPhase.Ready)
            {
                return;
            }

            CancelActiveAction();
            _inputBuffer.Clear();
            _currentAction = null;
            _acceptedBufferedAction = null;
            SetHitWindow(false);
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
                while (action != null && CanUseAction(action))
                {
                    if (!TryPlayAction(action, out _))
                    {
                        break;
                    }

                    _currentAction = action;
                    _acceptedBufferedAction = null;
                    BeginAttackExecution(action);
                    EnterExecuting(action);
                    SetComboIndex(
                        action.Presentation.Kind is PlayerCombatPresentationKind.ComboStep or
                            PlayerCombatPresentationKind.AirComboStep
                            ? action.Presentation.ComboIndex
                            : 0);

                    await ExecuteObservedActionAsync(
                        action.Definition,
                        action.Definition.DurationSeconds,
                        ActionObservationMode.CombatAction,
                        cancellation.Token);

                    if (action.Definition.Cooldown > 0.0f)
                    {
                        _lastActionUseTime[action.Id] = _clock.ScaledTime;
                    }

                    SetHitWindow(false);
                    EndAttackExecution();
                    action = _acceptedBufferedAction;
                    if (action == null)
                    {
                        TryConsumeBufferedEntry(out action);
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

        private bool TryPlayAction(
            PlayerCombatAction action,
            out AnimationActionPlayback playback)
        {
            PlayerCombatPresentation presentation = action.Presentation;
            return presentation.Kind switch
            {
                PlayerCombatPresentationKind.ComboStep =>
                    _animation.TryPlayComboStep(
                        presentation.ComboIndex,
                        action.Definition.DurationSeconds,
                        out playback),
                PlayerCombatPresentationKind.AirComboStep =>
                    _animation.TryPlayAirComboStep(
                        presentation.ComboIndex,
                        action.Definition.DurationSeconds,
                        out playback),
                PlayerCombatPresentationKind.NamedAnimation =>
                    _animation.TryPlayNamedAnimation(
                        _characterType,
                        CurrentWeapon,
                        presentation.AnimationId,
                        action.Definition.DurationSeconds,
                        out playback),
                _ => FailPlayback(out playback)
            };
        }

        private async UniTask<bool> RunWeaponTransitionAsync(
            EWeaponType animationWeapon,
            bool draw,
            EWeaponType resultingWeapon,
            CancellationToken token)
        {
            // Draw belongs to the incoming loadout, while sheathe belongs to the
            // currently equipped one. The weapon itself is still committed only
            // at the gameplay-owned commit point below.
            if (draw)
            {
                _animation.ApplyLoadout(_characterType, animationWeapon);
            }

            try
            {
                if (!_animation.TryPlayWeaponTransition(
                        animationWeapon,
                        draw,
                        out AnimationActionPlayback playback))
                {
                    return false;
                }

                _pendingWeapon = resultingWeapon;
                _weaponApplied = false;
                CombatActionRunResult result = await ExecuteObservedActionAsync(
                    _weaponTransitionAction.Definition,
                    _weaponTransitionAction.Definition.DurationSeconds,
                    ActionObservationMode.WeaponTransition,
                    token);

                return result == CombatActionRunResult.Completed && _weaponApplied;
            }
            finally
            {
                // Keep the source loadout for the full sheathe clip. On cancel,
                // restore whichever weapon actually reached its commit point.
                _animation.ApplyLoadout(_characterType, CurrentWeapon);
            }
        }

        private async UniTask<CombatActionRunResult> ExecuteObservedActionAsync(
            CombatActionDefinition definition,
            float duration,
            ActionObservationMode observationMode,
            CancellationToken token)
        {
            _observationMode = observationMode;
            try
            {
                return await _actionExecutor.ExecuteAsync(definition, duration, this, token);
            }
            finally
            {
                _observationMode = ActionObservationMode.None;
            }
        }

        CombatActionDirective ICombatActionObserver.Observe(in CombatActionFrame frame)
        {
            SetHitWindow(frame.IsHitboxOpen);
            switch (_observationMode)
            {
                case ActionObservationMode.CombatAction:
                    if (frame.IsInRecovery && Phase == PlayerCombatPhase.Executing)
                    {
                        _stateMachine.ChangeState<PlayerCombatRecoveryState, PlayerCombatAction>(
                            _currentAction);
                    }

                    _observedFrame = frame;
                    if (!_inputBuffer.TryConsume(
                            _bufferedTransitionPredicate,
                            out PlayerCombatCommandId commandId) ||
                        !_actionGraph.TryResolveTransition(
                            _currentAction,
                            commandId,
                            GetEnvironment(),
                            in _observedFrame,
                            _actionOutcome,
                            out _acceptedBufferedAction))
                    {
                        return CombatActionDirective.Continue;
                    }

                    return CombatActionDirective.Interrupt;

                case ActionObservationMode.WeaponTransition:
                    if (frame.CommitReached)
                    {
                        CommitWeapon(_pendingWeapon);
                        _weaponApplied = true;
                    }
                    break;
            }

            return CombatActionDirective.Continue;
        }

        private bool CanAcceptBufferedTransition(
            in InputBufferEntry<PlayerCombatCommandId> entry)
        {
            return CanEnterCombatAction() &&
                   _actionGraph.TryResolveTransition(
                       _currentAction,
                       entry.Action,
                       GetEnvironment(),
                       in _observedFrame,
                       _actionOutcome,
                       out PlayerCombatAction target) &&
                   CanUseAction(target);
        }

        private bool CanAcceptBufferedEntry(
            in InputBufferEntry<PlayerCombatCommandId> entry)
        {
            return CanEnterCombatAction() &&
                   _actionGraph.TryResolveEntry(
                       entry.Action,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   CanUseAction(target);
        }

        private bool TryConsumeBufferedEntry(out PlayerCombatAction action)
        {
            if (CanEnterCombatAction() &&
                _inputBuffer.TryConsume(
                    _bufferedEntryPredicate,
                    out PlayerCombatCommandId commandId) &&
                _actionGraph.TryResolveEntry(
                    commandId,
                    GetEnvironment(),
                    out action) &&
                CanUseAction(action))
            {
                return true;
            }

            action = null;
            return false;
        }

        private bool CanEnterCombatAction()
        {
            return !_reactionState.BlocksActions && _actor.CanAttack;
        }

        private bool CanUseAction(PlayerCombatAction action)
        {
            if (action == null ||
                (_lastActionUseTime.TryGetValue(action.Id, out float lastUseTime) &&
                 _clock.ScaledTime - lastUseTime < action.Definition.Cooldown))
            {
                return false;
            }

            PlayerCombatPresentation presentation = action.Presentation;
            return presentation.Kind switch
            {
                PlayerCombatPresentationKind.ComboStep =>
                    presentation.ComboIndex < _animation.ComboCount,
                PlayerCombatPresentationKind.AirComboStep =>
                    presentation.ComboIndex < _animation.AirComboCount,
                PlayerCombatPresentationKind.NamedAnimation =>
                    _animation.HasNamedAnimation(
                        _characterType,
                        CurrentWeapon,
                        presentation.AnimationId),
                _ => false
            };
        }

        private PlayerCombatEnvironment GetEnvironment()
        {
            return _actor.IsFalling
                ? PlayerCombatEnvironment.Airborne
                : PlayerCombatEnvironment.Grounded;
        }

        private CancellationTokenSource BeginAction()
        {
            CancelActiveAction();
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
            _currentAction = null;
            _acceptedBufferedAction = null;
            SetHitWindow(false);
            EndAttackExecution();
            SetComboIndex(0);
            _animation.Release();
            ReturnToReady();
        }

        private void CancelActiveAction()
        {
            EndAttackExecution();
            _animation.Cancel();
            CancellationTokenSource cancellation = _actionCancellation;
            if (cancellation == null)
            {
                return;
            }

            _actionCancellation = null;
            cancellation.Cancel();
            _actionExecutor.Abort();
            cancellation.Dispose();
        }

        private void BeginAttackExecution(PlayerCombatAction action)
        {
            EndAttackExecution();
            _nextAttackExecutionId++;
            if (_nextAttackExecutionId == 0)
            {
                _nextAttackExecutionId = 1;
            }

            _attackExecution.Begin(
                new AttackExecutionId(_nextAttackExecutionId),
                action.Id,
                action.Definition.HitDefinition,
                _actor.Root.gameObject);
            _actionOutcome.Begin(_attackExecution.Id, action.Id);
        }

        private void EndAttackExecution()
        {
            _weaponRig.CloseHitbox();
            _attackExecution.End();
            _actionOutcome.End();
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

        private void SetHitWindow(bool open)
        {
            if (IsHitWindowOpen == open)
            {
                return;
            }

            IsHitWindowOpen = open;
            if (open && _attackExecution.IsActive &&
                CurrentWeapon != EWeaponType.None)
            {
                _weaponRig.OpenHitbox(_attackExecution);
            }
            else
            {
                _weaponRig.CloseHitbox();
            }

            HitWindowChanged?.Invoke(open);
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
            ToggleWeaponAsync(EWeaponType.TwoHanded).Forget();
        }

        private void OnDodgeAccepted()
        {
            InterruptAttack();
        }

        private void OnOutgoingHitResolved(CombatHitResult hit)
        {
            _actionOutcome.TryRecord(in hit);
        }

        private static bool FailPlayback(out AnimationActionPlayback playback)
        {
            playback = default;
            return false;
        }
    }
}
