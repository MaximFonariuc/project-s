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
        private const string ReportPath =
            FrankHumanoidClipBaker.SourceRoot + "FrankHumanoidBakeReport.json";
        private const float MuscleLimit = 1.001f;
        private const float MuscleRateLimit = 18.05f;
        private static readonly HashSet<string> MuscleNames = new(
            Enumerable.Range(0, HumanTrait.MuscleCount)
                .Select(index => HumanTrait.MuscleName[index]),
            StringComparer.Ordinal);

        [Serializable]
        private sealed class ValidationReport
        {
            public string generatedAtUtc;
            public string targetModel;
            public int sourceCount;
            public int outputCount;
            public int validCount;
            public int invalidCount;
            public int overrideSourceReferenceCount;
            public float maximumAbsoluteMuscle;
            public float maximumMuscleRate;
            public List<ClipFailure> failures = new();
        }

        [Serializable]
        private sealed class ClipFailure
        {
            public string sourcePath;
            public string outputPath;
            public List<string> issues = new();
        }

        [MenuItem("Project S/Animation/Frank/Validate Baked Humanoid Library")]
        internal static void ValidateAll()
        {
            var report = BuildReport();
            var absoluteReportPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ReportPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(absoluteReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);

            var summary =
                $"Frank Humanoid bake validation: {report.validCount}/{report.sourceCount} valid; "
                + $"outputs={report.outputCount}; invalid={report.invalidCount}; "
                + $"direct Generic override references={report.overrideSourceReferenceCount}; "
                + $"max muscle={report.maximumAbsoluteMuscle:F4}; "
                + $"max muscle rate={report.maximumMuscleRate:F3}. Report: {ReportPath}";
            if (report.invalidCount == 0
                && report.outputCount == report.sourceCount
                && report.overrideSourceReferenceCount == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        internal static string ValidateForAutomation()
        {
            var report = BuildReport();
            return $"valid={report.validCount}/{report.sourceCount}; outputs={report.outputCount}; "
                   + $"invalid={report.invalidCount}; sourceOverrides={report.overrideSourceReferenceCount}; "
                   + $"maxMuscle={report.maximumAbsoluteMuscle:F4}; "
                   + $"maxRate={report.maximumMuscleRate:F3}";
        }

        private static ValidationReport BuildReport()
        {
            var sourcePaths = AssetDatabase.FindAssets(
                    "t:Model", new[] { FrankHumanoidClipBaker.SourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(FrankHumanoidClipBaker.IsSourceClip)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                targetModel = FrankHumanoidClipBaker.TargetModelPath,
                sourceCount = sourcePaths.Length,
                outputCount = AssetDatabase.FindAssets(
                        "t:AnimationClip", new[] { FrankHumanoidClipBaker.OutputRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
            };

            var targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FrankHumanoidClipBaker.TargetModelPath);
            var targetObject = targetPrefab != null
                ? UnityEngine.Object.Instantiate(targetPrefab)
                : null;
            if (targetObject != null)
                targetObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (var sourcePath in sourcePaths)
                    ValidatePair(sourcePath, targetObject, report);
            }
            finally
            {
                if (targetObject != null)
                    UnityEngine.Object.DestroyImmediate(targetObject);
            }

            report.invalidCount = report.failures.Count;
            report.validCount = report.sourceCount - report.invalidCount;
            report.overrideSourceReferenceCount = CountDirectSourceOverrideReferences();
            return report;
        }

        private static void ValidatePair(
            string sourcePath,
            GameObject targetObject,
            ValidationReport report)
        {
            var outputPath = FrankHumanoidClipBaker.GetOutputPath(sourcePath);
            var failure = new ClipFailure
            {
                sourcePath = sourcePath,
                outputPath = outputPath,
            };

            if (AssetImporter.GetAtPath(sourcePath) is not ModelImporter importer
                || importer.animationType != ModelImporterAnimationType.Generic)
            {
                failure.issues.Add("Source FBX is not imported as Generic.");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (clip == null)
            {
                failure.issues.Add("Mirrored Humanoid output is missing.");
            }
            else
            {
                ValidateClip(clip, targetObject, report, failure);
            }

            if (failure.issues.Count > 0)
                report.failures.Add(failure);
        }

        private static void ValidateClip(
            AnimationClip clip,
            GameObject targetObject,
            ValidationReport report,
            ClipFailure failure)
        {
            if (!clip.humanMotion)
                failure.issues.Add("Output is not Humanoid motion.");
            if (clip.legacy)
                failure.issues.Add("Output is marked Legacy.");
            if (!float.IsFinite(clip.length) || clip.length <= 0f)
                failure.issues.Add("Output length is invalid.");
            if (!float.IsFinite(clip.frameRate) || clip.frameRate < 1f || clip.frameRate > 60.01f)
                failure.issues.Add("Output frame rate is outside 1-60 FPS.");

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                var isMuscle = MuscleNames.Contains(binding.propertyName);
                var keys = curve.keys;
                for (var index = 0; index < keys.Length; index++)
                {
                    var key = keys[index];
                    if (!float.IsFinite(key.time) || !float.IsFinite(key.value))
                    {
                        AddOnce(failure.issues, $"Non-finite key in {binding.propertyName}.");
                        continue;
                    }

                    if (!isMuscle)
                        continue;
                    var absolute = Mathf.Abs(key.value);
                    report.maximumAbsoluteMuscle = Mathf.Max(
                        report.maximumAbsoluteMuscle,
                        absolute);
                    if (absolute > MuscleLimit)
                        AddOnce(failure.issues, $"Muscle range exceeded in {binding.propertyName}.");

                    if (index == 0)
                        continue;
                    var duration = key.time - keys[index - 1].time;
                    if (duration <= 0f)
                        continue;
                    var rate = Mathf.Abs(key.value - keys[index - 1].value) / duration;
                    report.maximumMuscleRate = Mathf.Max(report.maximumMuscleRate, rate);
                    if (rate > MuscleRateLimit)
                        AddOnce(failure.issues, $"Muscle spike in {binding.propertyName}.");
                }
            }

            if (targetObject == null)
            {
                AddOnce(failure.issues, "Humanoid audit target is missing.");
                return;
            }

            foreach (var normalizedTime in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                clip.SampleAnimation(targetObject, clip.length * normalizedTime);
                foreach (var transform in targetObject.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsFinite(transform.position) || !IsFinite(transform.rotation))
                    {
                        AddOnce(failure.issues, $"Non-finite sampled pose at {normalizedTime:F2}.");
                        return;
                    }
                }
            }
        }

        private static int CountDirectSourceOverrideReferences()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:AnimatorOverrideController", new[] { "Assets/_Game" }))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (controller == null)
                    continue;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                count += overrides.Count(pair =>
                    pair.Value != null
                    && FrankHumanoidClipBaker.IsSourceClip(
                        AssetDatabase.GetAssetPath(pair.Value)));
            }
            return count;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                   && float.IsFinite(value.y)
                   && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x)
                   && float.IsFinite(value.y)
                   && float.IsFinite(value.z)
                   && float.IsFinite(value.w);
        }

        private static void AddOnce(ICollection<string> issues, string issue)
        {
            if (!issues.Contains(issue))
                issues.Add(issue);
        }
    }
}
