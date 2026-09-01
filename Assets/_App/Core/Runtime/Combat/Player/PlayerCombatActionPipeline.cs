using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModularInput;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerCombatActionPipeline : IDisposable
    {
        private readonly IPlayerCombatActor _actor;
        private readonly PlayerCombatAvailability _availability;
        private readonly IPlayerCombatAnimation _animation;
        private readonly CombatInputBuffer _inputBuffer;
        private readonly CombatActionExecutor _actionExecutor;
        private readonly CombatActionSession _actionSession;
        private readonly PlayerCombatMovesetCatalog _movesets;
        private readonly IStateMachine<PlayerCombatPhase> _stateMachine;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample>
            _bufferedTransitionPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample>
            _bufferedEntryPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample>
            _bufferedReadyEntryPredicate;
        private readonly CombatActionFrameHandler _combatFrameHandler;
        private PlayerCombatAction _acceptedBufferedAction;
        private PlayerCombatCommandId _acceptedBufferedCommandId;
        private PlayerCombatActionGraph _activeActionGraph;
        private PlayerCombatActionGraph _readyEntryGraph;
        private EWeaponType _activeWeapon;
        private EWeaponType _readyEntryWeapon;

        public event Action<int?> ActiveComboIndexChanged;
        public event Action<bool> HitWindowChanged;

        public int? ActiveComboIndex { get; private set; }
        public bool IsHitWindowOpen => _actionSession.IsHitWindowOpen;

        public PlayerCombatActionPipeline(
            IPlayerCombatActor actor,
            PlayerCombatAvailability availability,
            IPlayerCombatAnimation animation,
            CombatInputBuffer inputBuffer,
            CombatActionExecutor actionExecutor,
            CombatActionSession actionSession,
            PlayerCombatMovesetCatalog movesets,
            IStateMachine<PlayerCombatPhase> stateMachine,
            ECharacterType characterType)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _availability = availability ??
                throw new ArgumentNullException(nameof(availability));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _actionExecutor = actionExecutor ??
                throw new ArgumentNullException(nameof(actionExecutor));
            _actionSession = actionSession ??
                throw new ArgumentNullException(nameof(actionSession));
            _movesets = movesets ?? throw new ArgumentNullException(nameof(movesets));
            _stateMachine = stateMachine ??
                throw new ArgumentNullException(nameof(stateMachine));
            _characterType = characterType;
            _bufferedTransitionPredicate = CanAcceptBufferedTransition;
            _bufferedEntryPredicate = CanAcceptBufferedEntry;
            _bufferedReadyEntryPredicate = CanAcceptBufferedReadyEntry;
            _combatFrameHandler = ObserveCombatFrame;
            _actionSession.HitWindowChanged += OnHitWindowChanged;
        }

        public PlayerCombatCommandRequest CaptureCommand(
            PlayerCombatCommandId commandId)
        {
            PlayerCombatAction sourceAction = _actionSession.Action;
            if (sourceAction == null)
            {
                return PlayerCombatCommandRequest.Unbound(commandId);
            }

            float sourceNormalizedTime = _actionExecutor.IsRunning
                ? _actionExecutor.CurrentFrame.NormalizedTime
                : _actionSession.CurrentFrame.NormalizedTime;
            return PlayerCombatCommandRequest.Bound(
                commandId,
                sourceAction.Id,
                sourceNormalizedTime);
        }

        public bool TryAccept(
            in PlayerCombatCommandRequest request,
            PlayerCombatPhase phase,
            EWeaponType weapon,
            out PlayerCombatAction initialAction)
        {
            initialAction = null;
            if (!_availability.CanEnterAction || phase == PlayerCombatPhase.WeaponSwitch)
            {
                return false;
            }

            if (phase is PlayerCombatPhase.Executing or PlayerCombatPhase.Recovery)
            {
                return TryStoreDuringActiveAction(in request);
            }

            return phase == PlayerCombatPhase.Ready &&
                   _movesets.TryGet(weapon, out PlayerCombatMoveset moveset) &&
                   moveset.ActionGraph.TryResolveEntry(
                       request.CommandId,
                       GetEnvironment(),
                       out initialAction) &&
                   _availability.CanUseAction(initialAction, weapon);
        }

        public bool TryBufferReadyEntry(
            in PlayerCombatCommandRequest request,
            PlayerCombatPhase phase,
            EWeaponType weapon)
        {
            if (phase != PlayerCombatPhase.Ready || request.HasSourceAction ||
                !_availability.CanBufferEntry ||
                !_movesets.TryGet(weapon, out PlayerCombatMoveset moveset) ||
                !moveset.ActionGraph.TryResolveEntry(
                    request.CommandId,
                    GetEnvironment(),
                    out _))
            {
                return false;
            }

            return _inputBuffer.StoreEntry(request.CommandId);
        }

        public bool TryConsumeBufferedReadyEntry(
            PlayerCombatPhase phase,
            EWeaponType weapon,
            out PlayerCombatAction action)
        {
            action = null;
            if (phase != PlayerCombatPhase.Ready || !_availability.CanEnterAction ||
                !_movesets.TryGet(weapon, out PlayerCombatMoveset moveset))
            {
                return false;
            }

            _readyEntryGraph = moveset.ActionGraph;
            _readyEntryWeapon = weapon;
            try
            {
                if (!_inputBuffer.TryConsume(
                        _bufferedReadyEntryPredicate,
                        out PlayerCombatCommandSample command) ||
                    !_readyEntryGraph.TryResolveEntry(
                        command.CommandId,
                        GetEnvironment(),
                        out action) ||
                    !_availability.CanUseAction(action, weapon))
                {
                    action = null;
                    return false;
                }

                // A Ready entry commits one intentional command. Remaining
                // samples were authored before this action became available.
                _inputBuffer.Clear();
                return true;
            }
            finally
            {
                _readyEntryGraph = null;
                _readyEntryWeapon = EWeaponType.None;
            }
        }

        public async UniTask ExecuteAsync(
            PlayerCombatAction initialAction,
            EWeaponType weapon,
            CancellationToken cancellationToken)
        {
            PlayerCombatActionGraph actionGraph = _movesets.Require(weapon).ActionGraph;
            _activeWeapon = weapon;
            _activeActionGraph = actionGraph;
            try
            {
                PlayerCombatAction action = initialAction;
                Transform previousConfirmedHitTarget = null;
                while (action != null && _availability.CanUseAction(action, weapon))
                {
                    if (!_animation.TryPlayAction(
                            _characterType,
                            weapon,
                            action,
                            out _))
                    {
                        break;
                    }

                    // The action is committed once its presentation has started.
                    // Recording cooldown after the session would let dodge, jump,
                    // reaction or disable interruptions bypass the action cost.
                    _availability.RecordUse(action, weapon);
                    ClearAcceptedTransition();
                    EnterExecuting(action);
                    SetActiveComboIndex(
                        action.Presentation.Kind is
                            PlayerCombatPresentationKind.ComboStep or
                            PlayerCombatPresentationKind.AirComboStep
                            ? action.Presentation.ComboIndex
                            : null);

                    CombatActionSessionResult sessionResult =
                        await _actionSession.ExecuteAsync(
                            action,
                            weapon,
                            previousConfirmedHitTarget,
                            _combatFrameHandler,
                            cancellationToken);

                    if (sessionResult.EndReason == CombatActionEndReason.Landed)
                    {
                        previousConfirmedHitTarget = null;
                        if (TryResolveLandingEntry(out action))
                        {
                            continue;
                        }

                        _animation.Cancel();
                        break;
                    }

                    if (sessionResult.EndReason == CombatActionEndReason.Parried)
                    {
                        // A parry invalidates every successor authored against the
                        // interrupted source action. Fresh input may be buffered
                        // later while the independent recoil state is active.
                        ClearAcceptedTransition();
                        _inputBuffer.Clear();
                        break;
                    }

                    PlayerCombatAction acceptedAction = _acceptedBufferedAction;
                    ClearAcceptedTransition();
                    Transform nextMotionTarget = ResolveNextMotionTarget(
                        acceptedAction,
                        previousConfirmedHitTarget,
                        sessionResult.PrimaryConfirmedTarget);
                    action = acceptedAction;
                    previousConfirmedHitTarget = nextMotionTarget;
                    if (action == null)
                    {
                        TryConsumeBufferedEntry(out action);
                        previousConfirmedHitTarget = null;
                    }
                }
            }
            finally
            {
                ClearAcceptedTransition();
                _activeActionGraph = null;
                _activeWeapon = EWeaponType.None;
            }
        }

        public void TryRecord(in CombatHitResult hit)
        {
            _actionSession.TryRecord(in hit);
        }

        public void Reset()
        {
            _inputBuffer.Clear();
            ClearAcceptedTransition();
            _activeActionGraph = null;
            _activeWeapon = EWeaponType.None;
            SetActiveComboIndex(null);
        }

        public void ClearBufferedInput()
        {
            _inputBuffer.Clear();
        }

        public void Dispose()
        {
            _actionSession.HitWindowChanged -= OnHitWindowChanged;
            ActiveComboIndexChanged = null;
            HitWindowChanged = null;
        }

        private CombatActionEndReason ObserveCombatFrame(in CombatActionFrame frame)
        {
            if (ShouldInterruptAirborneActionOnLanding())
            {
                return CombatActionEndReason.Landed;
            }

            // A transition accepted during a catch-up hitbox pulse is committed
            // on the next frame. This leaves the previous action's hitbox active
            // through LateUpdate so its swept query can sample the skipped window.
            if (_acceptedBufferedAction != null)
            {
                // Inputs received during the one-frame catch-up deferral still
                // belong to the old source action and cannot cross the edge.
                _inputBuffer.Clear();
                return CombatActionEndReason.Transitioned;
            }

            if (frame.IsInRecovery && Phase == PlayerCombatPhase.Executing)
            {
                _stateMachine.ChangeState<PlayerCombatRecoveryState, PlayerCombatAction>(
                    _actionSession.Action);
            }

            if (!_inputBuffer.TryConsume(
                    _bufferedTransitionPredicate,
                    out PlayerCombatCommandSample command) ||
                !ActiveActionGraph.TryResolveTransition(
                    _actionSession.Action,
                    in command,
                    GetEnvironment(),
                    in frame,
                    _actionSession.Outcome,
                    out _acceptedBufferedAction))
            {
                return CombatActionEndReason.None;
            }

            // One source action can commit exactly one successor. Any remaining
            // samples were authored against the old action and must not leak
            // through the next node as a fresh entry.
            _inputBuffer.Clear();
            _acceptedBufferedCommandId = command.CommandId;

            return frame.IsHitboxCatchUpPulse
                ? CombatActionEndReason.None
                : CombatActionEndReason.Transitioned;
        }

        private bool CanAcceptBufferedTransition(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            PlayerCombatCommandSample command = entry.Action;
            CombatActionFrame currentFrame = _actionSession.CurrentFrame;
            return command.HasSourceAction &&
                   _availability.CanEnterAction &&
                   ActiveActionGraph.TryResolveTransition(
                       _actionSession.Action,
                       in command,
                       GetEnvironment(),
                       in currentFrame,
                       _actionSession.Outcome,
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, _activeWeapon);
        }

        private bool CanAcceptBufferedEntry(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            PlayerCombatCommandSample command = entry.Action;
            return command.HasSourceAction &&
                   // A command captured for an OnHit/OnWhiff edge keeps that
                   // meaning until its source action ends. Letting it degrade
                   // into a normal entry would turn a failed confirm into a
                   // delayed standalone attack.
                   !ActiveActionGraph.HasConditionalTransition(
                       command.SourceActionId,
                       command.CommandId) &&
                   _availability.CanEnterAction &&
                   ActiveActionGraph.TryResolveEntry(
                       command.CommandId,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, _activeWeapon);
        }

        private bool CanAcceptBufferedReadyEntry(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            return !entry.Action.HasSourceAction &&
                   _readyEntryGraph != null &&
                   _readyEntryGraph.TryResolveEntry(
                       entry.Action.CommandId,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, _readyEntryWeapon);
        }

        private bool TryConsumeBufferedEntry(out PlayerCombatAction action)
        {
            if (_availability.CanEnterAction &&
                _inputBuffer.TryConsume(
                    _bufferedEntryPredicate,
                    out PlayerCombatCommandSample command) &&
                ActiveActionGraph.TryResolveEntry(
                    command.CommandId,
                    GetEnvironment(),
                    out action) &&
                _availability.CanUseAction(action, _activeWeapon))
            {
                // Completion handoff consumes one intentional successor. Other
                // samples still carry the completed source-action context.
                _inputBuffer.Clear();
                return true;
            }

            action = null;
            return false;
        }

        private bool TryResolveLandingEntry(out PlayerCombatAction action)
        {
            if (_acceptedBufferedAction == null)
            {
                return TryConsumeBufferedEntry(out action);
            }

            PlayerCombatCommandId commandId = _acceptedBufferedCommandId;
            ClearAcceptedTransition();
            if (_availability.CanEnterAction &&
                ActiveActionGraph.TryResolveEntry(
                    commandId,
                    GetEnvironment(),
                    out action) &&
                _availability.CanUseAction(action, _activeWeapon))
            {
                return true;
            }

            action = null;
            return false;
        }

        private void ClearAcceptedTransition()
        {
            _acceptedBufferedAction = null;
            _acceptedBufferedCommandId = default;
        }

        private bool TryStoreDuringActiveAction(
            in PlayerCombatCommandRequest request)
        {
            PlayerCombatAction activeAction = _actionSession.Action;
            if (activeAction == null)
            {
                return false;
            }

            PlayerCombatActionId sourceActionId;
            float sourceNormalizedTime;
            if (request.HasSourceAction)
            {
                if (request.SourceActionId != activeAction.Id)
                {
                    return false;
                }

                sourceActionId = request.SourceActionId;
                sourceNormalizedTime = request.SourceNormalizedTime;
            }
            else
            {
                // Multiple commands may arrive while Ready and be consumed in
                // one gameplay tick. Bind successors to the action started by
                // the first command without inventing a stale source context.
                sourceActionId = activeAction.Id;
                sourceNormalizedTime = _actionExecutor.IsRunning
                    ? _actionExecutor.CurrentFrame.NormalizedTime
                    : _actionSession.CurrentFrame.NormalizedTime;
            }

            return _inputBuffer.Store(
                request.CommandId,
                sourceActionId,
                sourceNormalizedTime);
        }

        private static Transform ResolveNextMotionTarget(
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

        private PlayerCombatPhase Phase =>
            _stateMachine.CurrentStateId ?? PlayerCombatPhase.Ready;

        private PlayerCombatActionGraph ActiveActionGraph =>
            _activeActionGraph ?? throw new InvalidOperationException(
                "Combat action graph is not active.");

        private void EnterExecuting(PlayerCombatAction action)
        {
            _stateMachine.ChangeState<PlayerCombatExecutingState, PlayerCombatAction>(action);
        }

        private void SetActiveComboIndex(int? comboIndex)
        {
            if (ActiveComboIndex == comboIndex)
            {
                return;
            }

            ActiveComboIndex = comboIndex;
            ActiveComboIndexChanged?.Invoke(comboIndex);
        }

        private void OnHitWindowChanged(bool open)
        {
            HitWindowChanged?.Invoke(open);
        }
    }
}
