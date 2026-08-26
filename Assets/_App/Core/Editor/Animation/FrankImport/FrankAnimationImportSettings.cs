using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal static class FrankAnimationImportSettings
    {
        internal const string FrankRoot = "Assets/_Game/Animations/Frank/";
        internal const string ExistingDualSwordRoot = FrankRoot + "DualSwords/";
        internal const string SharedAvatarPath = FrankRoot + "Rig/FrankHumanoidAnimationRig.fbx";

        internal static bool IsGeneratedClip(string path)
        {
            return path.StartsWith(FrankRoot, StringComparison.Ordinal)
                   && !path.StartsWith(ExistingDualSwordRoot, StringComparison.Ordinal)
                   && !path.StartsWith(FrankRoot + "Rig/", StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ApplySharedRig(ModelImporter importer)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
        }

        internal static void Apply(ModelImporter importer, string assetPath, Avatar sourceAvatar)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.resampleCurves = false;
            importer.removeConstantScaleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.importAnimatedCustomProperties = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            var clips = importer.defaultClipAnimations;
            var clipName = Path.GetFileNameWithoutExtension(assetPath);
            var shouldLoop = IsLoopingClip(clipName);
            for (var index = 0; index < clips.Length; index++)
            {
                clips[index].name = clipName;
                clips[index].loop = shouldLoop;
                clips[index].loopTime = shouldLoop;
                clips[index].loopPose = shouldLoop;
                clips[index].keepOriginalOrientation = true;
                clips[index].keepOriginalPositionY = true;
                clips[index].keepOriginalPositionXZ = true;
            }

            if (clips.Length > 0)
                importer.clipAnimations = clips;
        }

        private static bool IsLoopingClip(string clipName)
        {
            if (ContainsAny(clipName, "Attack", "Combo", "Skill", "Hit", "Die", "Death", "Evade", "Step"))
                return false;

            return ContainsAny(clipName, "Idle", "Walk", "Run", "Sprint", "Loop");
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (var token in tokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
