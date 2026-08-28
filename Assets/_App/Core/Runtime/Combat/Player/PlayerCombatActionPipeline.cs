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
        private readonly PlayerCombatActionGraph _actionGraph;
        private readonly IStateMachine<PlayerCombatPhase> _stateMachine;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample>
            _bufferedTransitionPredicate;
        private readonly InputBufferEntryPredicate<PlayerCombatCommandSample>
            _bufferedEntryPredicate;
        private readonly CombatActionFrameHandler _combatFrameHandler;
        private PlayerCombatAction _acceptedBufferedAction;
        private EWeaponType _activeWeapon;

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
            PlayerCombatActionGraph actionGraph,
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
            _actionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            _stateMachine = stateMachine ??
                throw new ArgumentNullException(nameof(stateMachine));
            _characterType = characterType;
            _bufferedTransitionPredicate = CanAcceptBufferedTransition;
            _bufferedEntryPredicate = CanAcceptBufferedEntry;
            _combatFrameHandler = ObserveCombatFrame;
            _actionSession.HitWindowChanged += OnHitWindowChanged;
        }

        public bool TryAccept(
            PlayerCombatCommandId commandId,
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
                return _actionSession.Action != null &&
                       _inputBuffer.Store(
                           commandId,
                           _actionSession.Action.Id,
                           _actionExecutor.IsRunning
                               ? _actionExecutor.CurrentFrame.NormalizedTime
                               : _actionSession.CurrentFrame.NormalizedTime);
            }

            return phase == PlayerCombatPhase.Ready &&
                   _actionGraph.TryResolveEntry(
                       commandId,
                       GetEnvironment(),
                       out initialAction) &&
                   _availability.CanUseAction(initialAction, weapon);
        }

        public async UniTask ExecuteAsync(
            PlayerCombatAction initialAction,
            EWeaponType weapon,
            CancellationToken cancellationToken)
        {
            _activeWeapon = weapon;
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

                    _acceptedBufferedAction = null;
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
            finally
            {
                _acceptedBufferedAction = null;
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
            _acceptedBufferedAction = null;
            _activeWeapon = EWeaponType.None;
            SetActiveComboIndex(null);
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
                   _availability.CanUseAction(target, _activeWeapon);
        }

        private bool CanAcceptBufferedEntry(
            in InputBufferEntry<PlayerCombatCommandSample> entry)
        {
            return _availability.CanEnterAction &&
                   _actionGraph.TryResolveEntry(
                       entry.Action.CommandId,
                       GetEnvironment(),
                       out PlayerCombatAction target) &&
                   _availability.CanUseAction(target, _activeWeapon);
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
                _availability.CanUseAction(action, _activeWeapon))
            {
                return true;
            }

            action = null;
            return false;
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
