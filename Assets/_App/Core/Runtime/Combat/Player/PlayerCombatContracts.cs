using System;
using Cysharp.Threading.Tasks;
using ModularAnimation;

namespace _App
{
    public enum PlayerCombatPhase
    {
        Ready,
        ComboAttack,
        SkillAttack,
        WeaponSwitch
    }

    public interface IPlayerCombatCommands
    {
        bool TryAttack(ESkillType skillType = ESkillType.Default);
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
        bool HasSkill(ECharacterType characterType, EWeaponType weaponType, ESkillType skillType);
        bool TryPlayComboStep(int comboIndex, out AnimationPlayback playback);
        bool TryPlaySkill(
            ECharacterType characterType,
            EWeaponType weaponType,
            ESkillType skillType,
            out AnimationPlayback playback);
        bool TryPlayWeaponTransition(EWeaponType weaponType, bool draw, out AnimationPlayback playback);
    }

    internal interface IPlayerWeaponRig
    {
        void ApplyWeapon(EWeaponType weaponType);
        void OpenHitbox();
        void CloseHitbox();
    }

    internal interface ICombatClock
    {
        float DeltaTime { get; }
        float ScaledTime { get; }
        float UnscaledTime { get; }
    }
}
