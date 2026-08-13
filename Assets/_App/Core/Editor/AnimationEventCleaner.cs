#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class AnimationEventCleaner
{
    [MenuItem("Tools/Animation/Remove All Animation Events (incl. FBX)")]
    public static void RemoveAllAnimationEvents()
    {
        int animClipCount = 0;
        int fbxClipCount = 0;

        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();

        foreach (string path in allAssetPaths)
        {
            // === Удаление событий из .anim файлов ===
            if (path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                if (clip != null && !AssetDatabase.IsSubAsset(clip))
                {
                    AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                    EditorUtility.SetDirty(clip);
                    animClipCount++;
                }
            }

            // === Удаление событий из FBX ===
            else if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                ModelImporterClipAnimation[] clips = importer.clipAnimations;

                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }

                bool modified = false;

                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].events != null && clips[i].events.Length > 0)
                    {
                        clips[i].events = new AnimationEvent[0];
                        modified = true;
                        fbxClipCount++;
                    }
                }

                if (modified)
                {
                    importer.clipAnimations = clips;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ Удалено событий из {animClipCount} .anim клипов и {fbxClipCount} анимаций внутри FBX.");
    }
}
#endif
