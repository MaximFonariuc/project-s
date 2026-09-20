using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class CuratedAnimationClipBuilder
    {
        private const string MenuPath = "Tools/Animation/Rebuild Curated Clips";
        private const string Shared = "Assets/_Game/Animations/Character/Shared/";
        private const string Legacy = "Assets/_Game/Animations/Character/2. Basic/";
        private const string Attack =
            "Assets/_Game/Animations/Clazy/Animations/P4_CLazyAttack/";
        private const string Weapons =
            "Assets/_Game/Animations/Character/Weapons/";

        private readonly struct CuratedClip
        {
            public string SourceAsset { get; }
            public string SourceClip { get; }
            public string TargetPath { get; }
            public bool Loop { get; }

            public CuratedClip(
                string sourceAsset,
                string sourceClip,
                string targetPath,
                bool loop)
            {
                SourceAsset = sourceAsset;
                SourceClip = sourceClip;
                TargetPath = targetPath;
                Loop = loop;
            }
        }

        private static readonly CuratedClip[] Manifest =
        {
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpIdle.fbx",
                "JumpIdle",
                Shared + "Jump/Jump_Rise_Idle.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpWalking.fbx",
                "JumpWalking",
                Shared + "Jump/Jump_Rise_Walk.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpRunning.fbx",
                "JumpRunning",
                Shared + "Jump/Jump_Rise_Run.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpSprinting.fbx",
                "JumpSprinting",
                Shared + "Jump/Jump_Rise_Sprint.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Falling/FallShort.fbx",
                "FallShort",
                Shared + "Jump/Jump_Fall_Short.anim",
                true),
            new CuratedClip(
                Legacy + "Jump/Falling/FallLarge.fbx",
                "FallLarge",
                Shared + "Jump/Jump_Fall_Long.anim",
                true),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpIdle.fbx",
                "LandDefault",
                Shared + "Jump/Jump_Land_Idle.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpWalking.fbx",
                "LandWalking",
                Shared + "Jump/Jump_Land_Walk.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpRunning.fbx",
                "LandRunning",
                Shared + "Jump/Jump_Land_Run.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpSprinting.fbx",
                "LandSprinting",
                Shared + "Jump/Jump_Land_Sprint.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandSoft.fbx",
                "LandSoft",
                Shared + "Jump/Jump_Land_Soft.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandMedium.fbx",
                "LandMedium",
                Shared + "Jump/Jump_Land_Medium.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandHard.fbx",
                "LandHard",
                Shared + "Jump/Jump_Land_Hard.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/AirDash/CLazy@Esc_BoostDash_Air_Front.FBX",
                "AirDashFront",
                Shared + "Jump/Jump_AirDash_F.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/AirDash/AirDashBack.FBX",
                "AirDashBack",
                Shared + "Jump/Jump_AirDash_B.anim",
                false),
            new CuratedClip(
                Attack + "Attack_Punch_Run/CLazy@Attack_Hand_PwR.FBX",
                "Attack_Hand_PwR",
                Weapons + "Unarmed/Ground/Unarmed_Special.anim",
                false),
            new CuratedClip(
                Attack + "Attack_Punch_Run/CLazy@Attack_Hand_Ready.FBX",
                "Attack_Hand_Ready",
                Weapons + "Unarmed/Defense/Unarmed_Guard.anim",
                false)
        };

        [MenuItem(MenuPath)]
        private static void Rebuild()
        {
            var report = new StringBuilder();
            int created = 0;
            int refreshed = 0;
            int failed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < Manifest.Length; i++)
                {
                    CuratedClip entry = Manifest[i];
                    if (!TryLoadSource(entry, out AnimationClip source, out string error))
                    {
                        failed++;
                        report.AppendLine($"  FAIL {entry.TargetPath}: {error}");
                        continue;
                    }

                    bool existed = Build(entry, source);
                    if (existed)
                    {
                        refreshed++;
                    }
                    else
                    {
                        created++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int repaired = ApplyRepairs(report);
            int smoothed = ApplySplices(report);
            int limited = ApplyTwistLimits(report);
            int aligned = ApplyRecoveryAlignments(report);
            int stitched = ApplyStitches(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Insert(
                0,
                $"[Curated Clips] created {created}, refreshed {refreshed}, " +
                $"repaired {repaired}, smoothed {smoothed}, twist-limited {limited}, " +
                $"aligned {aligned}, stitched {stitched}, failed {failed}\n");
            if (failed > 0)
            {
                Debug.LogError(report.ToString());
            }
            else
            {
                Debug.Log(report.ToString());
            }
        }

        private static bool TryLoadSource(
            CuratedClip entry,
            out AnimationClip source,
            out string error)
        {
            source = null;
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(entry.SourceAsset);
            if (assets == null || assets.Length == 0)
            {
                error = $"source asset missing ({entry.SourceAsset})";
                return false;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip &&
                    string.Equals(clip.name, entry.SourceClip, StringComparison.Ordinal))
                {
                    source = clip;
                    error = null;
                    return true;
                }
            }

            error = $"clip '{entry.SourceClip}' not found in source";
            return false;
        }

        private static bool Build(CuratedClip entry, AnimationClip source)
        {
            string name = Path.GetFileNameWithoutExtension(entry.TargetPath);
            AnimationClip target =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.TargetPath);
            bool existed = target != null;
            if (existed)
            {
                EditorUtility.CopySerialized(source, target);
            }
            else
            {
                string directory = Path.GetDirectoryName(entry.TargetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                target = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(target, entry.TargetPath);
            }

            target.name = name;
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(target);
            settings.loopTime = entry.Loop;
            AnimationUtility.SetAnimationClipSettings(target, settings);
            EditorUtility.SetDirty(target);
            return existed;
        }

        private readonly struct CurveRepair
        {
            public string ClipPath { get; }
            public string PropertyName { get; }
            public float StartSeconds { get; }
            public float EndSeconds { get; }

            public CurveRepair(
                string clipPath,
                string propertyName,
                float startSeconds,
                float endSeconds)
            {
                ClipPath = clipPath;
                PropertyName = propertyName;
                StartSeconds = startSeconds;
                EndSeconds = endSeconds;
            }
        }

        private static readonly CurveRepair[] Repairs =
        {
            new CurveRepair(
                Shared + "Dodge/Dodge_Roll_B.anim",
                "Right Upper Leg Twist In-Out",
                0.5667f,
                0.6333f),
            new CurveRepair(
                Shared + "Dodge/Dodge_Roll_B.anim",
                "Right Lower Leg Twist In-Out",
                0.5667f,
                0.6333f)
        };

        private readonly struct SpliceSmoothing
        {
            public string ClipPath { get; }
            public float StartSeconds { get; }
            public float EndSeconds { get; }
            public float StepThreshold { get; }

            public SpliceSmoothing(
                string clipPath,
                float startSeconds,
                float endSeconds,
                float stepThreshold)
            {
                ClipPath = clipPath;
                StartSeconds = startSeconds;
                EndSeconds = endSeconds;
                StepThreshold = stepThreshold;
            }
        }

        private static readonly SpliceSmoothing[] Splices =
        {
            new SpliceSmoothing(
                Shared + "Jump/Jump_AirDash_F.anim",
                0.167f,
                0.300f,
                0.6f)
        };

        private readonly struct TwistLimit
        {
            public string ClipPath { get; }
            public float PeakDegrees { get; }

            public TwistLimit(string clipPath, float peakDegrees)
            {
                ClipPath = clipPath;
                PeakDegrees = peakDegrees;
            }
        }

        private static readonly TwistLimit[] TwistLimits =
        {
            new TwistLimit(Shared + "Jump/Jump_AirDash_F.anim", 20.0f),
            new TwistLimit(Shared + "Jump/Jump_AirDash_B.anim", 20.0f)
        };

        private const string RootRotationPrefix = "RootQ.";
        private const float TwistTolerance = 1.0f;

        private readonly struct RecoveryAlignment
        {
            public string ClipPath { get; }
            public string ReferencePath { get; }

            public RecoveryAlignment(string clipPath, string referencePath)
            {
                ClipPath = clipPath;
                ReferencePath = referencePath;
            }
        }

        private static readonly RecoveryAlignment[] RecoveryAlignments =
        {
            new RecoveryAlignment(
                Shared + "Dodge/Dodge_Roll_B.anim",
                Shared + "Dodge/Dodge_Roll_F.anim")
        };

        private const string RootHeightProperty = "RootT.y";
        private const float RecoveryLowLevel = 0.55f;
        private const float RecoveryUprightLevel = 0.80f;
        private const float RecoveryTolerance = 0.01f;
        private const float ResampleStep = 1.0f / 60.0f;

        private readonly struct PoseStitch
        {
            public string ClipPath { get; }
            public bool AtTail { get; }
            public float WindowSeconds { get; }

            public PoseStitch(string clipPath, bool atTail, float windowSeconds)
            {
                ClipPath = clipPath;
                AtTail = atTail;
                WindowSeconds = windowSeconds;
            }
        }

        private const string RestPosePath = Shared + "Locomotion/Loco_Idle.anim";
        private const float KeyTimeTolerance = 0.0005f;

        private static readonly PoseStitch[] Stitches =
        {
            new PoseStitch(Shared + "Jump/Jump_Rise_Idle.anim", false, 0.12f),
            new PoseStitch(Shared + "Jump/Jump_Land_Idle.anim", true, 0.20f),
            new PoseStitch(Shared + "Jump/Jump_Land_Soft.anim", true, 0.20f),
            new PoseStitch(Shared + "Jump/Jump_Land_Medium.anim", true, 0.20f),
            new PoseStitch(Shared + "Jump/Jump_Land_Hard.anim", true, 0.20f),
            new PoseStitch(Shared + "Dodge/Dodge_Roll_F.anim", false, 0.12f),
            new PoseStitch(Shared + "Dodge/Dodge_Roll_B.anim", false, 0.12f)
        };

        private static int ApplyRepairs(StringBuilder report)
        {
            int repaired = 0;
            for (int i = 0; i < Repairs.Length; i++)
            {
                CurveRepair repair = Repairs[i];
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(repair.ClipPath);
                if (clip == null)
                {
                    report.AppendLine($"  FAIL repair: missing {repair.ClipPath}");
                    continue;
                }

                if (!TryRepairCurveSpan(clip, repair, out string error))
                {
                    report.AppendLine(
                        $"  FAIL repair {repair.PropertyName}: {error}");
                    continue;
                }

                EditorUtility.SetDirty(clip);
                repaired++;
            }

            return repaired;
        }

        private static bool TryRepairCurveSpan(
            AnimationClip clip,
            CurveRepair repair,
            out string error)
        {
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (!string.Equals(
                        binding.propertyName,
                        repair.PropertyName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 3)
                {
                    error = "curve is empty";
                    return false;
                }

                if (!TryLinearizeSpan(curve, repair.StartSeconds, repair.EndSeconds))
                {
                    error = "span does not bracket any key";
                    return false;
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
                error = null;
                return true;
            }

            error = "property not found";
            return false;
        }

        private static bool TryLinearizeSpan(
            AnimationCurve curve,
            float startSeconds,
            float endSeconds)
        {
            int first = -1;
            int last = -1;
            for (int k = 0; k < curve.length; k++)
            {
                float time = curve[k].time;
                if (time <= startSeconds + KeyTimeTolerance)
                {
                    first = k;
                }

                if (last < 0 && time >= endSeconds - KeyTimeTolerance)
                {
                    last = k;
                }
            }

            if (first < 0 || last <= first + 1)
            {
                return false;
            }

            float startValue = curve[first].value;
            float endValue = curve[last].value;
            float startTime = curve[first].time;
            float endTime = curve[last].time;
            for (int k = first + 1; k < last; k++)
            {
                Keyframe key = curve[k];
                float weight = (key.time - startTime) / (endTime - startTime);
                key.value = Mathf.Lerp(startValue, endValue, weight);
                curve.MoveKey(k, key);
            }

            for (int k = first; k <= last; k++)
            {
                curve.SmoothTangents(k, 0.0f);
            }

            return true;
        }

        private static int ApplySplices(StringBuilder report)
        {
            int smoothed = 0;
            for (int i = 0; i < Splices.Length; i++)
            {
                SpliceSmoothing splice = Splices[i];
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(splice.ClipPath);
                if (clip == null)
                {
                    report.AppendLine($"  FAIL splice: missing {splice.ClipPath}");
                    continue;
                }

                int curves = 0;
                foreach (EditorCurveBinding binding in
                         AnimationUtility.GetCurveBindings(clip))
                {
                    if (IsRootBinding(binding.propertyName))
                    {
                        continue;
                    }

                    AnimationCurve curve =
                        AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null ||
                        !HasStep(curve, splice) ||
                        !TryLinearizeSpan(
                            curve,
                            splice.StartSeconds,
                            splice.EndSeconds))
                    {
                        continue;
                    }

                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                    curves++;
                }

                if (curves == 0)
                {
                    continue;
                }

                EditorUtility.SetDirty(clip);
                smoothed++;
            }

            return smoothed;
        }

        private static bool HasStep(AnimationCurve curve, SpliceSmoothing splice)
        {
            for (int k = 1; k < curve.length; k++)
            {
                float time = curve[k].time;
                if (time < splice.StartSeconds || time > splice.EndSeconds)
                {
                    continue;
                }

                if (Mathf.Abs(curve[k].value - curve[k - 1].value) >
                    splice.StepThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ApplyTwistLimits(StringBuilder report)
        {
            int limited = 0;
            for (int i = 0; i < TwistLimits.Length; i++)
            {
                TwistLimit limit = TwistLimits[i];
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(limit.ClipPath);
                if (clip == null)
                {
                    report.AppendLine($"  FAIL twist: missing {limit.ClipPath}");
                    continue;
                }

                if (!TryLimitRootTwist(clip, limit.PeakDegrees, out string error))
                {
                    report.AppendLine($"  FAIL twist {limit.ClipPath}: {error}");
                    continue;
                }

                EditorUtility.SetDirty(clip);
                limited++;
            }

            return limited;
        }

        private static bool TryLimitRootTwist(
            AnimationClip clip,
            float peakDegrees,
            out string error)
        {
            var bindings = new EditorCurveBinding[4];
            var curves = new AnimationCurve[4];
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith(
                        RootRotationPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int index = "xyzw".IndexOf(binding.propertyName[^1]);
                if (index < 0)
                {
                    continue;
                }

                bindings[index] = binding;
                curves[index] = AnimationUtility.GetEditorCurve(clip, binding);
            }

            for (int i = 0; i < 4; i++)
            {
                if (curves[i] == null || curves[i].length == 0)
                {
                    error = "root rotation curves are missing";
                    return false;
                }
            }

            int count = Mathf.CeilToInt(clip.length / ResampleStep) + 1;
            var times = new float[count];
            var twists = new Quaternion[count];
            var swings = new Quaternion[count];
            Quaternion reference = Quaternion.identity;
            float peak = 0.0f;
            for (int k = 0; k < count; k++)
            {
                times[k] = Mathf.Min(k * ResampleStep, clip.length);
                var rotation = new Quaternion(
                    curves[0].Evaluate(times[k]),
                    curves[1].Evaluate(times[k]),
                    curves[2].Evaluate(times[k]),
                    curves[3].Evaluate(times[k]));
                rotation.Normalize();
                if (k == 0)
                {
                    reference = rotation;
                }

                Quaternion delta = rotation * Quaternion.Inverse(reference);
                var twist = new Quaternion(0.0f, delta.y, 0.0f, delta.w);
                twist.Normalize();
                twists[k] = twist;
                swings[k] = delta * Quaternion.Inverse(twist);
                peak = Mathf.Max(peak, Quaternion.Angle(Quaternion.identity, twist));
            }

            if (peak <= peakDegrees + TwistTolerance)
            {
                error = null;
                return true;
            }

            float scale = peakDegrees / peak;
            var damped = new AnimationCurve[4];
            for (int i = 0; i < 4; i++)
            {
                damped[i] = new AnimationCurve();
            }

            Quaternion previous = reference;
            for (int k = 0; k < count; k++)
            {
                Quaternion limited = swings[k] *
                    Quaternion.Slerp(Quaternion.identity, twists[k], scale);
                Quaternion rotation = limited * reference;
                if (Quaternion.Dot(previous, rotation) < 0.0f)
                {
                    rotation = new Quaternion(
                        -rotation.x,
                        -rotation.y,
                        -rotation.z,
                        -rotation.w);
                }

                previous = rotation;
                damped[0].AddKey(new Keyframe(times[k], rotation.x));
                damped[1].AddKey(new Keyframe(times[k], rotation.y));
                damped[2].AddKey(new Keyframe(times[k], rotation.z));
                damped[3].AddKey(new Keyframe(times[k], rotation.w));
            }

            for (int i = 0; i < 4; i++)
            {
                for (int k = 0; k < damped[i].length; k++)
                {
                    damped[i].SmoothTangents(k, 0.0f);
                }

                AnimationUtility.SetEditorCurve(clip, bindings[i], damped[i]);
            }

            error = null;
            return true;
        }

        private static int ApplyRecoveryAlignments(StringBuilder report)
        {
            int aligned = 0;
            for (int i = 0; i < RecoveryAlignments.Length; i++)
            {
                RecoveryAlignment alignment = RecoveryAlignments[i];
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(alignment.ClipPath);
                AnimationClip reference =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(alignment.ReferencePath);
                if (clip == null || reference == null)
                {
                    report.AppendLine($"  FAIL align: missing {alignment.ClipPath}");
                    continue;
                }

                if (!TryFindRecovery(clip, out float anchor, out float upright) ||
                    !TryFindRecovery(reference, out _, out float referenceUpright))
                {
                    report.AppendLine(
                        $"  FAIL align {alignment.ClipPath}: no {RootHeightProperty} profile");
                    continue;
                }

                float target = referenceUpright / reference.length * clip.length;
                if (Mathf.Abs(target - upright) < RecoveryTolerance ||
                    target <= anchor + RecoveryTolerance ||
                    target >= clip.length - RecoveryTolerance)
                {
                    continue;
                }

                var warp = new AnimationCurve();
                warp.AddKey(new Keyframe(0.0f, 0.0f));
                warp.AddKey(new Keyframe(anchor, anchor));
                warp.AddKey(new Keyframe(target, upright));
                warp.AddKey(new Keyframe(clip.length, clip.length));
                for (int k = 0; k < warp.length; k++)
                {
                    warp.SmoothTangents(k, 0.0f);
                }

                Resample(clip, warp);
                EditorUtility.SetDirty(clip);
                aligned++;
            }

            return aligned;
        }

        private static bool TryFindRecovery(
            AnimationClip clip,
            out float lowExit,
            out float upright)
        {
            lowExit = 0.0f;
            upright = 0.0f;
            AnimationCurve height = null;
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (string.Equals(
                        binding.propertyName,
                        RootHeightProperty,
                        StringComparison.Ordinal))
                {
                    height = AnimationUtility.GetEditorCurve(clip, binding);
                }
            }

            if (height == null || height.length == 0)
            {
                return false;
            }

            const int samples = 480;
            float lowest = float.MaxValue;
            float lowestTime = 0.0f;
            for (int i = 0; i <= samples; i++)
            {
                float time = clip.length * i / samples;
                float value = height.Evaluate(time);
                if (value < lowest)
                {
                    lowest = value;
                    lowestTime = time;
                }
            }

            for (int i = 0; i <= samples; i++)
            {
                float time = clip.length * i / samples;
                if (time < lowestTime)
                {
                    continue;
                }

                float value = height.Evaluate(time);
                if (lowExit <= 0.0f && value >= RecoveryLowLevel)
                {
                    lowExit = time;
                }

                if (lowExit > 0.0f && value >= RecoveryUprightLevel)
                {
                    upright = time;
                    return true;
                }
            }

            return false;
        }

        private static void Resample(AnimationClip clip, AnimationCurve warp)
        {
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
                if (source == null || source.length == 0)
                {
                    continue;
                }

                var resampled = new AnimationCurve();
                for (float time = 0.0f;
                     time <= clip.length + 0.0001f;
                     time += ResampleStep)
                {
                    float sample = Mathf.Min(time, clip.length);
                    resampled.AddKey(
                        new Keyframe(sample, source.Evaluate(warp.Evaluate(sample))));
                }

                for (int k = 0; k < resampled.length; k++)
                {
                    resampled.SmoothTangents(k, 0.0f);
                }

                AnimationUtility.SetEditorCurve(clip, binding, resampled);
            }
        }

        private static int ApplyStitches(StringBuilder report)
        {
            AnimationClip rest =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(RestPosePath);
            if (rest == null)
            {
                report.AppendLine($"  FAIL stitch: rest pose missing ({RestPosePath})");
                return 0;
            }

            var restPose = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(rest))
            {
                if (IsRootBinding(binding.propertyName))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(rest, binding);
                if (curve != null)
                {
                    restPose[binding.propertyName] = curve.Evaluate(0.0f);
                }
            }

            int stitched = 0;
            for (int i = 0; i < Stitches.Length; i++)
            {
                PoseStitch stitch = Stitches[i];
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(stitch.ClipPath);
                if (clip == null)
                {
                    report.AppendLine($"  FAIL stitch: {stitch.ClipPath} missing");
                    continue;
                }

                StitchToRestPose(clip, restPose, stitch);
                EditorUtility.SetDirty(clip);
                stitched++;
            }

            return stitched;
        }

        private static bool IsRootBinding(string propertyName)
        {
            return propertyName.StartsWith("Motion", StringComparison.Ordinal) ||
                   propertyName.EndsWith("T.x", StringComparison.Ordinal) ||
                   propertyName.EndsWith("T.y", StringComparison.Ordinal) ||
                   propertyName.EndsWith("T.z", StringComparison.Ordinal);
        }

        private static void StitchToRestPose(
            AnimationClip clip,
            IReadOnlyDictionary<string, float> restPose,
            PoseStitch stitch)
        {
            float length = clip.length;
            float window = Mathf.Min(stitch.WindowSeconds, length * 0.5f);
            if (window <= 0.0f)
            {
                return;
            }

            float boundary = stitch.AtTail ? length : 0.0f;
            const float step = 1.0f / 30.0f;

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (IsRootBinding(binding.propertyName) ||
                    !restPose.TryGetValue(binding.propertyName, out float restValue))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                float delta = restValue - curve.Evaluate(boundary);
                if (Mathf.Abs(delta) < 0.0001f)
                {
                    continue;
                }

                float windowStart = stitch.AtTail ? length - window : 0.0f;
                float windowEnd = stitch.AtTail ? length : window;

                var keys = new List<Keyframe>();
                Keyframe[] original = curve.keys;
                for (int k = 0; k < original.Length; k++)
                {
                    if (original[k].time < windowStart - 0.0001f ||
                        original[k].time > windowEnd + 0.0001f)
                    {
                        keys.Add(original[k]);
                    }
                }

                for (float time = windowStart; time <= windowEnd + 0.0001f; time += step)
                {
                    float sample = Mathf.Clamp(time, 0.0f, length);
                    float weight = 1.0f - Mathf.SmoothStep(
                        0.0f,
                        1.0f,
                        Mathf.Abs(sample - boundary) / window);
                    keys.Add(new Keyframe(sample, curve.Evaluate(sample) + delta * weight));
                }

                keys.Sort((left, right) => left.time.CompareTo(right.time));
                var stitchedCurve = new AnimationCurve(keys.ToArray());
                for (int k = 0; k < stitchedCurve.length; k++)
                {
                    float time = stitchedCurve[k].time;
                    if (time >= windowStart - 0.0001f && time <= windowEnd + 0.0001f)
                    {
                        stitchedCurve.SmoothTangents(k, 0.0f);
                    }
                }

                AnimationUtility.SetEditorCurve(clip, binding, stitchedCurve);
            }
        }

        [MenuItem("Tools/Animation/Report Curated Clip Sources")]
        private static void ReportSources()
        {
            var report = new StringBuilder();
            report.AppendLine("target;source;clip;length;loop;bindings");
            for (int i = 0; i < Manifest.Length; i++)
            {
                CuratedClip entry = Manifest[i];
                AnimationClip target =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.TargetPath);
                string length = target != null
                    ? target.length.ToString("F3", CultureInfo.InvariantCulture)
                    : "missing";
                string bindings = target != null
                    ? AnimationUtility.GetCurveBindings(target).Length
                        .ToString(CultureInfo.InvariantCulture)
                    : "-";
                report.AppendLine(string.Join(";", new[]
                {
                    Path.GetFileNameWithoutExtension(entry.TargetPath),
                    Path.GetFileName(entry.SourceAsset),
                    entry.SourceClip,
                    length,
                    entry.Loop ? "yes" : "no",
                    bindings
                }));
            }

            Debug.Log(report.ToString());
        }
    }
}