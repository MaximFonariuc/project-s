#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class ClipRotator180
{
    [MenuItem("Tools/Create Rotated 180 Copy")]
    static void RotateSelectedClip()
    {
        var clip = Selection.activeObject as AnimationClip;
        if (clip == null)
        {
            Debug.LogError("Выдели AnimationClip в Project");
            return;
        }

        AnimationClip newClip = Object.Instantiate(clip);

        // Накладываем модификатор root rotation
        newClip.name = clip.name + "_Rotated180";

        // Задаём offset в настройках
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(newClip);
        settings.orientationOffsetY = 180f; // поворот на 180 градусов по оси Y
        AnimationUtility.SetAnimationClipSettings(newClip, settings);

        var path = AssetDatabase.GetAssetPath(clip);
        path = path.Replace(".anim", "_Rotated180.anim");
        AssetDatabase.CreateAsset(newClip, path);

        Debug.Log("Создана копия с поворотом 180°: " + path);
    }
}
#endif