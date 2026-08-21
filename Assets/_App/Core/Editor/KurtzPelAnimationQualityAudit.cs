using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class KurtzPelAnimationQualityAudit
    {
        private const string RootFolder =
            "Assets/_Game/Animations/Reference/KurtzPel/MaleComplete";
        private const string ModelPath = RootFolder + "/Model/KurtzPel_MaleReference.fbx";
        private const string CsvReportPath = "Docs/Animation/KurtzPelAnimationQuality.csv";
        private const string SummaryReportPath = "Docs/Animation/KurtzPelAnimationQuality.md";
        private const int SampleCount = 32;

        private const string SkeletonRootPath = "KurtzPel_MaleReference/Root";
        private const string PelvisPath = "KurtzPel_MaleReference/Root/Core/Pelvis";
        private const string LeftFootPath =
            "KurtzPel_MaleReference/Root/Core/Pelvis/Thigh_L/Calf_L/Foot_L";
        private const string RightFootPath =
            "KurtzPel_MaleReference/Root/Core/Pelvis/Thigh_R/Calf_R/Foot_R";

        private static readonly (string Group, string Path)[] Packs =
        {
            ("Common_Ninja", RootFolder + "/Movement/KurtzPel_Common_Ninja.fbx"),
            ("Male_Directional", RootFolder + "/Movement/KurtzPel_Male_Directional.fbx"),
            ("Male_ENFJ", RootFolder + "/Movement/KurtzPel_Male_ENFJ.fbx"),
            ("Male_ENTP", RootFolder + "/Movement/KurtzPel_Male_ENTP.fbx"),
            ("Male_ESFP", RootFolder + "/Movement/KurtzPel_Male_ESFP.fbx"),
            ("Male_ESTJ", RootFolder + "/Movement/KurtzPel_Male_ESTJ.fbx"),
            ("Male_INFP", RootFolder + "/Movement/KurtzPel_Male_INFP.fbx"),
            ("Male_INTJ", RootFolder + "/Movement/KurtzPel_Male_INTJ.fbx"),
            ("Male_ISTJ", RootFolder + "/Movement/KurtzPel_Male_ISTJ.fbx"),
            ("Male_ISTP", RootFolder + "/Movement/KurtzPel_Male_ISTP.fbx"),
            ("Male_Reactions", RootFolder + "/Reactions/KurtzPel_Male_Reactions.fbx"),
            ("Male_Misc", RootFolder + "/Misc/KurtzPel_Male_Misc.fbx"),
            ("BlazingFist", RootFolder + "/Combat/KurtzPel_BlazingFist.fbx")
        };

        [MenuItem("Tools/Animation/KurtzPel/Run Full Quality Audit")]
        public static void RunFullAudit()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException(
                    $"KurtzPel reference model is missing: {ModelPath}");
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sampler = new RigSampler(instance);
                var results = new List<ClipQuality>(437);
                foreach ((string group, string path) in Packs)
                {
                    AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        .OrderBy(clip => clip.name, StringComparer.Ordinal)
                        .ToArray();
                    foreach (AnimationClip clip in clips)
                    {
                        results.Add(AnalyzeClip(group, path, clip, sampler));
                    }
                }

                if (results.Count != 437)
                {
                    throw new InvalidOperationException(
                        $"Quality audit expected 437 clips, found {results.Count}.");
                }

                WriteCsv(results);
                WriteSummary(results);
                Debug.Log(
                    $"[KurtzPel Animation Quality] Audited {results.Count} clips. " +
                    $"Reports: {CsvReportPath}, {SummaryReportPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static ClipQuality AnalyzeClip(
            string group,
            string assetPath,
            AnimationClip clip,
            RigSampler sampler)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            string category = Classify(clip.name);
            sampler.ResetPose();
            sampler.Sample(clip, 0f);
            Quaternion[] startPose = sampler.CaptureRotations();
            Vector3 startRoot = sampler.RootPosition;
            Vector3 startPelvis = sampler.PelvisPosition;

            sampler.ResetPose();
            sampler.Sample(clip, clip.length);
            float poseDelta = CalculatePoseRms(startPose, sampler.CaptureRotations());
            float rootDelta = HorizontalDistance(startRoot, sampler.RootPosition);
            float pelvisDelta = HorizontalDistance(startPelvis, sampler.PelvisPosition);

            float leftPhase = 0f;
            float rightPhase = 0f;
            float leftMinHeight = float.PositiveInfinity;
            float rightMinHeight = float.PositiveInfinity;
            float maxRootTravel = 0f;
            float maxPelvisTravel = 0f;
            for (int i = 0; i < SampleCount; i++)
            {
                float phase = i / (float)SampleCount;
                sampler.ResetPose();
                sampler.Sample(clip, clip.length * phase);

                float leftHeight = sampler.LeftFootPosition.y;
                if (leftHeight < leftMinHeight)
                {
                    leftMinHeight = leftHeight;
                    leftPhase = phase;
                }

                float rightHeight = sampler.RightFootPosition.y;
                if (rightHeight < rightMinHeight)
                {
                    rightMinHeight = rightHeight;
                    rightPhase = phase;
                }

                maxRootTravel = Mathf.Max(
                    maxRootTravel,
                    HorizontalDistance(startRoot, sampler.RootPosition));
                maxPelvisTravel = Mathf.Max(
                    maxPelvisTravel,
                    HorizontalDistance(startPelvis, sampler.PelvisPosition));
            }

            int curveCount = AnimationUtility.GetCurveBindings(clip).Length;
            int transformCount = AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.type == typeof(Transform))
                .Select(binding => binding.path)
                .Distinct(StringComparer.Ordinal)
                .Count();

            string flags = BuildFlags(
                clip,
                settings,
                category,
                poseDelta,
                leftPhase,
                rightPhase);

            return new ClipQuality(
                group,
                assetPath,
                clip.name,
                category,
                clip.length,
                clip.frameRate,
                settings.loopTime,
                settings.loopBlend,
                curveCount,
                transformCount,
                clip.hasRootCurves,
                clip.hasMotionCurves,
                poseDelta,
                rootDelta,
                pelvisDelta,
                maxRootTravel,
                maxPelvisTravel,
                leftPhase,
                rightPhase,
                flags);
        }

        private static string BuildFlags(
            AnimationClip clip,
            AnimationClipSettings settings,
            string category,
            float poseDelta,
            float leftPhase,
            float rightPhase)
        {
            var flags = new List<string>(4);
            if (clip.length <= 1f / Mathf.Max(1f, clip.frameRate) + 0.001f)
            {
                flags.Add("OneFrame");
            }

            if (settings.loopTime && poseDelta > 2.0f)
            {
                flags.Add("LoopPoseDelta");
            }

            if (settings.loopTime && category == "Locomotion")
            {
                float separation = CircularDistance(leftPhase, rightPhase);
                if (separation < 0.32f || separation > 0.68f)
                {
                    flags.Add("FootPhaseReview");
                }
            }

            if (clip.hasRootCurves || clip.hasMotionCurves)
            {
                flags.Add("AuthoredRootMotion");
            }

            if (clip.length > 8f)
            {
                flags.Add("LongClip");
            }

            return flags.Count > 0 ? string.Join("|", flags) : "None";
        }

        private static string Classify(string name)
        {
            if (Contains(name, "Wait") || Contains(name, "Idle"))
            {
                return "Idle";
            }

            if (Contains(name, "RunEnd") || name.EndsWith("_Start", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_End", StringComparison.OrdinalIgnoreCase))
            {
                return "StartStop";
            }

            if (Contains(name, "Run") || Contains(name, "Walk"))
            {
                return "Locomotion";
            }

            if (Contains(name, "Jump") || Contains(name, "Air") || Contains(name, "Landing"))
            {
                return "Airborne";
            }

            if (Contains(name, "Avoid") || Contains(name, "Dash") || Contains(name, "Roll"))
            {
                return "Dodge";
            }

            if (Contains(name, "Attack") || Contains(name, "Combo") || Contains(name, "Skill") ||
                Contains(name, "Punch") || Contains(name, "Parry"))
            {
                return "Combat";
            }

            if (Contains(name, "Hit") || Contains(name, "Blow") || Contains(name, "Stand"))
            {
                return "Reaction";
            }

            return "Other";
        }

        private static bool Contains(string source, string value) =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static float CalculatePoseRms(
            IReadOnlyList<Quaternion> first,
            IReadOnlyList<Quaternion> second)
        {
            int count = Mathf.Min(first.Count, second.Count);
            if (count == 0)
            {
                return 0f;
            }

            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                float angle = Quaternion.Angle(first[i], second[i]);
                sum += angle * angle;
            }

            return (float)Math.Sqrt(sum / count);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            Vector2 delta = new Vector2(second.x - first.x, second.z - first.z);
            return delta.magnitude;
        }

        private static float CircularDistance(float first, float second)
        {
            float difference = Mathf.Abs(first - second);
            return Mathf.Min(difference, 1f - difference);
        }

        private static void WriteCsv(IReadOnlyList<ClipQuality> results)
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "group,asset,clip,category,length,fps,loop,loop_pose,curves,animated_transforms," +
                "has_root_curves,has_motion_curves,start_end_pose_rms,root_end_delta," +
                "pelvis_end_delta,max_root_travel,max_pelvis_travel,left_foot_phase," +
                "right_foot_phase,flags");
            foreach (ClipQuality result in results)
            {
                csv.AppendLine(result.ToCsv());
            }

            WriteText(CsvReportPath, csv.ToString());
        }

        private static void WriteSummary(IReadOnlyList<ClipQuality> results)
        {
            var markdown = new StringBuilder();
            markdown.AppendLine("# KurtzPel animation quality report");
            markdown.AppendLine();
            markdown.AppendLine(
                "This deterministic editor audit samples every imported clip on the shared " +
                "reference rig. It detects structural/timing/loop/contact issues; semantic " +
                "combat suitability still requires visual review in the preview window.");
            markdown.AppendLine();
            markdown.AppendLine($"- Total clips: `{results.Count}`");
            markdown.AppendLine($"- Clips with authored root/motion curves: `{results.Count(result => result.HasAuthoredRootMotion)}`");
            markdown.AppendLine($"- Clips flagged for review: `{results.Count(result => result.Flags != "None")}`");
            markdown.AppendLine();
            markdown.AppendLine("## Categories");
            markdown.AppendLine();
            markdown.AppendLine("| Category | Clips | Flagged |");
            markdown.AppendLine("| --- | ---: | ---: |");
            foreach (IGrouping<string, ClipQuality> group in results
                         .GroupBy(result => result.Category)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                markdown.AppendLine(
                    $"| {group.Key} | {group.Count()} | {group.Count(result => result.Flags != "None")} |");
            }

            markdown.AppendLine();
            markdown.AppendLine("## Review flags");
            markdown.AppendLine();
            foreach (IGrouping<string, ClipQuality> flag in results
                         .SelectMany(result => result.Flags.Split('|')
                             .Where(value => value != "None")
                             .Select(value => (Flag: value, Result: result)))
                         .GroupBy(item => item.Flag, item => item.Result)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                markdown.AppendLine($"- `{flag.Key}`: {flag.Count()}");
            }

            markdown.AppendLine();
            markdown.AppendLine("Full row-level measurements are stored in `KurtzPelAnimationQuality.csv`.");
            WriteText(SummaryReportPath, markdown.ToString());
        }

        private static void WriteText(string relativePath, string contents)
        {
            string absolutePath = Path.GetFullPath(relativePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, contents, new UTF8Encoding(false));
        }

        private sealed class RigSampler
        {
            private readonly GameObject _instance;
            private readonly Transform[] _transforms;
            private readonly Vector3[] _positions;
            private readonly Quaternion[] _rotations;
            private readonly Vector3[] _scales;
            private readonly Transform _root;
            private readonly Transform _pelvis;
            private readonly Transform _leftFoot;
            private readonly Transform _rightFoot;

            public Vector3 RootPosition => RelativePosition(_root);
            public Vector3 PelvisPosition => RelativePosition(_pelvis);
            public Vector3 LeftFootPosition => RelativePosition(_leftFoot);
            public Vector3 RightFootPosition => RelativePosition(_rightFoot);

            public RigSampler(GameObject instance)
            {
                _instance = instance;
                _transforms = instance.GetComponentsInChildren<Transform>(true);
                _positions = new Vector3[_transforms.Length];
                _rotations = new Quaternion[_transforms.Length];
                _scales = new Vector3[_transforms.Length];
                for (int i = 0; i < _transforms.Length; i++)
                {
                    _positions[i] = _transforms[i].localPosition;
                    _rotations[i] = _transforms[i].localRotation;
                    _scales[i] = _transforms[i].localScale;
                }

                _root = RequireTransform(SkeletonRootPath);
                _pelvis = RequireTransform(PelvisPath);
                _leftFoot = RequireTransform(LeftFootPath);
                _rightFoot = RequireTransform(RightFootPath);
            }

            public void ResetPose()
            {
                for (int i = 0; i < _transforms.Length; i++)
                {
                    _transforms[i].localPosition = _positions[i];
                    _transforms[i].localRotation = _rotations[i];
                    _transforms[i].localScale = _scales[i];
                }
            }

            public void Sample(AnimationClip clip, float time)
            {
                clip.SampleAnimation(_instance, Mathf.Clamp(time, 0f, clip.length));
            }

            public Quaternion[] CaptureRotations()
            {
                var result = new Quaternion[_transforms.Length];
                for (int i = 0; i < _transforms.Length; i++)
                {
                    result[i] = _transforms[i].localRotation;
                }

                return result;
            }

            private Transform RequireTransform(string path)
            {
                Transform result = _instance.transform.Find(path);
                return result != null
                    ? result
                    : throw new InvalidOperationException(
                        $"KurtzPel reference rig is missing transform '{path}'.");
            }

            private Vector3 RelativePosition(Transform target) =>
                _instance.transform.InverseTransformPoint(target.position);
        }

        private readonly struct ClipQuality
        {
            public string Group { get; }
            public string AssetPath { get; }
            public string ClipName { get; }
            public string Category { get; }
            public float Length { get; }
            public float FrameRate { get; }
            public bool Loop { get; }
            public bool LoopPose { get; }
            public int CurveCount { get; }
            public int TransformCount { get; }
            public bool HasRootCurves { get; }
            public bool HasMotionCurves { get; }
            public float PoseDelta { get; }
            public float RootDelta { get; }
            public float PelvisDelta { get; }
            public float MaxRootTravel { get; }
            public float MaxPelvisTravel { get; }
            public float LeftFootPhase { get; }
            public float RightFootPhase { get; }
            public string Flags { get; }
            public bool HasAuthoredRootMotion => HasRootCurves || HasMotionCurves;

            public ClipQuality(
                string group,
                string assetPath,
                string clipName,
                string category,
                float length,
                float frameRate,
                bool loop,
                bool loopPose,
                int curveCount,
                int transformCount,
                bool hasRootCurves,
                bool hasMotionCurves,
                float poseDelta,
                float rootDelta,
                float pelvisDelta,
                float maxRootTravel,
                float maxPelvisTravel,
                float leftFootPhase,
                float rightFootPhase,
                string flags)
            {
                Group = group;
                AssetPath = assetPath;
                ClipName = clipName;
                Category = category;
                Length = length;
                FrameRate = frameRate;
                Loop = loop;
                LoopPose = loopPose;
                CurveCount = curveCount;
                TransformCount = transformCount;
                HasRootCurves = hasRootCurves;
                HasMotionCurves = hasMotionCurves;
                PoseDelta = poseDelta;
                RootDelta = rootDelta;
                PelvisDelta = pelvisDelta;
                MaxRootTravel = maxRootTravel;
                MaxPelvisTravel = maxPelvisTravel;
                LeftFootPhase = leftFootPhase;
                RightFootPhase = rightFootPhase;
                Flags = flags;
            }

            public string ToCsv()
            {
                return string.Join(",",
                    Escape(Group),
                    Escape(AssetPath),
                    Escape(ClipName),
                    Escape(Category),
                    Format(Length),
                    Format(FrameRate),
                    Loop ? "true" : "false",
                    LoopPose ? "true" : "false",
                    CurveCount.ToString(CultureInfo.InvariantCulture),
                    TransformCount.ToString(CultureInfo.InvariantCulture),
                    HasRootCurves ? "true" : "false",
                    HasMotionCurves ? "true" : "false",
                    Format(PoseDelta),
                    Format(RootDelta),
                    Format(PelvisDelta),
                    Format(MaxRootTravel),
                    Format(MaxPelvisTravel),
                    Format(LeftFootPhase),
                    Format(RightFootPhase),
                    Escape(Flags));
            }

            private static string Format(float value) =>
                value.ToString("0.######", CultureInfo.InvariantCulture);

            private static string Escape(string value) =>
                '"' + value.Replace("\"", "\"\"") + '"';
        }
    }
}
