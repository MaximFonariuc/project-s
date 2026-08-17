using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace _App
{
    [MovedFrom(true, sourceNamespace: "_App", sourceClassName: "OverrideAnimation")]
    [CreateAssetMenu(fileName = "CombatAnimationSet", menuName = "Combat/Animation Set")]
    public sealed class CombatAnimationSet : ScriptableObject
    {
        [FormerlySerializedAs("<WeaponType>k__BackingField")]
        [SerializeField] private EWeaponType _weaponType;

        [SerializeField] private List<CharacterAnimationActionPresentation> _comboActions =
            new List<CharacterAnimationActionPresentation>();

        [FormerlySerializedAs("<Actions>k__BackingField")]
        [SerializeField] private List<CharacterAnimationActionPresentation> _namedActions =
            new List<CharacterAnimationActionPresentation>();

        [SerializeField] private CharacterAnimationActionPresentation _drawAction;
        [SerializeField] private CharacterAnimationActionPresentation _sheatheAction;

        public EWeaponType WeaponType => _weaponType;
        public IReadOnlyList<CharacterAnimationActionPresentation> ComboActions => _comboActions;

        public bool TryGetNamedAction(
            PlayerCombatAnimationId animationId,
            out CharacterAnimationActionPresentation presentation)
        {
            if (_namedActions != null && !string.IsNullOrEmpty(animationId.Value))
            {
                for (int i = 0; i < _namedActions.Count; i++)
                {
                    CharacterAnimationActionPresentation candidate = _namedActions[i];
                    if (candidate != null &&
                        string.Equals(candidate.Id, animationId.Value, StringComparison.Ordinal) &&
                        candidate.IsValid)
                    {
                        presentation = candidate;
                        return true;
                    }
                }
            }

            presentation = null;
            return false;
        }

        public bool TryGetWeaponTransition(
            bool draw,
            out CharacterAnimationActionPresentation presentation)
        {
            presentation = draw ? _drawAction : _sheatheAction;
            return presentation != null && presentation.IsValid;
        }

        public void ValidateConfiguration()
        {
            if (_weaponType == EWeaponType.None)
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' requires a weapon type.");
            }

            if (_comboActions == null)
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' has no combo collection.");
            }

            for (int i = 0; i < _comboActions.Count; i++)
            {
                if (_comboActions[i] == null || !_comboActions[i].IsValid)
                {
                    throw new InvalidOperationException(
                        $"Combat animation set '{name}' has an invalid combo presentation at index {i}.");
                }
            }

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            if (_namedActions != null)
            {
                for (int i = 0; i < _namedActions.Count; i++)
                {
                    CharacterAnimationActionPresentation action = _namedActions[i];
                    if (action == null || !action.IsValid || string.IsNullOrWhiteSpace(action.Id))
                    {
                        throw new InvalidOperationException(
                            $"Combat animation set '{name}' has an invalid named action at index {i}.");
                    }

                    if (!actionIds.Add(action.Id))
                    {
                        throw new InvalidOperationException(
                            $"Combat animation set '{name}' contains duplicate action id '{action.Id}'.");
                    }
                }
            }

            if (_weaponType != EWeaponType.Unarmed &&
                (_drawAction == null || !_drawAction.IsValid ||
                 _sheatheAction == null || !_sheatheAction.IsValid))
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' requires valid draw and sheathe actions.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_weaponType == EWeaponType.None)
            {
                return;
            }

            string expectedName = $"{_weaponType}_CombatAnimationSet";
            if (name == expectedName)
            {
                return;
            }

            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            UnityEditor.AssetDatabase.RenameAsset(assetPath, expectedName);
        }
#endif
    }

}
