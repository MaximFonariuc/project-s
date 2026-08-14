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
        [field: SerializeField] public List<OverrideAnimation> AnimationDatas { get; protected set; }

        public IReadOnlyList<AnimationClip> GetComboClips(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            OverrideAnimation animations = FindAnimations(characterType, weaponType);
            return animations?.ComboClips ?? EmptyClips;
        }

        public AnimationClip GetActionClip(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId)
        {
            return FindAnimations(characterType, weaponType)?.GetActionClip(animationId);
        }

        public IReadOnlyList<AnimationClip> GetDefaultsAnimationClips(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            OverrideAnimation animations = FindAnimations(characterType, weaponType);
            return animations?.DefaultsAnimationClips ?? EmptyClips;
        }

        private OverrideAnimation FindAnimations(
            ECharacterType characterType,
            EWeaponType weaponType)
        {
            if (CharacterType != characterType || AnimationDatas == null)
            {
                return null;
            }

            for (int i = 0; i < AnimationDatas.Count; i++)
            {
                OverrideAnimation animations = AnimationDatas[i];
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
