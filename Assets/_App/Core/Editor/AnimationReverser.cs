using UnityEditor;
using UnityEngine;

namespace _App
{
    public class AnimationReverser
    {
        [MenuItem("Tools/Reverse Animation Clip")]
        static void ReverseAnimationClip()
        {
            AnimationClip originalClip = Selection.activeObject as AnimationClip;
            if (originalClip == null)
            {
                Debug.LogError("Выберите анимационный клип в Project");
                return;
            }

            string path = AssetDatabase.GetAssetPath(originalClip);
            AnimationClip reversedClip = new AnimationClip
            {
                frameRate = originalClip.frameRate,
                legacy = originalClip.legacy,
                wrapMode = originalClip.wrapMode
            };

            float clipLength = originalClip.length;

            foreach (var binding in AnimationUtility.GetCurveBindings(originalClip))
            {
                AnimationCurve originalCurve = AnimationUtility.GetEditorCurve(originalClip, binding);
                Keyframe[] originalKeys = originalCurve.keys;
                Keyframe[] reversedKeys = new Keyframe[originalKeys.Length];

                for (int i = 0; i < originalKeys.Length; i++)
                {
                    Keyframe originalKey = originalKeys[i];
                    Keyframe reversedKey = new Keyframe(clipLength - originalKey.time, originalKey.value);

                    reversedKey.inTangent = -originalKey.outTangent;
                    reversedKey.outTangent = -originalKey.inTangent;
                    reversedKey.inWeight = originalKey.outWeight;
                    reversedKey.outWeight = originalKey.inWeight;
                    reversedKey.weightedMode = originalKey.weightedMode;

                    reversedKeys[originalKeys.Length - 1 - i] = reversedKey;
                }

                AnimationCurve reversedCurve = new AnimationCurve(reversedKeys);
                reversedClip.SetCurve(binding.path, binding.type, binding.propertyName, reversedCurve);
            }

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(originalClip);
            foreach (var e in events)
            {
                e.time = clipLength - e.time;
            }
            AnimationUtility.SetAnimationEvents(reversedClip, events);

            SerializedObject clipSettings = new SerializedObject(originalClip);
            SerializedObject reversedSettings = new SerializedObject(reversedClip);

            CopyAnimationSettings(clipSettings, reversedSettings);

            string newPath = path.Replace(".anim", "_Reversed.anim");
            AssetDatabase.CreateAsset(reversedClip, newPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Reversed animation saved at: " + newPath);
        }

        static void CopyAnimationSettings(SerializedObject source, SerializedObject target)
        {
            string[] settings = new string[]
            {
                "m_AnimationType",
                "m_Compression",
                "m_RotationError",
                "m_PositionError",
                "m_ScaleError",
                "m_AnimationClipSettings.m_LoopTime",
                "m_AnimationClipSettings.m_LoopBlend",
                "m_AnimationClipSettings.m_LoopBlendOrientation",
                "m_AnimationClipSettings.m_LoopBlendPositionY",
                "m_AnimationClipSettings.m_LoopBlendPositionXZ",
                "m_AnimationClipSettings.m_StartTime",
                "m_AnimationClipSettings.m_StopTime",
                "m_AnimationClipSettings.m_OrientationOffsetY",
                "m_AnimationClipSettings.m_Level",
                "m_AnimationClipSettings.m_CycleOffset"
            };

            foreach (string setting in settings)
            {
                SerializedProperty property = source.FindProperty(setting);
                if (property != null)
                {
                    target.FindProperty(setting).serializedObject.CopyFromSerializedProperty(property);
                }
            }

            target.ApplyModifiedProperties();
        }
    }
}
