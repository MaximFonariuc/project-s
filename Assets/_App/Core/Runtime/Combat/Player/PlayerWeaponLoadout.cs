using System;
using UnityEngine;

namespace _App
{
    public sealed class PlayerWeaponLoadout : MonoBehaviour
    {
        [SerializeField] private Weapon[] _unarmedHitboxes;
        [SerializeField] private PlayerWeaponBinding[] _weapons;

        internal Weapon[] UnarmedHitboxes => _unarmedHitboxes ?? Array.Empty<Weapon>();
        internal PlayerWeaponBinding[] Weapons => _weapons ?? Array.Empty<PlayerWeaponBinding>();

        internal bool Supports(EWeaponType weaponType)
        {
            if (weaponType == EWeaponType.Unarmed)
            {
                for (int i = 0; i < UnarmedHitboxes.Length; i++)
                {
                    if (UnarmedHitboxes[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (weaponType == EWeaponType.None)
            {
                return false;
            }

            for (int i = 0; i < Weapons.Length; i++)
            {
                PlayerWeaponBinding binding = Weapons[i];
                if (binding != null && binding.WeaponType == weaponType && binding.HasHitboxes())
                {
                    return true;
                }
            }

            return false;
        }

        private void LateUpdate()
        {
            if (!HasActiveHitboxes())
            {
                return;
            }

            Physics.SyncTransforms();
            for (int i = 0; i < Weapons.Length; i++)
            {
                Weapons[i]?.SampleHitboxes();
            }

            for (int i = 0; i < UnarmedHitboxes.Length; i++)
            {
                UnarmedHitboxes[i]?.SampleSweptHitbox();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                Weapons[i]?.DisableHitboxes();
            }

            for (int i = 0; i < UnarmedHitboxes.Length; i++)
            {
                UnarmedHitboxes[i]?.DisableHitbox();
            }
        }

        private bool HasActiveHitboxes()
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                if (Weapons[i]?.HasActiveHitboxes() == true)
                {
                    return true;
                }
            }

            for (int i = 0; i < UnarmedHitboxes.Length; i++)
            {
                if (UnarmedHitboxes[i]?.IsHitboxActive == true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
