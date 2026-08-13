using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCombatController :
        IPlayerCombatCommands,
        IPlayerCombatState,
        IDisposable
    {
        private const float AttackWindowStart = 0.5f;
        private const float AttackWindowEnd = 0.75f;
        private const float HitWindowStart = 0.2f;
        private const float HitWindowEnd = 0.55f;
        private const float AnimationStateTimeout = 2.0f;

        private readonly IPlayerInput _input;
        private readonly Character _character;
        private readonly IPlayerLocomotionEvents _locomotionEvents;
        private readonly IPlayerCombatAnimation _animation;
        private readonly IPlayerWeaponRig _weaponRig;
        private readonly CombatInputBuffer _inputBuffer;
        private readonly ICombatClock _clock;
        private readonly ECharacterType _characterType;
        private readonly Dictionary<ESkillType, float> _skillCooldowns = new Dictionary<ESkillType, float>
        {
            [ESkillType.Special] = 1.25f
        };
        private readonly Dictionary<ESkillType, float> _lastSkillUseTime =
            new Dictionary<ESkillType, float>();

        private CancellationTokenSource _actionCancellation;
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
                bool attacking = Phase is PlayerCombatPhase.ComboAttack or PlayerCombatPhase.SkillAttack;
                bool switching = Phase == PlayerCombatPhase.WeaponSwitch;
                bool faceMoveDirection = attacking &&
                                         _character.RotationMode == ERotationMode.OrientRotationToMovement;

                return new PlayerMovementIntent(
                    attacking ? 0.0f : 1.0f,
                    !attacking && !switching,
                    Phase != PlayerCombatPhase.SkillAttack && !switching,
                    faceMoveDirection);
            }
        }

        internal PlayerCombatController(
            IPlayerInput input,
            Character character,
            IPlayerLocomotionEvents locomotionEvents,
            IPlayerCombatAnimation animation,
            IPlayerWeaponRig weaponRig,
            CombatInputBuffer inputBuffer,
            ICombatClock clock,
            ECharacterType characterType)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
            _locomotionEvents = locomotionEvents ?? throw new ArgumentNullException(nameof(locomotionEvents));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _weaponRig = weaponRig ?? throw new ArgumentNullException(nameof(weaponRig));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _characterType = characterType;
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
            if (!_enabled || !_character.CanAttack() || Phase == PlayerCombatPhase.WeaponSwitch)
            {
                return false;
            }

            if (Phase == PlayerCombatPhase.ComboAttack)
            {
                _inputBuffer.Store(skillType);
                return true;
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
                _character.IsFalling())
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
                Debug.LogException(exception, _character);
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
                Debug.LogException(exception, _character);
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
                if (!_animation.TryPlayComboStep(ComboIndex, out string stateName) ||
                    !await WaitForStateAsync(stateName, AnimationController.BASE_LAYER_INDEX, token))
                {
                    return null;
                }

                bool advanceCombo = false;
                while (_animation.IsInState(stateName, AnimationController.BASE_LAYER_INDEX))
                {
                    token.ThrowIfCancellationRequested();
                    float normalizedTime = _animation.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX);
                    SetHitWindow(normalizedTime is >= HitWindowStart and < HitWindowEnd);

                    if (normalizedTime is >= AttackWindowStart and <= AttackWindowEnd &&
                        _inputBuffer.TryConsume(out ESkillType bufferedAction))
                    {
                        SetHitWindow(false);
                        if (bufferedAction == ESkillType.Default && ComboIndex < _animation.ComboCount - 1)
                        {
                            SetComboIndex(ComboIndex + 1);
                            advanceCombo = true;
                            break;
                        }

                        if (bufferedAction != ESkillType.Default && CanUseSkill(bufferedAction))
                        {
                            return bufferedAction;
                        }
                    }

                    if (normalizedTime >= AttackWindowEnd)
                    {
                        break;
                    }

                    await UniTask.Yield(token);
                }

                SetHitWindow(false);
                if (!advanceCombo)
                {
                    break;
                }
            }

            return null;
        }

        private async UniTask<ESkillType?> RunSkillAsync(
            ESkillType skillType,
            CancellationToken token)
        {
            if (!CanUseSkill(skillType) ||
                !_animation.TryPlaySkill(
                    _characterType,
                    CurrentWeapon,
                    skillType,
                    out string stateName))
            {
                return null;
            }

            SetPhase(PlayerCombatPhase.SkillAttack);
            SetComboIndex(0);
            SetHitWindow(false);

            if (!await WaitForStateAsync(stateName, AnimationController.BASE_LAYER_INDEX, token))
            {
                return null;
            }

            while (_animation.IsInState(stateName, AnimationController.BASE_LAYER_INDEX))
            {
                token.ThrowIfCancellationRequested();
                float normalizedTime = _animation.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX);
                SetHitWindow(normalizedTime is >= HitWindowStart and < HitWindowEnd);
                if (normalizedTime >= AttackWindowEnd)
                {
                    _lastSkillUseTime[skillType] = _clock.ScaledTime;
                    break;
                }

                await UniTask.Yield(token);
            }

            SetHitWindow(false);
            return null;
        }

        private async UniTask<bool> RunWeaponTransitionAsync(
            EWeaponType animationWeapon,
            bool draw,
            EWeaponType resultingWeapon,
            CancellationToken token)
        {
            if (!_animation.TryPlayWeaponTransition(animationWeapon, draw, out string stateName) ||
                !await WaitForStateAsync(stateName, AnimationController.SWITCH_LAYER_INDEX, token))
            {
                return false;
            }

            while (_animation.IsInState(stateName, AnimationController.SWITCH_LAYER_INDEX))
            {
                token.ThrowIfCancellationRequested();
                if (_animation.GetNormalizedTime(AnimationController.SWITCH_LAYER_INDEX) >= 0.5f)
                {
                    ApplyWeapon(resultingWeapon);
                    return true;
                }

                await UniTask.Yield(token);
            }

            return false;
        }

        private async UniTask<bool> WaitForStateAsync(
            string stateName,
            int layerIndex,
            CancellationToken token)
        {
            float timeoutAt = _clock.UnscaledTime + AnimationStateTimeout;
            while (!_animation.IsInState(stateName, layerIndex))
            {
                token.ThrowIfCancellationRequested();
                if (_clock.UnscaledTime >= timeoutAt)
                {
                    Debug.LogError(
                        $"Combat animation state '{stateName}' was not entered within {AnimationStateTimeout:0.0}s.",
                        _character);
                    return false;
                }

                await UniTask.Yield(token);
            }

            return true;
        }

        private bool CanUseSkill(ESkillType skillType)
        {
            if (!_skillCooldowns.TryGetValue(skillType, out float cooldown) ||
                !_animation.HasSkill(_characterType, CurrentWeapon, skillType))
            {
                return false;
            }

            return !_lastSkillUseTime.TryGetValue(skillType, out float lastUseTime) ||
                   _clock.ScaledTime - lastUseTime >= cooldown;
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
