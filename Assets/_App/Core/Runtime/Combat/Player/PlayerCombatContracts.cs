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
        void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType);
        bool CanPlayAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAction action);
        bool TryPlayAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAction action,
            out AnimationActionPlayback playback);
        bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out AnimationActionPlayback playback);
        void Release();
        void Cancel();
    }

    internal interface IPlayerWeaponRig
    {
        void ApplyWeapon(EWeaponType weaponType);
        void OpenHitbox(CombatAttackExecution execution);
        void CloseHitbox();
    }

    internal interface ICombatClock
    {
        float ScaledTime { get; }
        float UnscaledTime { get; }
    }
}
