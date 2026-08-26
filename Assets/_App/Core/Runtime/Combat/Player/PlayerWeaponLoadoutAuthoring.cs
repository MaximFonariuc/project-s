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
                Debug.LogErrorFormat(
                    _weaponRoot,
                    "Weapon attachment '{0}' has no {1} parent.",
                    _weaponRoot.name,
                    equipped ? "equipped" : "sheathed");
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

        internal bool HasActiveHitboxes()
        {
            if (_hitboxes == null)
            {
                return false;
            }

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i]?.IsHitboxActive == true)
                {
                    return true;
                }
            }

            return false;
        }

        internal void SampleHitboxes()
        {
            if (_hitboxes == null)
            {
                return;
            }

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                _hitboxes[i]?.SampleSweptHitbox();
            }
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

        internal bool HasActiveHitboxes()
        {
            if (_attachments == null)
            {
                return false;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                if (_attachments[i]?.HasActiveHitboxes() == true)
                {
                    return true;
                }
            }

            return false;
        }

        internal void SampleHitboxes()
        {
            if (_attachments == null)
            {
                return;
            }

            for (int i = 0; i < _attachments.Length; i++)
            {
                _attachments[i]?.SampleHitboxes();
            }
        }
    }
}
