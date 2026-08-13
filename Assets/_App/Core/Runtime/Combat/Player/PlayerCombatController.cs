using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModularAnimation;
using ModularInput;
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
            Combo,
            Skill,
            WeaponTransition
        }

        private readonly IPlayerInput _input;
        private readonly IPlayerCombatActor _actor;
        private readonly IPlayerLocomotionEvents _locomotionEvents;
        private readonly IPlayerCombatAnimation _animation;
        private readonly IPlayerWeaponRig _weaponRig;
        private readonly CombatInputBuffer _inputBuffer;
        private readonly CombatActionExecutor _actionExecutor;
        private readonly PlayerCombatActionCatalog _actionCatalog;
        private readonly ICombatClock _clock;
        private readonly ECharacterType _characterType;
        private readonly InputBufferEntryPredicate<ESkillType> _bufferedActionPredicate;
        private readonly Dictionary<ESkillType, float> _lastSkillUseTime =
            new Dictionary<ESkillType, float>();

        private CancellationTokenSource _actionCancellation;
        private ActionObservationMode _observationMode;
        private ESkillType? _acceptedBufferedAction;
        private EWeaponType _pendingWeapon;
        private bool _weaponApplied;
        private bool _canAcceptDefaultBufferedAction;
        private bool _canAcceptSkillBufferedAction;
        private bool _enabled;

        public event Action<PlayerCombatPhase> PhaseChanged;
        public event Action<EWeaponType> WeaponChanged;
        public event Action<int> ComboIndexChanged;
        public event Action<bool> HitWindowChanged;

        public PlayerCombatPhase Phase { get; private set; } = PlayerCombatPhase.Ready;
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
            IPlayerLocomotionEvents locomotionEvents,
            IPlayerCombatAnimation animation,
            IPlayerWeaponRig weaponRig,
            CombatInputBuffer inputBuffer,
            CombatActionExecutor actionExecutor,
            PlayerCombatActionCatalog actionCatalog,
            ICombatClock clock,
            ECharacterType characterType)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _locomotionEvents = locomotionEvents ?? throw new ArgumentNullException(nameof(locomotionEvents));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _weaponRig = weaponRig ?? throw new ArgumentNullException(nameof(weaponRig));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
            _actionCatalog = actionCatalog ?? throw new ArgumentNullException(nameof(actionCatalog));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _characterType = characterType;
            _bufferedActionPredicate = CanAcceptBufferedAction;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _animation.ApplyLoadout(_characterType, CurrentWeapon);
            _weaponRig.ApplyWeapon(CurrentWeapon);

            _input.AttackRequested += OnAttackRequested;
            _input.SkillRequested += OnSkillRequested;
            _input.SwitchCombatStateRequested += OnSwitchWeaponRequested;
            _locomotionEvents.DodgeAccepted += OnDodgeAccepted;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.AttackRequested -= OnAttackRequested;
            _input.SkillRequested -= OnSkillRequested;
            _input.SwitchCombatStateRequested -= OnSwitchWeaponRequested;
            _locomotionEvents.DodgeAccepted -= OnDodgeAccepted;
            _enabled = false;

            CancelActiveAction();
            _inputBuffer.Clear();
            SetHitWindow(false);
            SetComboIndex(0);
            SetPhase(PlayerCombatPhase.Ready);
        }

        public bool TryAttack(ESkillType skillType = ESkillType.Default)
        {
            if (!_enabled || !_actor.CanAttack || Phase == PlayerCombatPhase.WeaponSwitch)
            {
                return false;
            }

            if (Phase is PlayerCombatPhase.ComboAttack or PlayerCombatPhase.SkillAttack)
            {
                return _inputBuffer.Store(skillType);
            }

            if (Phase != PlayerCombatPhase.Ready ||
                (skillType != ESkillType.Default && !CanUseSkill(skillType)))
            {
                return false;
            }

            CancellationTokenSource cancellation = BeginAction();
            RunAttackPipelineAsync(skillType, cancellation).Forget();
            return true;
        }

        public async UniTask<bool> ToggleWeaponAsync(EWeaponType targetWeapon)
        {
            if (!_enabled || targetWeapon == EWeaponType.None || Phase != PlayerCombatPhase.Ready ||
                _actor.IsFalling)
            {
                return false;
            }

            if (targetWeapon == EWeaponType.Unarmed && CurrentWeapon == EWeaponType.Unarmed)
            {
                return true;
            }

            CancellationTokenSource cancellation = BeginAction();
            SetPhase(PlayerCombatPhase.WeaponSwitch);

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
            if (Phase is not PlayerCombatPhase.ComboAttack and not PlayerCombatPhase.SkillAttack)
            {
                return;
            }

            CancelActiveAction();
            _inputBuffer.Clear();
            SetHitWindow(false);
            SetComboIndex(0);
            SetPhase(PlayerCombatPhase.Ready);
        }

        public void Dispose()
        {
            Disable();
            PhaseChanged = null;
            WeaponChanged = null;
            ComboIndexChanged = null;
            HitWindowChanged = null;
        }

        private async UniTaskVoid RunAttackPipelineAsync(
            ESkillType initialAction,
            CancellationTokenSource cancellation)
        {
            try
            {
                ESkillType? nextAction = initialAction;
                while (nextAction.HasValue)
                {
                    nextAction = nextAction.Value == ESkillType.Default
                        ? await RunComboAsync(cancellation.Token)
                        : await RunSkillAsync(nextAction.Value, cancellation.Token);
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

        private async UniTask<ESkillType?> RunComboAsync(CancellationToken token)
        {
            if (_animation.ComboCount == 0)
            {
                return null;
            }

            SetPhase(PlayerCombatPhase.ComboAttack);
            SetComboIndex(0);
            _inputBuffer.Clear();

            while (ComboIndex < _animation.ComboCount)
            {
                if (!_animation.TryPlayComboStep(ComboIndex, out AnimationPlayback playback))
                {
                    return null;
                }

                CombatActionDefinition definition = _actionCatalog.GetCombo(ComboIndex);
                _acceptedBufferedAction = null;
                await ExecuteObservedActionAsync(
                    definition,
                    playback.Duration,
                    ActionObservationMode.Combo,
                    token);

                SetHitWindow(false);
                if (!_acceptedBufferedAction.HasValue)
                {
                    break;
                }

                if (_acceptedBufferedAction.Value != ESkillType.Default)
                {
                    return _acceptedBufferedAction.Value;
                }

                SetComboIndex(ComboIndex + 1);
            }

            return null;
        }

        private async UniTask<ESkillType?> RunSkillAsync(
            ESkillType skillType,
            CancellationToken token)
        {
            if (!CanUseSkill(skillType) ||
                !_actionCatalog.TryGetSkill(skillType, out CombatActionDefinition definition) ||
                !_animation.TryPlaySkill(
                    _characterType,
                    CurrentWeapon,
                    skillType,
                    out AnimationPlayback playback))
            {
                return null;
            }

            SetPhase(PlayerCombatPhase.SkillAttack);
            SetComboIndex(0);
            SetHitWindow(false);
            _acceptedBufferedAction = null;

            await ExecuteObservedActionAsync(
                definition,
                playback.Duration,
                ActionObservationMode.Skill,
                token);

            _lastSkillUseTime[skillType] = _clock.ScaledTime;
            SetHitWindow(false);
            return _acceptedBufferedAction;
        }

        private async UniTask<bool> RunWeaponTransitionAsync(
            EWeaponType animationWeapon,
            bool draw,
            EWeaponType resultingWeapon,
            CancellationToken token)
        {
            if (!_animation.TryPlayWeaponTransition(animationWeapon, draw, out AnimationPlayback playback))
            {
                return false;
            }

            _pendingWeapon = resultingWeapon;
            _weaponApplied = false;
            CombatActionRunResult result = await ExecuteObservedActionAsync(
                _actionCatalog.GetWeaponTransition(),
                playback.Duration,
                ActionObservationMode.WeaponTransition,
                token);

            return result == CombatActionRunResult.Completed && _weaponApplied;
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
                case ActionObservationMode.Combo:
                    return ObserveBufferedActionFrame(
                        in frame,
                        frame.CanChain && ComboIndex < _animation.ComboCount - 1);

                case ActionObservationMode.Skill:
                    return ObserveBufferedActionFrame(in frame, frame.CanCancel);

                case ActionObservationMode.WeaponTransition:
                    if (frame.CommitReached)
                    {
                        ApplyWeapon(_pendingWeapon);
                        _weaponApplied = true;
                    }
                    break;
            }

            return CombatActionDirective.Continue;
        }

        private CombatActionDirective ObserveBufferedActionFrame(
            in CombatActionFrame frame,
            bool canAcceptDefaultAction)
        {
            _canAcceptDefaultBufferedAction = canAcceptDefaultAction;
            _canAcceptSkillBufferedAction = frame.CanCancel;
            if (!_inputBuffer.TryConsume(_bufferedActionPredicate, out ESkillType bufferedAction))
            {
                return CombatActionDirective.Continue;
            }

            _acceptedBufferedAction = bufferedAction;
            return CombatActionDirective.Interrupt;
        }

        private bool CanAcceptBufferedAction(in InputBufferEntry<ESkillType> entry)
        {
            return entry.Action == ESkillType.Default
                ? _canAcceptDefaultBufferedAction
                : _canAcceptSkillBufferedAction && CanUseSkill(entry.Action);
        }

        private bool CanUseSkill(ESkillType skillType)
        {
            if (!_actionCatalog.TryGetSkill(skillType, out CombatActionDefinition definition) ||
                !_animation.HasSkill(_characterType, CurrentWeapon, skillType))
            {
                return false;
            }

            return !_lastSkillUseTime.TryGetValue(skillType, out float lastUseTime) ||
                   _clock.ScaledTime - lastUseTime >= definition.Cooldown;
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
            SetHitWindow(false);
            SetComboIndex(0);
            SetPhase(PlayerCombatPhase.Ready);
        }

        private void CancelActiveAction()
        {
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

        private void ApplyWeapon(EWeaponType weaponType)
        {
            _animation.ApplyLoadout(_characterType, weaponType);
            _weaponRig.ApplyWeapon(weaponType);
            if (CurrentWeapon == weaponType)
            {
                return;
            }

            CurrentWeapon = weaponType;
            WeaponChanged?.Invoke(weaponType);
        }

        private void SetPhase(PlayerCombatPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            if (!CanChangePhase(Phase, phase))
            {
                throw new InvalidOperationException(
                    $"Player combat transition '{Phase}' -> '{phase}' is not allowed.");
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private static bool CanChangePhase(PlayerCombatPhase from, PlayerCombatPhase to)
        {
            return to == PlayerCombatPhase.Ready ||
                   from == PlayerCombatPhase.Ready ||
                   (from == PlayerCombatPhase.ComboAttack && to == PlayerCombatPhase.SkillAttack);
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
            if (open && CurrentWeapon is not EWeaponType.None and not EWeaponType.Unarmed)
            {
                _weaponRig.OpenHitbox();
            }
            else
            {
                _weaponRig.CloseHitbox();
            }

            HitWindowChanged?.Invoke(open);
        }

        private void OnAttackRequested()
        {
            TryAttack();
        }

        private void OnSkillRequested(ESkillType skillType)
        {
            TryAttack(skillType);
        }

        private void OnSwitchWeaponRequested()
        {
            ToggleWeaponAsync(EWeaponType.TwoHanded).Forget();
        }

        private void OnDodgeAccepted()
        {
            InterruptAttack();
        }
    }
}
