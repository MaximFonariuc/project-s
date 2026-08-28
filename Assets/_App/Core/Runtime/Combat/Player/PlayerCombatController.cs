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
        IDisposable
    {
        private const int PendingCommandCapacity = 8;

        private readonly IPlayerInput _input;
        private readonly IPlayerCombatActor _actor;
        private readonly ICharacterReactionState _reactionState;
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
        private readonly ICombatClock _clock;
        private readonly EWeaponType _preferredWeapon;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample> _bufferedTransitionPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample> _bufferedEntryPredicate;
        private readonly CombatActionFrameHandler _combatFrameHandler;
        private readonly Action<EWeaponType> _weaponCommitHandler;
        private readonly Dictionary<PlayerCombatActionId, float> _lastActionUseTime =
            new Dictionary<PlayerCombatActionId, float>();
        private readonly Queue<PlayerCombatCommandId> _pendingCommands =
            new Queue<PlayerCombatCommandId>(PendingCommandCapacity);
        private CancellationTokenSource _actionCancellation;
        private PlayerCombatAction _acceptedBufferedAction;
        private bool _weaponSwitchRequested;
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
            ICharacterReactionState reactionState,
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
            ICombatClock clock,
            ECharacterType characterType,
            EWeaponType startingWeapon)
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
            _actionSession = actionSession ?? throw new ArgumentNullException(nameof(actionSession));
            _weaponTransitionSession = weaponTransitionSession ??
                throw new ArgumentNullException(nameof(weaponTransitionSession));
            _actionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
            _pendingCommands.Clear();
            _weaponSwitchRequested = false;
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
            if (!_enabled || string.IsNullOrEmpty(commandId.Value) ||
                _pendingCommands.Count >= PendingCommandCapacity)
            {
                return false;
            }

            _pendingCommands.Enqueue(commandId);
            return true;
        }

        public void Tick(float scaledDeltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            _actionExecutor.Advance(scaledDeltaTime);
            ConsumePendingCommands();

            bool switchWeapon = _weaponSwitchRequested;
            _weaponSwitchRequested = false;
            if (switchWeapon)
            {
                ToggleWeaponAsync(_preferredWeapon).Forget();
            }

            _actionExecutor.Observe();
        }

        private bool TryExecuteNow(PlayerCombatCommandId commandId)
        {
            if (!CanEnterCombatAction() || Phase == PlayerCombatPhase.WeaponSwitch)
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
                !CanUseAction(action))
            {
                return false;
            }

            CancellationTokenSource cancellation = BeginAction();
            RunActionPipelineAsync(action, cancellation).Forget();
            return true;
        }

        private void ConsumePendingCommands()
        {
            while (_pendingCommands.Count > 0)
            {
                TryExecuteNow(_pendingCommands.Dequeue());
            }
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
            _pendingCommands.Clear();
            _weaponSwitchRequested = false;
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
                while (action != null && CanUseAction(action))
                {
                    if (!TryPlayAction(action, out _))
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

                    if (action.Definition.Cooldown > 0.0f)
                    {
                        _lastActionUseTime[action.Id] = _clock.ScaledTime;
                    }

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
            return CanEnterCombatAction() &&
                   _actionGraph.TryResolveTransition(
                       _actionSession.Action,
                       in command,
                       GetEnvironment(),
                       in currentFrame,
                       _actionSession.Outcome,
                       out PlayerCombatAction target) &&
                   CanUseAction(target);
        }

        private bool CanAcceptBufferedEntry(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            return CanEnterCombatAction() &&
                   _actionGraph.TryResolveEntry(
                       entry.Action.CommandId,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   CanUseAction(target);
        }

        private bool TryConsumeBufferedEntry(out PlayerCombatAction action)
        {
            if (CanEnterCombatAction() &&
                _inputBuffer.TryConsume(
                    _bufferedEntryPredicate,
                    out PlayerCombatCommandSample command) &&
                _actionGraph.TryResolveEntry(
                    command.CommandId,
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
            _weaponSwitchRequested = true;
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

        private static bool FailPlayback(out AnimationActionPlayback playback)
        {
            playback = default;
            return false;
        }
    }
}
