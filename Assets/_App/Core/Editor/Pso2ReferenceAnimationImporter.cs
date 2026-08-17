using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class Pso2ReferenceAnimationImporter
    {
        private const string RootFolder = "Assets/_Game/Animations/Reference/PSO2NGS";
        private const string FemaleSourceRigPath = RootFolder + "/PSO2NGS_SourceRig_Female200000.fbx";
        private const string MaleSourceRigPath = RootFolder + "/PSO2NGS_SourceRig_Male100000.fbx";
        private const string PreviewFolder = RootFolder + "/Preview";
        private const float CurveMotionEpsilon = 0.00001f;

        private static readonly HashSet<string> KnownStaticClipNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "pl_std_rwiz2_10320_jump_rise",
                "pl_std_rwiz2_10340_jump_fall"
            };

        private static readonly string[] AnimationPackPaths =
        {
            RootFolder + "/Female200000/Common/PSO2NGS_Common_Locomotion.fbx",
            RootFolder + "/Female200000/DualBlade/PSO2NGS_DualBlade_Locomotion.fbx",
            RootFolder + "/Female200000/DualBlade/PSO2NGS_DualBlade_CombatCandidates.fbx",
            RootFolder + "/Male100000/Common/PSO2NGS_Common_Locomotion.fbx",
            RootFolder + "/Male100000/DualBlade/PSO2NGS_DualBlade_Locomotion.fbx",
            RootFolder + "/Male100000/DualBlade/PSO2NGS_DualBlade_CombatCandidates.fbx"
        };

        [MenuItem("Tools/Animation/PSO2 NGS/Configure Reference FBX")]
        private static void ConfigureReferenceFbx()
        {
            var selectionSnapshot = CaptureActiveSelection();
            Selection.objects = Array.Empty<UnityEngine.Object>();

            try
            {
                ConfigureReferenceFbxCore();
            }
            finally
            {
                RestoreActiveSelection(selectionSnapshot);
            }
        }

        private static void ConfigureReferenceFbxCore()
        {
            Avatar femaleAvatar = ConfigureSourceAvatar(FemaleSourceRigPath);
            Avatar maleAvatar = ConfigureSourceAvatar(MaleSourceRigPath);

            int importedClipCount = 0;
            var packAvatarNames = new List<string>();
            foreach (string packPath in AnimationPackPaths)
            {
                ModelImporter importer = RequireImporter(packPath);
                ConfigureSharedSettings(importer);
                importer.importAnimation = true;
                ConfigureGenericAvatar(importer);
                packAvatarNames.Add(RequireValidAvatar(packPath).name);

                bool bakeRootTransform = !packPath.Contains("CombatCandidates", StringComparison.Ordinal);
                importedClipCount += ConfigureClips(importer, bakeRootTransform);
            }

            CreateReferenceCharacterPrefab(
                FemaleSourceRigPath,
                femaleAvatar,
                "PSO2NGS_ReferenceCharacter_Female200000",
                PreviewFolder + "/PSO2NGS_ReferenceCharacter_Female200000.prefab");
            CreateReferenceCharacterPrefab(
                MaleSourceRigPath,
                maleAvatar,
                "PSO2NGS_ReferenceCharacter_Male100000",
                PreviewFolder + "/PSO2NGS_ReferenceCharacter_Male100000.prefab");

            int clipsWithMotion = ValidateAnimationPacks();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[PSO2 NGS Animation Import] Male and female reference Avatars plus " +
                $"{packAvatarNames.Count} pack Avatars are valid; {importedClipCount} clips and " +
                $"two reference character prefabs are configured; {clipsWithMotion} clips contain motion. " +
                $"Packs={string.Join(", ", packAvatarNames)}");
        }

        private static (
            UnityEngine.Object SelectedObject,
            string AssetPath,
            string ObjectName,
            Type ObjectType) CaptureActiveSelection()
        {
            UnityEngine.Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return (null, string.Empty, string.Empty, null);
            }

            return (
                selectedObject,
                AssetDatabase.GetAssetPath(selectedObject),
                selectedObject.name,
                selectedObject.GetType());
        }

        private static void RestoreActiveSelection((
            UnityEngine.Object SelectedObject,
            string AssetPath,
            string ObjectName,
            Type ObjectType) snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.AssetPath))
            {
                Selection.activeObject = AssetDatabase.LoadAllAssetsAtPath(snapshot.AssetPath)
                    .FirstOrDefault(candidate =>
                        candidate.name == snapshot.ObjectName
                        && snapshot.ObjectType.IsInstanceOfType(candidate));
                return;
            }

            if (snapshot.SelectedObject != null)
            {
                Selection.activeObject = snapshot.SelectedObject;
            }
        }

        private static Avatar ConfigureSourceAvatar(string assetPath)
        {
            ModelImporter importer = RequireImporter(assetPath);
            ConfigureSharedSettings(importer);
            importer.importAnimation = false;
            ConfigureGenericAvatar(importer);
            return RequireValidAvatar(assetPath);
        }

        private static void ConfigureGenericAvatar(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
        }

        private static Avatar RequireValidAvatar(string assetPath)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Avatar>()
                .FirstOrDefault();

            if (avatar == null || !avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"PSO2 NGS Avatar is invalid after import: {assetPath}");
            }

            return avatar;
        }

        private static int ConfigureClips(ModelImporter importer, bool bakeRootTransform)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = NormalizeTakeName(clip.takeName);
                clip.loopTime = IsLoopingLocomotion(clip.name);
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = bakeRootTransform;
                clip.lockRootHeightY = bakeRootTransform;
                clip.lockRootPositionXZ = bakeRootTransform;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return clips.Length;
        }

        private static ModelImporter RequireImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter importer)
            {
                return importer;
            }

            throw new InvalidOperationException($"PSO2 NGS FBX was not found: {assetPath}");
        }

        private static void ConfigureSharedSettings(ModelImporter importer)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.bakeAxisConversion = true;
        }

        private static void CreateReferenceCharacterPrefab(
            string sourceRigPath,
            Avatar sourceAvatar,
            string instanceName,
            string prefabPath)
        {
            EnsureFolder(PreviewFolder);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceRigPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException($"PSO2 NGS model was not found: {sourceRigPath}");
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, previewScene);
                instance.name = instanceName;

                if (!instance.TryGetComponent(out Animator animator))
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.avatar = sourceAvatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = folderPath[..folderPath.LastIndexOf('/')];
            string folderName = folderPath[(folderPath.LastIndexOf('/') + 1)..];
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static int ValidateAnimationPacks()
        {
            int clipsWithMotion = 0;

            foreach (string packPath in AnimationPackPaths)
            {
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(packPath)
                    .OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    .ToArray();

                if (clips.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"PSO2 NGS animation pack contains no clips: {packPath}");
                }

                foreach (AnimationClip clip in clips)
                {
                    if (clip.length <= 0.0f)
                    {
                        throw new InvalidOperationException(
                            $"PSO2 NGS animation clip has zero length: {clip.name} ({packPath})");
                    }

                    if (ContainsMotion(clip))
                    {
                        clipsWithMotion++;
                        continue;
                    }

                    if (!KnownStaticClipNames.Contains(clip.name))
                    {
                        throw new InvalidOperationException(
                            $"PSO2 NGS animation clip contains only constant curves: " +
                            $"{clip.name} ({packPath})");
                    }
                }
            }

            return clipsWithMotion;
        }

        private static bool ContainsMotion(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2)
                {
                    continue;
                }

                float firstValue = curve.keys[0].value;
                foreach (Keyframe key in curve.keys)
                {
                    if (Mathf.Abs(key.value - firstValue) > CurveMotionEpsilon)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeTakeName(string takeName)
        {
            int separatorIndex = takeName.LastIndexOf('|');
            return separatorIndex >= 0 ? takeName[(separatorIndex + 1)..] : takeName;
        }

        private static bool IsLoopingLocomotion(string clipName)
        {
            return clipName.Contains("_idle", StringComparison.Ordinal)
                   || clipName.Contains("_mov_", StringComparison.Ordinal)
                   || clipName.Contains("_walk_", StringComparison.Ordinal)
                   || clipName.EndsWith("_lp", StringComparison.Ordinal)
                   || clipName.Contains("_swim_f", StringComparison.Ordinal)
                   || clipName.Contains("_swim_b", StringComparison.Ordinal)
                   || clipName.Contains("_swim_l", StringComparison.Ordinal)
                   || clipName.Contains("_swim_r", StringComparison.Ordinal)
                   || clipName.Contains("_glide_f", StringComparison.Ordinal)
                   || clipName.Contains("_glide_b", StringComparison.Ordinal)
                   || clipName.Contains("_glide_l", StringComparison.Ordinal)
                   || clipName.Contains("_glide_r", StringComparison.Ordinal);
        }
    }
}
