using System;
using System.Collections.Generic;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "AnimationsData", menuName = "Combat/Animation Data")]
    public class AnimationsData : ScriptableObject
    {
        [field: SerializeField] public ECharacterType CharacterType { get; protected set; }
        [field: SerializeField] public List<CombatAnimationSet> AnimationDatas { get; protected set; }
        [SerializeField] private List<CharacterAnimationActionPresentation> _sharedActions =
            new List<CharacterAnimationActionPresentation>();

        public IReadOnlyList<CharacterAnimationActionPresentation> GetComboActions(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            return animations?.ComboActions ?? Array.Empty<CharacterAnimationActionPresentation>();
        }

        public bool TryGetAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out CharacterAnimationActionPresentation presentation)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            if (animations != null && animations.TryGetNamedAction(animationId, out presentation))
            {
                return true;
            }

            presentation = null;
            return false;
        }

        public bool TryGetWeaponTransition(
            ECharacterType characterType,
            EWeaponType weaponType,
            bool draw,
            out CharacterAnimationActionPresentation presentation)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            if (animations != null && animations.TryGetWeaponTransition(draw, out presentation))
            {
                return true;
            }

            presentation = null;
            return false;
        }

        public bool TryGetSharedAction(
            AnimationSlotId slot,
            out CharacterAnimationActionPresentation presentation)
        {
            if (slot.IsValid && _sharedActions != null)
            {
                for (int i = 0; i < _sharedActions.Count; i++)
                {
                    CharacterAnimationActionPresentation candidate = _sharedActions[i];
                    if (candidate != null && candidate.IsValid &&
                        string.Equals(candidate.Id, slot.Value, StringComparison.Ordinal))
                    {
                        presentation = candidate;
                        return true;
                    }
                }
            }

            presentation = null;
            return false;
        }

        public void ValidateConfiguration()
        {
            if (CharacterType == ECharacterType.None)
            {
                throw new InvalidOperationException(
                    $"Animation library '{name}' requires a character type.");
            }

            if (AnimationDatas == null)
            {
                throw new InvalidOperationException(
                    $"Animation library '{name}' has no animation sets.");
            }

            var weaponTypes = new HashSet<EWeaponType>();
            for (int i = 0; i < AnimationDatas.Count; i++)
            {
                CombatAnimationSet animationSet = AnimationDatas[i];
                if (animationSet == null)
                {
                    throw new InvalidOperationException(
                        $"Animation library '{name}' has a missing set at index {i}.");
                }

                animationSet.ValidateConfiguration();
                if (!weaponTypes.Add(animationSet.WeaponType))
                {
                    throw new InvalidOperationException(
                        $"Animation library '{name}' contains duplicate weapon type '{animationSet.WeaponType}'.");
                }
            }

            ValidateSharedActions();
        }

        private void ValidateSharedActions()
        {
            if (_sharedActions == null)
            {
                return;
            }

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _sharedActions.Count; i++)
            {
                CharacterAnimationActionPresentation action = _sharedActions[i];
                if (action == null || !action.IsValid || string.IsNullOrWhiteSpace(action.Id))
                {
                    throw new InvalidOperationException(
                        $"Animation library '{name}' has an invalid shared action at index {i}.");
                }

                if (!actionIds.Add(action.Id))
                {
                    throw new InvalidOperationException(
                        $"Animation library '{name}' contains duplicate shared action id '{action.Id}'.");
                }
            }
        }

        private CombatAnimationSet FindAnimations(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            if (CharacterType != characterType || AnimationDatas == null)
            {
                return null;
            }

            for (int i = 0; i < AnimationDatas.Count; i++)
            {
                CombatAnimationSet animations = AnimationDatas[i];
                if (animations != null && animations.WeaponType == weaponType)
                {
                    return animations;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (CharacterType != ECharacterType.None)
            {
                string expectedName = $"{CharacterType}_AnimationsData";
                if (name != expectedName)
                {
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
                    UnityEditor.AssetDatabase.RenameAsset(assetPath, expectedName);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
        }
#endif
    }
}
