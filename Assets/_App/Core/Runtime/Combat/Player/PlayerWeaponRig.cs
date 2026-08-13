using UnityEngine;

namespace _App
{
    internal sealed class PlayerWeaponRig : IPlayerWeaponRig
    {
        private readonly Transform _weaponHolder;
        private readonly Transform _weaponHand;
        private readonly Transform _weaponTransform;
        private readonly Weapon _weapon;

        public PlayerWeaponRig(
            Transform weaponHolder,
            Transform weaponHand,
            Transform weaponTransform,
            Weapon weapon)
        {
            _weaponHolder = weaponHolder;
            _weaponHand = weaponHand;
            _weaponTransform = weaponTransform;
            _weapon = weapon;
        }

        public void ApplyWeapon(EWeaponType weaponType)
        {
            CloseHitbox();
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

        public void OpenHitbox()
        {
            _weapon?.EnableHitbox();
        }

        public void CloseHitbox()
        {
            _weapon?.DisableHitbox();
        }
    }
}
