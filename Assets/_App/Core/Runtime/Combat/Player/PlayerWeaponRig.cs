using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerWeaponRig : IPlayerWeaponRig
    {
        private readonly PlayerWeaponBinding[] _weapons;
        private readonly Weapon[] _unarmedHitboxes;
        private readonly ICombatHitResolver _hitResolver;
        private EWeaponType _weaponType = EWeaponType.Unarmed;

        public PlayerWeaponRig(
            PlayerWeaponLoadout loadout,
            ICombatHitResolver hitResolver)
        {
            _weapons = loadout != null
                ? loadout.Weapons
                : Array.Empty<PlayerWeaponBinding>();
            _unarmedHitboxes = loadout != null
                ? loadout.UnarmedHitboxes
                : Array.Empty<Weapon>();
            _hitResolver = hitResolver;
        }

        public bool CanEquip(EWeaponType weaponType)
        {
            if (weaponType == EWeaponType.Unarmed)
            {
                return true;
            }

            if (weaponType == EWeaponType.None)
            {
                return false;
            }

            PlayerWeaponBinding binding = FindBinding(weaponType);
            return binding != null && binding.IsConfigured();
        }

        public void ApplyWeapon(EWeaponType weaponType)
        {
            CloseHitbox();
            _weaponType = weaponType;
            for (int i = 0; i < _weapons.Length; i++)
            {
                PlayerWeaponBinding binding = _weapons[i];
                if (binding == null)
                {
                    continue;
                }

                bool equipped = binding.WeaponType == weaponType;
                bool visible = weaponType != EWeaponType.None &&
                               (equipped || binding.VisibleWhenUnequipped);
                binding.Apply(equipped, visible);
            }
        }

        public void OpenHitbox(CombatAttackExecution execution)
        {
            if (_weaponType is EWeaponType.None or EWeaponType.Unarmed)
            {
                EnableUnarmedHitboxes(execution);
                return;
            }

            PlayerWeaponBinding binding = FindBinding(_weaponType);
            if (binding == null || !binding.EnableHitboxes(execution, _hitResolver))
            {
                Debug.LogError($"No hitboxes are configured for weapon '{_weaponType}'.");
            }
        }

        public void CloseHitbox()
        {
            for (int i = 0; i < _weapons.Length; i++)
            {
                _weapons[i]?.DisableHitboxes();
            }

            for (int i = 0; i < _unarmedHitboxes.Length; i++)
            {
                _unarmedHitboxes[i]?.DisableHitbox();
            }
        }

        private PlayerWeaponBinding FindBinding(EWeaponType weaponType)
        {
            for (int i = 0; i < _weapons.Length; i++)
            {
                PlayerWeaponBinding binding = _weapons[i];
                if (binding != null && binding.WeaponType == weaponType)
                {
                    return binding;
                }
            }

            return null;
        }

        private void EnableUnarmedHitboxes(CombatAttackExecution execution)
        {
            for (int i = 0; i < _unarmedHitboxes.Length; i++)
            {
                _unarmedHitboxes[i]?.EnableHitbox(execution, _hitResolver);
            }
        }
    }
}
