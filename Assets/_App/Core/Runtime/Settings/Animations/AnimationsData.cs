using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "AnimationsData", menuName = "Combat/Animation Data")]
    public class AnimationsData : ScriptableObject
    {
        private static readonly IReadOnlyList<AnimationClip> EmptyClips =
            new List<AnimationClip>(0);

        [field: SerializeField] public ECharacterType CharacterType { get; protected set; }
        [field: SerializeField] public List<CombatAnimationSet> AnimationDatas { get; protected set; }

        public IReadOnlyList<CombatAnimationPresentation> GetComboActions(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            return animations?.ComboActions ?? Array.Empty<CombatAnimationPresentation>();
        }

        public bool TryGetAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out CombatAnimationPresentation presentation)
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
            out CombatAnimationPresentation presentation)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            if (animations != null && animations.TryGetWeaponTransition(draw, out presentation))
            {
                return true;
            }

            presentation = null;
            return false;
        }

        public IReadOnlyList<AnimationClip> GetControllerOverrides(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            CombatAnimationSet animations = FindAnimations(characterType, weaponType);
            return animations?.ControllerOverrides ?? EmptyClips;
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
