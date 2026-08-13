using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "AnimationsData", menuName = "Combat/Animation Data")]
    public class AnimationsData : ScriptableObject
    {
        [field: SerializeField] public ECharacterType CharacterType { get; protected set; }
        [field: SerializeField] public List<OverrideAnimation> AnimationDatas { get; protected set; }
        
        public T GetSkill<T>(ECharacterType characterType, EWeaponType weaponType, ESkillType skillType = ESkillType.Default) where T : SkillBase
        {
            if (CharacterType != characterType)
            {
                return null;
            }
            
            foreach (var overrideAnimation in AnimationDatas)
            {
                if (overrideAnimation.WeaponType == weaponType)
                {
                    return overrideAnimation.GetSkill<T>(weaponType, skillType);
                }
            }

            return null;
        }

        public Skill GetAllSkillsForCurrentWeapon(ECharacterType characterType, EWeaponType weaponType)
        {
            if (CharacterType != characterType)
            {
                return null;
            }
            
            foreach (var overrideAnimation in AnimationDatas)
            {
                if (overrideAnimation.WeaponType == weaponType)
                {
                    foreach (var skills in overrideAnimation.Skills)
                    {
                        return skills;
                    }
                }
            }

            return null;
        }

        public List<AnimationClip> GetDefaultsAnimationClips(ECharacterType characterType, EWeaponType weaponType)
        {
            if (CharacterType != characterType)
            {
                return null;
            }
            
            foreach (var overrideAnimation in AnimationDatas)
            {
                if (overrideAnimation.WeaponType == weaponType)
                {
                    return overrideAnimation.DefaultsAnimationClips;
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