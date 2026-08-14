using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "OverrideAnimation", menuName = "Combat/OverrideAnimation")]
    public class OverrideAnimation : ScriptableObject
    {
        [Header("Default Setting")]
        [field: SerializeField] public EWeaponType WeaponType { get; protected set; }
        [field: SerializeField] public List<AnimationClip> DefaultsAnimationClips { get; protected set; }

        [Header("Combat")]
        [field: SerializeField] public List<AnimationClip> ComboClips { get; protected set; }
        [field: SerializeField] public List<CombatAnimationClip> Actions { get; protected set; }

        public AnimationClip GetActionClip(PlayerCombatAnimationId animationId)
        {
            if (Actions == null || string.IsNullOrEmpty(animationId.Value))
            {
                return null;
            }

            for (int i = 0; i < Actions.Count; i++)
            {
                CombatAnimationClip action = Actions[i];
                if (action != null &&
                    string.Equals(action.Id, animationId.Value, StringComparison.Ordinal))
                {
                    return action.AnimationClip;
                }
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
    public sealed class CombatAnimationClip
    {
        [field: SerializeField] public string Id { get; private set; } = "skill.special";
        [field: SerializeField] public AnimationClip AnimationClip { get; private set; }
    }
}
