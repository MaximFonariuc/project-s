using System;
using Cysharp.Threading.Tasks;
using ModularAnimation;

namespace _App
{
    public enum PlayerCombatPhase
    {
        Ready,
        Executing,
        Recovery,
        WeaponSwitch,
        Interrupted
    }

    public interface IPlayerCombatCommands
    {
        bool TryExecute(PlayerCombatCommandId commandId);
        UniTask<bool> ToggleWeaponAsync(EWeaponType targetWeapon);
        void InterruptAttack();
    }

    public interface IPlayerCombatState
    {
        event Action<PlayerCombatPhase> PhaseChanged;
        event Action<EWeaponType> WeaponChanged;
        event Action<int> ComboIndexChanged;
        event Action<bool> HitWindowChanged;

        PlayerCombatPhase Phase { get; }
        EWeaponType CurrentWeapon { get; }
        int ComboIndex { get; }
        bool IsHitWindowOpen { get; }
        PlayerMovementIntent MovementIntent { get; }
    }

    internal interface IPlayerCombatAnimation : IDisposable
    {
        int ComboCount { get; }

        void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType);
        bool HasNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId);
        bool TryPlayComboStep(int comboIndex, out AnimationPlayback playback);
        bool TryPlayNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out AnimationPlayback playback);
        bool TryPlayWeaponTransition(EWeaponType weaponType, bool draw, out AnimationPlayback playback);
    }

    internal interface IPlayerWeaponRig
    {
        void ApplyWeapon(EWeaponType weaponType);
        void OpenHitbox(CombatAttackExecution execution);
        void CloseHitbox();
    }

    internal interface ICombatClock
    {
        float DeltaTime { get; }
        float ScaledTime { get; }
        float UnscaledTime { get; }
    }
}
