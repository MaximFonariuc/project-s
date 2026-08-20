using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class KurtzPelReferenceAnimationImporter
    {
        private const string RootFolder =
            "Assets/_Game/Animations/Reference/KurtzPel/MaleComplete";

        private const string ModelPath =
            RootFolder + "/Model/KurtzPel_MaleReference.fbx";

        private const string PreviewFolder = RootFolder + "/Preview";
        private const string ManifestPath = RootFolder + "/KurtzPel_AnimationManifest.csv";
        private static readonly IReadOnlyDictionary<string, int> AnimationPacks =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RootFolder + "/Movement/KurtzPel_Common_Ninja.fbx"] = 27,
                [RootFolder + "/Movement/KurtzPel_Male_Directional.fbx"] = 44,
                [RootFolder + "/Movement/KurtzPel_Male_ENFJ.fbx"] = 45,
                [RootFolder + "/Movement/KurtzPel_Male_ENTP.fbx"] = 31,
                [RootFolder + "/Movement/KurtzPel_Male_ESFP.fbx"] = 31,
                [RootFolder + "/Movement/KurtzPel_Male_ESTJ.fbx"] = 33,
                [RootFolder + "/Movement/KurtzPel_Male_INFP.fbx"] = 31,
                [RootFolder + "/Movement/KurtzPel_Male_INTJ.fbx"] = 32,
                [RootFolder + "/Movement/KurtzPel_Male_ISTJ.fbx"] = 31,
                [RootFolder + "/Movement/KurtzPel_Male_ISTP.fbx"] = 31,
                [RootFolder + "/Reactions/KurtzPel_Male_Reactions.fbx"] = 26,
                [RootFolder + "/Misc/KurtzPel_Male_Misc.fbx"] = 4,
                [RootFolder + "/Combat/KurtzPel_BlazingFist.fbx"] = 71
            };

        [MenuItem("Tools/Animation/KurtzPel/Configure Reference Pack")]
        public static void ConfigureReferencePack()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Avatar sourceAvatar = ConfigureModel();
            foreach ((string packPath, _) in AnimationPacks)
            {
                ConfigureAnimationPack(packPath, sourceAvatar);
            }

            CreateReferenceCharacterPrefab(sourceAvatar);
            int clipCount = ValidateImportedAssets();
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[KurtzPel Animation Import] Valid 86-bone reference model, " +
                $"{AnimationPacks.Count} animation FBXs and {clipCount} clips configured. " +
                "Existing Player, scenes and Animator Controllers were not changed.");
        }

        private static Avatar ConfigureModel()
        {
            ModelImporter importer = RequireImporter(ModelPath);
            ConfigureSharedSettings(importer);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();

            if (avatar == null || !avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"KurtzPel source Avatar is invalid after import: {ModelPath}");
            }

            return avatar;
        }

        private static void ConfigureAnimationPack(string packPath, Avatar sourceAvatar)
        {
            ModelImporter importer = RequireImporter(packPath);
            ConfigureSharedSettings(importer);
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = NormalizeTakeName(clip.takeName);
                clip.loopTime = IsObviousLoop(clip.name);
                clip.loopPose = clip.loopTime;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
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

        private static string NormalizeTakeName(string takeName)
        {
            int separatorIndex = takeName.LastIndexOf('|');
            return separatorIndex >= 0 && separatorIndex < takeName.Length - 1
                ? takeName[(separatorIndex + 1)..]
                : takeName;
        }

        private static bool IsObviousLoop(string clipName)
        {
            if (clipName.Contains("_Loop", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (clipName.EndsWith("_Wait", StringComparison.OrdinalIgnoreCase)
                || clipName.EndsWith("_Combat_Wait", StringComparison.OrdinalIgnoreCase)
                || clipName.EndsWith("_Tired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool hasTerminalSuffix = clipName.Contains("RunEnd", StringComparison.OrdinalIgnoreCase)
                || clipName.EndsWith("_Start", StringComparison.OrdinalIgnoreCase)
                || clipName.EndsWith("_End", StringComparison.OrdinalIgnoreCase)
                || clipName.Contains("Landing", StringComparison.OrdinalIgnoreCase)
                || clipName.Contains("Attack", StringComparison.OrdinalIgnoreCase);

            if (hasTerminalSuffix)
            {
                return false;
            }

            return clipName.Contains("_Run", StringComparison.OrdinalIgnoreCase)
                || clipName.Contains("_Walk", StringComparison.OrdinalIgnoreCase);
        }

        private static ModelImporter RequireImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter importer)
            {
                return importer;
            }

            throw new InvalidOperationException(
                $"KurtzPel FBX was not found or did not import as a model: {assetPath}");
        }

        private static void CreateReferenceCharacterPrefab(Avatar sourceAvatar)
        {
            EnsureFolder(PreviewFolder);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException(
                    $"KurtzPel reference model was not found: {ModelPath}");
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, previewScene);
                instance.name = "KurtzPel_MaleReference";

                if (!instance.TryGetComponent(out Animator animator))
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.avatar = sourceAvatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    PreviewFolder + "/KurtzPel_MaleReference.prefab");
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

            int separatorIndex = folderPath.LastIndexOf('/');
            string parentPath = folderPath[..separatorIndex];
            string folderName = folderPath[(separatorIndex + 1)..];
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static int ValidateImportedAssets()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null || model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    "KurtzPel reference model contains no skinned mesh renderer.");
            }

            var modelTransformPaths = new HashSet<string>(
                model.GetComponentsInChildren<Transform>(true)
                    .Select(transform =>
                        AnimationUtility.CalculateTransformPath(transform, model.transform)),
                StringComparer.Ordinal);

            var uniqueClipNames = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ManifestEntry> manifest = ReadManifest();
            int totalClipCount = 0;

            foreach ((string packPath, int expectedClipCount) in AnimationPacks)
            {
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(packPath)
                    .OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    .ToArray();

                if (clips.Length != expectedClipCount)
                {
                    throw new InvalidOperationException(
                        $"Unexpected KurtzPel clip count in {packPath}: " +
                        $"expected {expectedClipCount}, found {clips.Length}.");
                }

                foreach (AnimationClip clip in clips)
                {
                    EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                    if (bindings.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"KurtzPel clip contains no animation curves: {clip.name} ({packPath})");
                    }

                    string missingTransformPath = bindings
                        .Where(binding => binding.type == typeof(Transform))
                        .Select(binding => binding.path)
                        .FirstOrDefault(path => !modelTransformPaths.Contains(path));
                    if (missingTransformPath != null)
                    {
                        throw new InvalidOperationException(
                            $"KurtzPel clip '{clip.name}' targets transform path " +
                            $"'{missingTransformPath}', which is absent from the reference model.");
                    }

                    if (!uniqueClipNames.Add(clip.name))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate KurtzPel clip name: {clip.name}");
                    }

                    if (!manifest.TryGetValue(clip.name, out ManifestEntry entry))
                    {
                        throw new InvalidOperationException(
                            $"Imported KurtzPel clip is absent from the source manifest: {clip.name}");
                    }

                    float sourceSpeed = entry.FrameRate / Mathf.Max(1f, clip.frameRate);
                    float correctedDuration = clip.length / sourceSpeed;
                    if (!float.IsFinite(sourceSpeed) || sourceSpeed <= 0f ||
                        !float.IsFinite(correctedDuration) || correctedDuration <= 0f)
                    {
                        throw new InvalidOperationException(
                            $"KurtzPel clip has invalid timing metadata: {clip.name}");
                    }
                }

                totalClipCount += clips.Length;
            }

            if (totalClipCount != 437)
            {
                throw new InvalidOperationException(
                    $"Unexpected total KurtzPel clip count: {totalClipCount}");
            }

            string missingClip = manifest.Keys.FirstOrDefault(name => !uniqueClipNames.Contains(name));
            if (missingClip != null)
            {
                throw new InvalidOperationException(
                    $"KurtzPel manifest clip was not imported: {missingClip}");
            }

            return totalClipCount;
        }

        private static Dictionary<string, ManifestEntry> ReadManifest()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"KurtzPel animation manifest was not found: {ManifestPath}");
            }

            var result = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
            string[] lines = source.text.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] columns = lines[i].Split(',');
                if (columns.Length < 5 ||
                    !int.TryParse(columns[2], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int frames) ||
                    !float.TryParse(columns[3], NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float frameRate) ||
                    frames <= 0 || frameRate <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Invalid KurtzPel manifest row {i + 1}: '{lines[i]}'.");
                }

                result.Add(columns[1], new ManifestEntry(frames, frameRate, columns[4]));
            }

            if (result.Count != 437)
            {
                throw new InvalidOperationException(
                    $"KurtzPel manifest contains {result.Count} clips instead of 437.");
            }

            return result;
        }

        private readonly struct ManifestEntry
        {
            public int Frames { get; }
            public float FrameRate { get; }
            public string Source { get; }

            public ManifestEntry(int frames, float frameRate, string source)
            {
                Frames = frames;
                FrameRate = frameRate;
                Source = source;
            }
        }
    }
}
