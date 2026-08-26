using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal static class FrankAnimationImportValidator
    {
        private const string FrankRoot = "Assets/_Game/Animations/Frank";
        private const string SharedAvatarPath = FrankAnimationImportSettings.SharedAvatarPath;
        private const string ReportPath = FrankRoot + "/FrankHumanoidImportReport.json";

        [Serializable]
        private sealed class ValidationReport
        {
            public string generatedAtUtc;
            public string sharedAvatar;
            public bool sharedAvatarIsValid;
            public bool sharedAvatarIsHuman;
            public int modelCount;
            public int validModelCount;
            public int invalidModelCount;
            public List<ModelRecord> models = new();
        }

        [Serializable]
        private sealed class ModelRecord
        {
            public string assetPath;
            public bool valid;
            public string error;
            public int clipCount;
            public List<ClipRecord> clips = new();
        }

        [Serializable]
        private sealed class ClipRecord
        {
            public string name;
            public float length;
            public float frameRate;
            public bool humanMotion;
            public int curveCount;
        }

        [MenuItem("Project S/Animation/Frank/Configure Humanoid Imports")]
        public static void ConfigureAll()
        {
            if (AssetImporter.GetAtPath(SharedAvatarPath) is not ModelImporter rigImporter)
                throw new FileNotFoundException("The generated shared Frank rig is missing.", SharedAvatarPath);

            FrankAnimationImportSettings.ApplySharedRig(rigImporter);
            rigImporter.SaveAndReimport();

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(SharedAvatarPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException($"A valid Humanoid avatar is required at '{SharedAvatarPath}'.");

            var paths = AssetDatabase.FindAssets("t:Model", new[] { FrankRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(FrankAnimationImportSettings.IsGeneratedClip)
                .OrderBy(path => path)
                .ToArray();

            // The first pass establishes a valid Humanoid Avatar binding. The second
            // pass can then read Unity's default take and persist the semantic clip
            // name/loop settings; invalid or Generic imports expose no usable take.
            ConfigureAndImport(paths, avatar);
            ConfigureAndImport(paths, avatar);

            ValidateAll();
        }

        [MenuItem("Project S/Animation/Frank/Validate Humanoid Library")]
        public static void ValidateAll()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(SharedAvatarPath);
            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                sharedAvatar = SharedAvatarPath,
                sharedAvatarIsValid = avatar != null && avatar.isValid,
                sharedAvatarIsHuman = avatar != null && avatar.isHuman,
            };

            var guids = AssetDatabase.FindAssets("t:Model", new[] { FrankRoot });
            foreach (var path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/Rig/", StringComparison.Ordinal))
                    continue;

                report.models.Add(ValidateModel(path, avatar));
            }

            report.modelCount = report.models.Count;
            report.validModelCount = report.models.Count(model => model.valid);
            report.invalidModelCount = report.modelCount - report.validModelCount;

            var absoluteReportPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ReportPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(absoluteReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);

            if (report.invalidModelCount == 0)
            {
                Debug.Log(
                    $"Frank Humanoid validation passed: {report.validModelCount}/{report.modelCount} FBX models. "
                    + $"Report: {ReportPath}");
            }
            else
            {
                Debug.LogError(
                    $"Frank Humanoid validation failed for {report.invalidModelCount}/{report.modelCount} FBX models. "
                    + $"Report: {ReportPath}");
            }
        }

        private static ModelRecord ValidateModel(string path, Avatar expectedAvatar)
        {
            var record = new ModelRecord { assetPath = path };
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                record.error = "Asset is not handled by ModelImporter.";
                return record;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            foreach (var clip in clips)
            {
                record.clips.Add(new ClipRecord
                {
                    name = clip.name,
                    length = clip.length,
                    frameRate = clip.frameRate,
                    humanMotion = clip.humanMotion,
                    curveCount = AnimationUtility.GetCurveBindings(clip).Length
                                 + AnimationUtility.GetObjectReferenceCurveBindings(clip).Length,
                });
            }

            record.clipCount = clips.Length;
            var failures = new List<string>();
            if (importer.animationType != ModelImporterAnimationType.Human)
                failures.Add("animation type is not Humanoid");
            if (FrankAnimationImportSettings.IsGeneratedClip(path))
            {
                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
                    failures.Add("avatar is not copied from the shared rig");
                if (expectedAvatar == null || importer.sourceAvatar != expectedAvatar)
                    failures.Add("shared source avatar is missing or different");
                if (clips.Length == 1
                    && clips[0].name != Path.GetFileNameWithoutExtension(path))
                {
                    failures.Add("clip name does not match the FBX file name");
                }
                if (clips.Any(clip => clip.frameRate <= 0f || clip.frameRate > 60.01f))
                    failures.Add("clip frame rate is outside the supported 1-60 FPS range");
            }
            if (clips.Length != 1)
                failures.Add($"expected one clip, found {clips.Length}");
            if (clips.Any(clip => !clip.humanMotion || clip.length <= 0f))
                failures.Add("clip is empty or is not Humanoid motion");
            if (record.clips.Any(clip => clip.curveCount == 0))
                failures.Add("clip contains no animation curves");

            record.valid = failures.Count == 0;
            record.error = string.Join("; ", failures);
            return record;
        }

        private static void ConfigureAndImport(IEnumerable<string> paths, Avatar avatar)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                        continue;

                    FrankAnimationImportSettings.Apply(importer, path, avatar);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
