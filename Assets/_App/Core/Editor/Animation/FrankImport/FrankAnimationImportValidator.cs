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
        private const string ReportPath = FrankRoot + "/FrankHumanoidImportReport.json";
        private const string PoseAuditModelPath =
            "Assets/_Game/Models/Characters/UnityCharacter/DefaultMale.fbx";
        private const float IsolatedCurveJump = 0.75f;
        private const float IsolatedCurveNeighborBridge = 0.2f;
        private const float ConfirmedPoseJumpDegrees = 35f;
        private const float ConfirmedPoseNeighborBridgeDegrees = 20f;
        private const int MaxRecordedKeyIssuesPerClip = 32;

        [Serializable]
        private sealed class ValidationReport
        {
            public string generatedAtUtc;
            public int sourceRigCount;
            public int validSourceRigCount;
            public int invalidSourceRigCount;
            public int modelCount;
            public int validModelCount;
            public int invalidModelCount;
            public int curveKeyCount;
            public int invalidCurveKeyCount;
            public int configuredRangeViolationCount;
            public int humanoidCurveSpikeCandidateCount;
            public int confirmedPoseSpikeCount;
            public int configuredRetargetCorrectionCount;
            public int configuredRetargetCorrectionClipCount;
            public List<ModelRecord> models = new();
        }

        [Serializable]
        private sealed class ModelRecord
        {
            public string assetPath;
            public bool valid;
            public string error;
            public int clipCount;
            public int curveKeyCount;
            public int invalidCurveKeyCount;
            public int configuredRangeViolationCount;
            public int humanoidCurveSpikeCandidateCount;
            public int confirmedPoseSpikeCount;
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
            public int keyCount;
            public int invalidKeyCount;
            public int configuredRangeViolationCount;
            public int humanoidCurveSpikeCandidateCount;
            public int confirmedPoseSpikeCount;
            public List<KeyIssueRecord> keyIssues = new();
        }

        [Serializable]
        private sealed class KeyIssueRecord
        {
            public string property;
            public float time;
            public string issue;
        }

        [MenuItem("Project S/Animation/Frank/Configure Humanoid Imports")]
        public static void ConfigureAll()
        {
            var rigPaths = AssetDatabase.FindAssets("t:Model", new[] { FrankRoot + "/Rig/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(FrankAnimationImportSettings.IsGeneratedRig)
                .OrderBy(path => path)
                .ToArray();
            foreach (var rigPath in rigPaths)
                ConfigureRigImporter(rigPath);

            var paths = AssetDatabase.FindAssets("t:Model", new[] { FrankRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(FrankAnimationImportSettings.IsGeneratedClip)
                .OrderBy(path => path)
                .ToArray();

            ConfigureAndImport(paths);

            ValidateAll();
        }

        [MenuItem("Project S/Animation/Frank/Validate Humanoid Library")]
        public static void ValidateAll()
        {
            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                configuredRetargetCorrectionCount =
                    FrankHumanoidRetargetPostprocessor.CorrectionCount,
                configuredRetargetCorrectionClipCount =
                    FrankHumanoidRetargetPostprocessor.CorrectedClipCount,
            };

            var sourceRigPaths = AssetDatabase.FindAssets(
                    "t:Model", new[] { FrankRoot + "/Rig/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(FrankAnimationImportSettings.IsGeneratedRig)
                .ToArray();
            report.sourceRigCount = sourceRigPaths.Length;
            report.validSourceRigCount = sourceRigPaths.Count(path =>
            {
                var sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                return sourceAvatar != null && sourceAvatar.isValid && sourceAvatar.isHuman;
            });
            report.invalidSourceRigCount = report.sourceRigCount - report.validSourceRigCount;

            var guids = AssetDatabase.FindAssets("t:Model", new[] { FrankRoot });
            var poseAuditSource = AssetDatabase.LoadAssetAtPath<GameObject>(PoseAuditModelPath);
            var poseAuditObject = poseAuditSource != null
                ? UnityEngine.Object.Instantiate(poseAuditSource)
                : null;
            var poseAuditAnimator = poseAuditObject != null
                ? poseAuditObject.GetComponentInChildren<Animator>(true)
                : null;
            if (poseAuditObject != null)
            {
                poseAuditObject.name = "__FrankHumanoidPoseAudit";
                poseAuditObject.hideFlags = HideFlags.HideAndDontSave;
            }

            var ownsAnimationMode = false;
            try
            {
                if (poseAuditAnimator != null
                    && poseAuditAnimator.avatar != null
                    && poseAuditAnimator.avatar.isValid
                    && poseAuditAnimator.avatar.isHuman)
                {
                    if (!AnimationMode.InAnimationMode())
                    {
                        AnimationMode.StartAnimationMode();
                        ownsAnimationMode = true;
                    }
                }
                else
                {
                    poseAuditAnimator = null;
                }

                foreach (var path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
                {
                    if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("/Rig/", StringComparison.Ordinal))
                        continue;

                    report.models.Add(ValidateModel(path, poseAuditObject, poseAuditAnimator));
                }
            }
            finally
            {
                if (ownsAnimationMode && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                if (poseAuditObject != null)
                    UnityEngine.Object.DestroyImmediate(poseAuditObject);
            }

            report.modelCount = report.models.Count;
            report.validModelCount = report.models.Count(model => model.valid);
            report.invalidModelCount = report.modelCount - report.validModelCount;
            report.curveKeyCount = report.models.Sum(model => model.curveKeyCount);
            report.invalidCurveKeyCount = report.models.Sum(model => model.invalidCurveKeyCount);
            report.configuredRangeViolationCount = report.models.Sum(
                model => model.configuredRangeViolationCount);
            report.humanoidCurveSpikeCandidateCount = report.models.Sum(
                model => model.humanoidCurveSpikeCandidateCount);
            report.confirmedPoseSpikeCount = report.models.Sum(
                model => model.confirmedPoseSpikeCount);

            var absoluteReportPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ReportPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(absoluteReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);

            if (report.invalidModelCount == 0 && report.invalidSourceRigCount == 0)
            {
                Debug.Log(
                    $"Frank Humanoid validation passed: {report.validModelCount}/{report.modelCount} clips, "
                    + $"{report.validSourceRigCount}/{report.sourceRigCount} source rigs. "
                    + $"Checked {report.curveKeyCount} curve keys; "
                    + $"invalid keys: {report.invalidCurveKeyCount}; "
                    + $"configured range violations: {report.configuredRangeViolationCount}; "
                    + $"Humanoid curve candidates: {report.humanoidCurveSpikeCandidateCount}; "
                    + $"confirmed sampled pose spikes: {report.confirmedPoseSpikeCount}. "
                    + $"Report: {ReportPath}");
            }
            else
            {
                Debug.LogError(
                    $"Frank Humanoid validation failed: {report.invalidModelCount}/{report.modelCount} invalid clips, "
                    + $"{report.invalidSourceRigCount}/{report.sourceRigCount} invalid source rigs. "
                    + $"Report: {ReportPath}");
            }
        }

        private static ModelRecord ValidateModel(
            string path,
            GameObject poseAuditObject,
            Animator poseAuditAnimator)
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
                var clipRecord = new ClipRecord
                {
                    name = clip.name,
                    length = clip.length,
                    frameRate = clip.frameRate,
                    humanMotion = clip.humanMotion,
                    curveCount = AnimationUtility.GetCurveBindings(clip).Length
                                 + AnimationUtility.GetObjectReferenceCurveBindings(clip).Length,
                };
                ValidateCurveKeys(path, clip, clipRecord, poseAuditObject, poseAuditAnimator);
                record.clips.Add(clipRecord);
            }

            record.clipCount = clips.Length;
            record.curveKeyCount = record.clips.Sum(clip => clip.keyCount);
            record.invalidCurveKeyCount = record.clips.Sum(clip => clip.invalidKeyCount);
            record.configuredRangeViolationCount = record.clips.Sum(
                clip => clip.configuredRangeViolationCount);
            record.humanoidCurveSpikeCandidateCount = record.clips.Sum(
                clip => clip.humanoidCurveSpikeCandidateCount);
            record.confirmedPoseSpikeCount = record.clips.Sum(
                clip => clip.confirmedPoseSpikeCount);
            var failures = new List<string>();
            if (importer.animationType != ModelImporterAnimationType.Human)
                failures.Add("animation type is not Humanoid");
            if (FrankAnimationImportSettings.IsGeneratedClip(path))
            {
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    failures.Add("clip does not create its Avatar from the embedded source bind pose");
                var sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                    failures.Add("embedded source Avatar is missing or invalid");
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
            if (record.invalidCurveKeyCount > 0)
                failures.Add($"clip curves contain {record.invalidCurveKeyCount} invalid keys");
            if (record.configuredRangeViolationCount > 0)
                failures.Add(
                    $"clip contains {record.configuredRangeViolationCount} "
                    + "configured Humanoid range violations");
            if (record.confirmedPoseSpikeCount > 0)
                failures.Add($"clip contains {record.confirmedPoseSpikeCount} confirmed pose discontinuities");

            record.valid = failures.Count == 0;
            record.error = string.Join("; ", failures);
            return record;
        }

        private static void ValidateCurveKeys(
            string assetPath,
            AnimationClip clip,
            ClipRecord record,
            GameObject poseAuditObject,
            Animator poseAuditAnimator)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                var keys = curve.keys;
                record.keyCount += keys.Length;
                for (var index = 0; index < keys.Length; index++)
                {
                    var key = keys[index];
                    if (!IsFinite(key.time)
                        || !IsFinite(key.value)
                        || float.IsNaN(key.inTangent)
                        || float.IsNaN(key.outTangent)
                        || !IsFinite(key.inWeight)
                        || !IsFinite(key.outWeight)
                        || key.time < -0.0001f
                        || key.time > clip.length + 0.0001f
                        || index > 0 && key.time < keys[index - 1].time)
                    {
                        record.invalidKeyCount++;
                        AddKeyIssue(record, binding.propertyName, key.time, "invalid key data");
                    }
                }

                if (FrankHumanoidRetargetPostprocessor.TryGetAllowedMuscleRange(
                        assetPath,
                        binding.propertyName,
                        out var minimum,
                        out var maximum))
                {
                    foreach (var key in keys)
                    {
                        if (key.value >= minimum - 0.0001f
                            && key.value <= maximum + 0.0001f)
                        {
                            continue;
                        }

                        record.configuredRangeViolationCount++;
                        AddKeyIssue(
                            record,
                            binding.propertyName,
                            key.time,
                            $"configured Humanoid range violation: "
                            + $"{key.value:F4} outside [{minimum:F4}, {maximum:F4}]");
                    }
                }

                if (!IsHumanoidBodyCurve(binding.propertyName) || keys.Length < 3)
                    continue;

                var maximumNeighborInterval = clip.frameRate > 0f
                    ? 1.6f / clip.frameRate
                    : 0.055f;
                for (var index = 1; index < keys.Length - 1; index++)
                {
                    var previous = keys[index - 1];
                    var current = keys[index];
                    var next = keys[index + 1];
                    if (current.time - previous.time > maximumNeighborInterval
                        || next.time - current.time > maximumNeighborInterval)
                    {
                        continue;
                    }

                    var incoming = Mathf.Abs(current.value - previous.value);
                    var outgoing = Mathf.Abs(current.value - next.value);
                    var neighborBridge = Mathf.Abs(previous.value - next.value);
                    if (incoming <= IsolatedCurveJump
                        || outgoing <= IsolatedCurveJump
                        || neighborBridge >= IsolatedCurveNeighborBridge)
                    {
                        continue;
                    }

                    record.humanoidCurveSpikeCandidateCount++;
                    var poseResult = TryMeasurePoseContinuity(
                        clip,
                        binding.propertyName,
                        previous.time,
                        current.time,
                        next.time,
                        poseAuditObject,
                        poseAuditAnimator,
                        out var poseIncoming,
                        out var poseOutgoing,
                        out var poseNeighborBridge);
                    var confirmedPoseSpike = poseResult
                                             && poseIncoming > ConfirmedPoseJumpDegrees
                                             && poseOutgoing > ConfirmedPoseJumpDegrees
                                             && poseNeighborBridge
                                             < ConfirmedPoseNeighborBridgeDegrees;
                    if (confirmedPoseSpike)
                        record.confirmedPoseSpikeCount++;

                    var poseSummary = poseResult
                        ? $"sampled bone {poseIncoming:F2}/{poseOutgoing:F2}, bridge {poseNeighborBridge:F2}"
                        : "sampled bone unavailable";
                    AddKeyIssue(
                        record,
                        binding.propertyName,
                        current.time,
                        $"{(confirmedPoseSpike ? "confirmed pose spike" : "curve-only candidate")} "
                        + $"({incoming:F3}/{outgoing:F3}, bridge {neighborBridge:F3}); {poseSummary}");
                }
            }
        }

        private static bool TryMeasurePoseContinuity(
            AnimationClip clip,
            string propertyName,
            float previousTime,
            float currentTime,
            float nextTime,
            GameObject poseAuditObject,
            Animator poseAuditAnimator,
            out float incoming,
            out float outgoing,
            out float neighborBridge)
        {
            incoming = 0f;
            outgoing = 0f;
            neighborBridge = 0f;
            if (poseAuditObject == null
                || poseAuditAnimator == null
                || !TryGetHumanBodyBone(propertyName, out var humanBone))
            {
                return false;
            }

            var bone = poseAuditAnimator.GetBoneTransform(humanBone);
            if (bone == null)
                return false;

            var previousRotation = SampleBoneLocalRotation(
                poseAuditObject, bone, clip, previousTime);
            var currentRotation = SampleBoneLocalRotation(
                poseAuditObject, bone, clip, currentTime);
            var nextRotation = SampleBoneLocalRotation(
                poseAuditObject, bone, clip, nextTime);
            incoming = Quaternion.Angle(previousRotation, currentRotation);
            outgoing = Quaternion.Angle(currentRotation, nextRotation);
            neighborBridge = Quaternion.Angle(previousRotation, nextRotation);
            return true;
        }

        private static Quaternion SampleBoneLocalRotation(
            GameObject poseAuditObject,
            Transform bone,
            AnimationClip clip,
            float time)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(poseAuditObject, clip, time);
            AnimationMode.EndSampling();
            return bone.localRotation;
        }

        private static bool TryGetHumanBodyBone(string propertyName, out HumanBodyBones bone)
        {
            if (propertyName.StartsWith("Left Upper Leg ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftUpperLeg;
            else if (propertyName.StartsWith("Left Lower Leg ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftLowerLeg;
            else if (propertyName.StartsWith("Left Foot ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftFoot;
            else if (propertyName.StartsWith("Left Toes ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftToes;
            else if (propertyName.StartsWith("Right Upper Leg ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightUpperLeg;
            else if (propertyName.StartsWith("Right Lower Leg ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightLowerLeg;
            else if (propertyName.StartsWith("Right Foot ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightFoot;
            else if (propertyName.StartsWith("Right Toes ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightToes;
            else if (propertyName.StartsWith("Left Upper Arm ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftUpperArm;
            else if (propertyName.StartsWith("Left Lower Arm ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftLowerArm;
            else if (propertyName.StartsWith("Left Hand ", StringComparison.Ordinal))
                bone = HumanBodyBones.LeftHand;
            else if (propertyName.StartsWith("Right Upper Arm ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightUpperArm;
            else if (propertyName.StartsWith("Right Lower Arm ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightLowerArm;
            else if (propertyName.StartsWith("Right Hand ", StringComparison.Ordinal))
                bone = HumanBodyBones.RightHand;
            else if (propertyName.StartsWith("UpperChest ", StringComparison.Ordinal))
                bone = HumanBodyBones.UpperChest;
            else if (propertyName.StartsWith("Chest ", StringComparison.Ordinal))
                bone = HumanBodyBones.Chest;
            else if (propertyName.StartsWith("Spine ", StringComparison.Ordinal))
                bone = HumanBodyBones.Spine;
            else if (propertyName.StartsWith("Neck ", StringComparison.Ordinal))
                bone = HumanBodyBones.Neck;
            else if (propertyName.StartsWith("Head ", StringComparison.Ordinal))
                bone = HumanBodyBones.Head;
            else
            {
                bone = HumanBodyBones.LastBone;
                return false;
            }

            return true;
        }

        private static void AddKeyIssue(ClipRecord record, string property, float time, string issue)
        {
            if (record.keyIssues.Count >= MaxRecordedKeyIssuesPerClip)
                return;

            record.keyIssues.Add(new KeyIssueRecord
            {
                property = property,
                time = time,
                issue = issue,
            });
        }

        private static bool IsHumanoidBodyCurve(string propertyName)
        {
            return propertyName.StartsWith("Left ", StringComparison.Ordinal)
                   || propertyName.StartsWith("Right ", StringComparison.Ordinal)
                   || propertyName.StartsWith("Spine ", StringComparison.Ordinal)
                   || propertyName.StartsWith("Chest ", StringComparison.Ordinal)
                   || propertyName.StartsWith("UpperChest ", StringComparison.Ordinal)
                   || propertyName.StartsWith("Neck ", StringComparison.Ordinal)
                   || propertyName.StartsWith("Head ", StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ConfigureRigImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter rigImporter)
                throw new FileNotFoundException("The Frank source rig is missing.", path);

            FrankAnimationImportSettings.ApplyRig(rigImporter);
            rigImporter.sourceAvatar = null;
            rigImporter.SaveAndReimport();
        }

        private static void ConfigureAndImport(IEnumerable<string> paths)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                        continue;

                    FrankAnimationImportSettings.Apply(importer, path);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
