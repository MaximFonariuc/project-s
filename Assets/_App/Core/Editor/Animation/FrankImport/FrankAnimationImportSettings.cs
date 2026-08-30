using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal static class FrankAnimationImportSettings
    {
        internal const string FrankRoot = "Assets/_Game/Animations/Frank/";

        [Serializable]
        private sealed class AnimationCatalog
        {
            public List<AnimationCatalogClip> clips = new();
        }

        [Serializable]
        private sealed class AnimationCatalogClip
        {
            public string assetPath;
            public string rigAssetPath;
        }

        internal static bool IsGeneratedClip(string path)
        {
            return path.StartsWith(FrankRoot, StringComparison.Ordinal)
                   && path.Contains("/Animations/", StringComparison.Ordinal)
                   && !path.Contains("/Diagnostics/", StringComparison.Ordinal)
                   && !path.Contains("/Rig/", StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGeneratedRig(string path)
        {
            return path.StartsWith(FrankRoot + "Rig/Source/", StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ApplyRig(ModelImporter importer)
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

        internal static IReadOnlyDictionary<string, string> LoadRigAssignments()
        {
            var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
            var catalogPaths = AssetDatabase.FindAssets("t:TextAsset", new[] { FrankRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(
                    "AnimationCatalog.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path);

            foreach (var catalogPath in catalogPaths)
            {
                var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
                var catalog = catalogAsset != null
                    ? JsonUtility.FromJson<AnimationCatalog>(catalogAsset.text)
                    : null;
                if (catalog?.clips == null)
                    throw new InvalidDataException(
                        $"Frank animation catalog is invalid: {catalogPath}");

                foreach (var clip in catalog.clips)
                {
                    if (string.IsNullOrWhiteSpace(clip.assetPath)
                        || string.IsNullOrWhiteSpace(clip.rigAssetPath))
                    {
                        throw new InvalidDataException(
                            $"Frank animation catalog contains an incomplete rig assignment: "
                            + catalogPath);
                    }

                    if (assignments.TryGetValue(clip.assetPath, out var existingRigPath)
                        && !string.Equals(
                            existingRigPath, clip.rigAssetPath, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Frank animation has conflicting rig assignments: {clip.assetPath}");
                    }

                    assignments[clip.assetPath] = clip.rigAssetPath;
                }
            }

            return assignments;
        }

        internal static Avatar LoadSourceAvatar(
            string assetPath,
            IReadOnlyDictionary<string, string> rigAssignments,
            out string rigAssetPath)
        {
            if (!rigAssignments.TryGetValue(assetPath, out rigAssetPath))
                throw new InvalidDataException(
                    $"Frank animation has no source-rig assignment: {assetPath}");

            var sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(rigAssetPath);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidDataException(
                    $"Frank animation source Avatar is missing or invalid: "
                    + $"{assetPath} -> {rigAssetPath}");

            return sourceAvatar;
        }

        internal static void Apply(
            ModelImporter importer,
            string assetPath,
            Avatar sourceAvatar)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.resampleCurves = false;
            importer.humanoidOversampling = ModelImporterHumanoidOversampling.X4;
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
