using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.AnimationLibrary
{
    internal sealed class AnimationLibraryAssetPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingAvatarDependentImports = new(StringComparer.Ordinal);

        public override int GetPostprocessOrder() => -1000;

        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (!AnimationLibraryMetadataReader.TryReadForModel(assetPath, out AnimationLibraryImportMetadata metadata, out string error))
            {
                return;
            }

            if (metadata == null)
            {
                Debug.LogError($"[AnimationLibrary] {error}");
                return;
            }

            if (assetImporter is not ModelImporter importer)
            {
                Debug.LogError($"[AnimationLibrary] Expected ModelImporter for {assetPath}.");
                return;
            }

            Avatar sourceAvatar = LoadAvatar(metadata.TargetAvatarAsset);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
            {
                QueueAvatarDependentReimport(assetPath, metadata.TargetAvatarAsset);
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.preserveHierarchy = false;
            importer.userData = $"AnimationLibrary|{metadata.LibraryID}|{metadata.PipelineVersion}";
        }

        private void OnPreprocessAnimation()
        {
            if (!AnimationLibraryMetadataReader.TryReadForModel(assetPath, out AnimationLibraryImportMetadata metadata, out string error))
            {
                return;
            }

            if (metadata == null)
            {
                Debug.LogError($"[AnimationLibrary] {error}");
                return;
            }

            if (assetImporter is not ModelImporter importer)
            {
                Debug.LogError($"[AnimationLibrary] Expected ModelImporter for {assetPath}.");
                return;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"[AnimationLibrary] No animation take was found in {assetPath}.");
                return;
            }

            for (int index = 0; index < clips.Length; index++)
            {
                ModelImporterClipAnimation clip = clips[index];
                clip.name = clips.Length == 1 ? metadata.ClipName : $"{metadata.ClipName}_{index + 1}";
                clip.firstFrame = metadata.ClipFrameStart;
                clip.lastFrame = metadata.ClipFrameEnd;
                clip.loop = metadata.Loop;
                clip.loopTime = metadata.Loop;
                clip.loopPose = metadata.Loop;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;

                bool lockRoot = !metadata.HasRootMotion;
                clip.lockRootRotation = lockRoot;
                clip.lockRootHeightY = lockRoot;
                clip.lockRootPositionXZ = lockRoot;
            }

            importer.clipAnimations = clips;
        }

        private void OnPostprocessModel(GameObject importedModel)
        {
            if (!AnimationLibraryMetadataReader.TryReadForModel(assetPath, out AnimationLibraryImportMetadata metadata, out _) ||
                metadata == null)
            {
                return;
            }

            string importedAssetPath = assetPath;
            string[] labels = metadata.Labels ?? Array.Empty<string>();
            EditorApplication.delayCall += () =>
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(importedAssetPath);
                if (asset != null && !LabelsMatch(AssetDatabase.GetLabels(asset), labels))
                {
                    AssetDatabase.SetLabels(asset, labels);
                }
            };
        }

        private static bool LabelsMatch(string[] actual, string[] expected)
        {
            actual ??= Array.Empty<string>();
            expected ??= Array.Empty<string>();
            if (actual.Length != expected.Length)
            {
                return false;
            }

            var actualLabels = new HashSet<string>(actual, StringComparer.Ordinal);
            return actualLabels.Count == expected.Length && actualLabels.SetEquals(expected);
        }

        private static Avatar LoadAvatar(string avatarAssetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(avatarAssetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static void QueueAvatarDependentReimport(string modelAssetPath, string avatarAssetPath)
        {
            if (!PendingAvatarDependentImports.Add(modelAssetPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                PendingAvatarDependentImports.Remove(modelAssetPath);
                Avatar avatar = LoadAvatar(avatarAssetPath);
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    Debug.LogError(
                        $"[AnimationLibrary] Valid Humanoid Avatar was not found at {avatarAssetPath} " +
                        $"after importing {modelAssetPath}.");
                    return;
                }

                AssetDatabase.ImportAsset(modelAssetPath, ImportAssetOptions.ForceUpdate);
            };
        }
    }
}
