using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerWeaponRig : IPlayerWeaponRig
    {
        private readonly Transform _weaponHolder;
        private readonly Transform _weaponHand;
        private readonly Transform _weaponTransform;
        private readonly Weapon _weapon;
        private readonly Weapon[] _unarmedHitboxes;
        private readonly ICombatHitResolver _hitResolver;
        private EWeaponType _weaponType = EWeaponType.Unarmed;

        public PlayerWeaponRig(
            Transform weaponHolder,
            Transform weaponHand,
            Transform weaponTransform,
            Weapon weapon,
            Weapon[] unarmedHitboxes,
            ICombatHitResolver hitResolver)
        {
            _weaponHolder = weaponHolder;
            _weaponHand = weaponHand;
            _weaponTransform = weaponTransform;
            _weapon = weapon;
            _unarmedHitboxes = unarmedHitboxes ?? Array.Empty<Weapon>();
            _hitResolver = hitResolver;
        }

        public void ApplyWeapon(EWeaponType weaponType)
        {
            CloseHitbox();
            _weaponType = weaponType;
            if (_weaponTransform == null)
            {
                return;
            }

            bool equipped = weaponType is not EWeaponType.None and not EWeaponType.Unarmed;
            Transform parent = equipped ? _weaponHand : _weaponHolder;
            if (parent == null)
            {
                Debug.LogError(
                    $"Player weapon rig has no {(equipped ? "hand" : "holder")} transform.",
                    _weaponTransform);
                return;
            }

            _weaponTransform.SetParent(parent);
            _weaponTransform.localPosition = Vector3.zero;
            _weaponTransform.localRotation = Quaternion.identity;
        }

        public void OpenHitbox(CombatAttackExecution execution)
        {
            // The animation-first combat prototype does not yet own final weapon
            // prefabs. Keep the verified hand hitboxes active until a concrete
            // weapon rig is authored, so selecting a visual loadout cannot make
            // combat silently stop producing hit candidates.
            if (_weaponType == EWeaponType.Unarmed || _weapon == null)
            {
                for (int i = 0; i < _unarmedHitboxes.Length; i++)
                {
                    _unarmedHitboxes[i]?.EnableHitbox(execution, _hitResolver);
                }

                return;
            }

            _weapon?.EnableHitbox(execution, _hitResolver);
        }

        public void CloseHitbox()
        {
            _weapon?.DisableHitbox();
            for (int i = 0; i < _unarmedHitboxes.Length; i++)
            {
                _unarmedHitboxes[i]?.DisableHitbox();
            }
        }
    }
}
