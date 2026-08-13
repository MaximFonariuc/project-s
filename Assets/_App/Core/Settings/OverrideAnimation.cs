using System;
using System.Collections.Generic;
using System.Linq;
using _App;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "OverrideAnimation", menuName = "Combat/OverrideAnimation")]
    public class OverrideAnimation : ScriptableObject
    {
        [Header("DefaultSetting")]
        [field: SerializeField] public EWeaponType WeaponType { get; protected set; }
        [field: SerializeField] public List<AnimationClip> DefaultsAnimationClips { get; protected set; }

        [Header("Combat")]
        [field: SerializeField] public List<DefaultSkillCombo> DefaultCombos { get; protected set; }
        [field: SerializeField] public List<Skill> Skills { get; protected set; }

        public T GetSkill<T>(EWeaponType weaponType, ESkillType skillType) where T : SkillBase
        {
            if (WeaponType != weaponType)
            {
                return null;
            }
            
            if (typeof(T) == typeof(DefaultSkillCombo))
            {
                var defaultCombo = DefaultCombos.FirstOrDefault(combo => combo.SkillType == skillType);
                return defaultCombo as T;
            }

            if (typeof(T) == typeof(Skill))
            {
                var skill = Skills.FirstOrDefault(combo => combo.SkillType == skillType);
                return skill as T;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (WeaponType != EWeaponType.None)
            {
                string expectedName = $"{WeaponType}_OverrideAnimation";
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
    
    [Serializable]
    public abstract class SkillBase
    {
        [field: SerializeField] public ESkillType SkillType { get; protected set; }
    }

    [Serializable]
    public class DefaultSkillCombo : SkillBase
    {
        [field: SerializeField] public List<AnimationClip> ComboClips { get; protected set; }

        public AnimationClip GetClip(int comboIndex = 0)
        {
            if (ComboClips == null || ComboClips.Count == 0) return null;
            return ComboClips[Mathf.Clamp(comboIndex, 0, ComboClips.Count - 1)];
        }
    }

    [Serializable]
    public class Skill : SkillBase
    {
        [field: SerializeField] public AnimationClip AnimationClip { get; protected set; }
    }
}