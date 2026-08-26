using System;
using UnityEngine;

namespace _App
{
    [Serializable]
    public struct WeaponAttachmentPose
    {
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Vector3 _localEulerAngles;

        internal void Apply(Transform target)
        {
            target.localPosition = _localPosition;
            target.localRotation = Quaternion.Euler(_localEulerAngles);
        }
    }

    [Serializable]
    public sealed class PlayerWeaponAttachment
    {
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _equippedParent;
        [SerializeField] private WeaponAttachmentPose _equippedPose;
        [SerializeField] private Transform _sheathedParent;
        [SerializeField] private WeaponAttachmentPose _sheathedPose;
        [SerializeField] private Weapon[] _hitboxes;

        internal Transform WeaponRoot => _weaponRoot;

        internal bool Apply(bool equipped, bool visible)
        {
            if (_weaponRoot == null)
            {
                return false;
            }

            if (!visible)
            {
                _weaponRoot.gameObject.SetActive(false);
                return true;
            }

            Transform parent = equipped ? _equippedParent : _sheathedParent;
            if (parent == null)
            {
                _weaponRoot.gameObject.SetActive(false);
                Debug.LogError(
                    $"Weapon attachment '{_weaponRoot.name}' has no " +
                    $"{(equipped ? "equipped" : "sheathed")} parent.",
                    _weaponRoot);
                return false;
            }

            _weaponRoot.SetParent(parent, false);
            if (equipped)
            {
                _equippedPose.Apply(_weaponRoot);
            }
            else
            {
                _sheathedPose.Apply(_weaponRoot);
            }

            _weaponRoot.gameObject.SetActive(true);
            return true;
        }

        internal void EnableHitboxes(
            CombatAttackExecution execution,
            ICombatHitResolver hitResolver)
        {
            if (_hitboxes == null)
            {
                return;
            }

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                _hitboxes[i]?.EnableHitbox(execution, hitResolver);
            }
        }

        internal void DisableHitboxes()
        {
            if (_hitboxes == null)
            {
                return;
            }

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                _hitboxes[i]?.DisableHitbox();
            }
        }

        internal bool HasHitboxes()
        {
            if (_hitboxes == null)
            {
                return false;
            }

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class PlayerWeaponBinding
    {
        [SerializeField] private EWeaponType _weaponType;
        [SerializeField] private bool _visibleWhenUnequipped = true;
        [SerializeField] private PlayerWeaponAttachment[] _attachments;

        internal EWeaponType WeaponType => _weaponType;
        internal bool VisibleWhenUnequipped => _visibleWhenUnequipped;

        internal void Apply(bool equipped, bool visible)
        {
            if (_attachments == null)
            {
                return;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                _attachments[i]?.Apply(equipped, visible);
            }
        }

        internal bool EnableHitboxes(
            CombatAttackExecution execution,
            ICombatHitResolver hitResolver)
        {
            bool enabledAny = false;
            if (_attachments == null)
            {
                return false;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                PlayerWeaponAttachment attachment = _attachments[i];
                if (attachment == null || !attachment.HasHitboxes())
                {
                    continue;
                }

                attachment.EnableHitboxes(execution, hitResolver);
                enabledAny = true;
            }

            return enabledAny;
        }

        internal void DisableHitboxes()
        {
            if (_attachments == null)
            {
                return;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                _attachments[i]?.DisableHitboxes();
            }
        }

        internal bool HasHitboxes()
        {
            if (_attachments == null)
            {
                return false;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                if (_attachments[i]?.HasHitboxes() == true)
                {
                    return true;
                }
            }

            return false;
        }
    }

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
    }
}
