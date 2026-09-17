using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class AnimationSeamReport
    {
        private const string ControllerPath =
            "Assets/_Game/Animations/Character/1. Controllers/XController.controller";
        private const string PlayerPrefabPath =
            "Assets/_Game/Prefabs/Character/Player.prefab";
        private const string OutputPath = "Docs/Animation/AnimationSeams.csv";
        private const string MenuPath = "Tools/Animation/Report Animation Seams";
        private const float ActionCrossFadeSeconds = 0.12f;
        private const float DodgeCrossFadeSeconds = 0.08f;
        private const float ChainExitTime = 0.84f;

        private readonly struct Seam
        {
            public string Kind { get; }
            public string From { get; }
            public string To { get; }
            public string FromClip { get; }
            public string ToClip { get; }
            public string FromSource { get; }
            public string ToSource { get; }
            public float TransitionSeconds { get; }
            public float PeakDegrees { get; }
            public string PeakBone { get; }
            public float MeanDegrees { get; }

            public Seam(
                string kind,
                string from,
                string to,
                AnimationClip fromClip,
                AnimationClip toClip,
                float transitionSeconds,
                float peakDegrees,
                string peakBone,
                float meanDegrees)
            {
                Kind = kind;
                From = from;
                To = to;
                FromClip = fromClip != null ? fromClip.name : "none";
                ToClip = toClip != null ? toClip.name : "none";
                FromSource = DescribeSource(fromClip);
                ToSource = DescribeSource(toClip);
                TransitionSeconds = transitionSeconds;
                PeakDegrees = peakDegrees;
                PeakBone = peakBone;
                MeanDegrees = meanDegrees;
            }
        }

        private sealed class PoseSampler : IDisposable
        {
            private readonly GameObject _instance;
            private readonly Animator _animator;
            private readonly List<KeyValuePair<HumanBodyBones, Transform>> _bones =
                new List<KeyValuePair<HumanBodyBones, Transform>>();

            public bool IsValid => _animator != null && _bones.Count > 0;

            public PoseSampler()
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (prefab == null)
                {
                    return;
                }

                _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _instance.name = "__AnimationSeamProbe";
                _instance.hideFlags = HideFlags.HideAndDontSave;
                _animator = _instance.GetComponentInChildren<Animator>(true);
                if (_animator == null || _animator.avatar == null ||
                    !_animator.avatar.isHuman)
                {
                    return;
                }

                for (var bone = HumanBodyBones.Hips;
                     bone < HumanBodyBones.LastBone;
                     bone++)
                {
                    if (IsFingerBone(bone))
                    {
                        continue;
                    }

                    Transform transform = _animator.GetBoneTransform(bone);
                    if (transform != null)
                    {
                        _bones.Add(
                            new KeyValuePair<HumanBodyBones, Transform>(
                                bone,
                                transform));
                    }
                }
            }

            public bool TrySample(
                AnimationClip clip,
                float normalizedTime,
                Quaternion[] pose)
            {
                if (clip == null || !IsValid)
                {
                    return false;
                }

                clip.SampleAnimation(
                    _animator.gameObject,
                    clip.length * Mathf.Clamp01(normalizedTime));
                for (int i = 0; i < _bones.Count; i++)
                {
                    pose[i] = _bones[i].Value.localRotation;
                }

                return true;
            }

            public Quaternion[] CreatePose()
            {
                return new Quaternion[_bones.Count];
            }

            public string BoneName(int index)
            {
                return _bones[index].Key.ToString();
            }

            public int BoneCount => _bones.Count;

            public void Dispose()
            {
                if (_instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(_instance);
                }
            }

            private static bool IsFingerBone(HumanBodyBones bone)
            {
                return bone >= HumanBodyBones.LeftThumbProximal &&
                       bone <= HumanBodyBones.RightLittleDistal;
            }
        }

        [MenuItem(MenuPath)]
        private static void Run()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"Missing controller: {ControllerPath}");
                return;
            }

            using (var sampler = new PoseSampler())
            {
                if (!sampler.IsValid)
                {
                    Debug.LogError(
                        $"Seam report requires a humanoid Animator on {PlayerPrefabPath}.");
                    return;
                }

                List<Seam> seams = CollectSeams(controller, sampler);
                WriteReport(seams);
                LogSummary(seams);
            }
        }

        private static List<Seam> CollectSeams(
            AnimatorController controller,
            PoseSampler sampler)
        {
            Dictionary<string, AnimatorState> states = IndexStates(controller);
            var seams = new List<Seam>();
            if (!states.TryGetValue(XControllerAnimationContract.MoveStatePath, out AnimatorState move))
            {
                Debug.LogError("Locomotion Move state is missing from the controller.");
                return seams;
            }

            foreach (string actionPath in ActionStatePaths(states))
            {
                AnimatorState action = states[actionPath];
                float fade = FadeSeconds(actionPath);
                AddSeam(seams, sampler, "enter", move, action, fade, 1.0f);
                AddSeam(seams, sampler, "exit", action, move, fade, 1.0f);
            }

            foreach (KeyValuePair<string, string> chain in ChainPairs())
            {
                if (states.TryGetValue(chain.Key, out AnimatorState from) &&
                    states.TryGetValue(chain.Value, out AnimatorState to))
                {
                    AddSeam(seams, sampler, "chain", from, to, ActionCrossFadeSeconds, ChainExitTime);
                }
            }

            return seams;
        }

        private static Dictionary<string, AnimatorState> IndexStates(AnimatorController controller)
        {
            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                IndexStateMachine(layer.stateMachine, layer.name, states);
            }

            return states;
        }

        private static void IndexStateMachine(
            AnimatorStateMachine stateMachine,
            string path,
            Dictionary<string, AnimatorState> states)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                states[path + "." + child.state.name] = child.state;
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                IndexStateMachine(
                    child.stateMachine,
                    path + "." + child.stateMachine.name,
                    states);
            }
        }

        private static List<string> ActionStatePaths(Dictionary<string, AnimatorState> states)
        {
            string[] prefixes =
            {
                "Base Layer.Attack.",
                "Base Layer.Reaction.",
                "Base Layer.Locomotion.Roll.",
                "Base Layer.Locomotion.Jump."
            };

            return states.Keys
                .Where(path => prefixes.Any(prefix =>
                    path.StartsWith(prefix, StringComparison.Ordinal)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static List<KeyValuePair<string, string>> ChainPairs()
        {
            string[] ground =
            {
                XControllerAnimationContract.Attack1StatePath,
                XControllerAnimationContract.Attack2StatePath,
                XControllerAnimationContract.Attack3StatePath,
                XControllerAnimationContract.Attack4StatePath
            };
            string[] air =
            {
                XControllerAnimationContract.AirAttack1StatePath,
                XControllerAnimationContract.AirAttack2StatePath,
                XControllerAnimationContract.AirAttack3StatePath
            };

            var pairs = new List<KeyValuePair<string, string>>();
            AppendChain(pairs, ground);
            AppendChain(pairs, air);
            pairs.Add(new KeyValuePair<string, string>(
                XControllerAnimationContract.LauncherStatePath,
                XControllerAnimationContract.AirAttack1StatePath));
            return pairs;
        }

        private static void AppendChain(
            List<KeyValuePair<string, string>> pairs,
            string[] sequence)
        {
            for (int i = 0; i + 1 < sequence.Length; i++)
            {
                pairs.Add(new KeyValuePair<string, string>(sequence[i], sequence[i + 1]));
            }
        }

        private static float FadeSeconds(string targetPath)
        {
            return targetPath.StartsWith("Base Layer.Locomotion.Roll.", StringComparison.Ordinal)
                ? DodgeCrossFadeSeconds
                : ActionCrossFadeSeconds;
        }

        private static void AddSeam(
            List<Seam> seams,
            PoseSampler sampler,
            string kind,
            AnimatorState from,
            AnimatorState to,
            float transitionSeconds,
            float exitTime)
        {
            AnimationClip fromClip = ResolveClip(from.motion);
            AnimationClip toClip = ResolveClip(to.motion);
            ComparePoses(
                sampler,
                fromClip,
                toClip,
                exitTime,
                out float peakDegrees,
                out string peakBone,
                out float meanDegrees);

            seams.Add(new Seam(
                kind,
                from.name,
                to.name,
                fromClip,
                toClip,
                transitionSeconds,
                peakDegrees,
                peakBone,
                meanDegrees));
        }

        private static AnimationClip ResolveClip(Motion motion)
        {
            switch (motion)
            {
                case AnimationClip clip:
                    return clip;
                case BlendTree tree:
                {
                    foreach (ChildMotion child in tree.children)
                    {
                        AnimationClip resolved = ResolveClip(child.motion);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }

                    return null;
                }
                default:
                    return null;
            }
        }

        private static void ComparePoses(
            PoseSampler sampler,
            AnimationClip from,
            AnimationClip to,
            float exitTime,
            out float peakDegrees,
            out string peakBone,
            out float meanDegrees)
        {
            peakDegrees = 0.0f;
            peakBone = "none";
            meanDegrees = 0.0f;

            Quaternion[] fromPose = sampler.CreatePose();
            Quaternion[] toPose = sampler.CreatePose();
            if (!sampler.TrySample(from, exitTime, fromPose) ||
                !sampler.TrySample(to, 0.0f, toPose))
            {
                return;
            }

            double total = 0.0;
            for (int i = 0; i < sampler.BoneCount; i++)
            {
                float angle = Quaternion.Angle(fromPose[i], toPose[i]);
                total += angle;
                if (angle > peakDegrees)
                {
                    peakDegrees = angle;
                    peakBone = sampler.BoneName(i);
                }
            }

            meanDegrees = (float)(total / sampler.BoneCount);
        }

        private static string DescribeSource(AnimationClip clip)
        {
            if (clip == null)
            {
                return "none";
            }

            int bindingCount = AnimationUtility.GetCurveBindings(clip).Length;
            switch (bindingCount)
            {
                case 130:
                case 193:
                    return "CLazy";
                case 137:
                    return "Knight";
                case 181:
                case 182:
                    return "Frank";
                default:
                    return "rig" + bindingCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void WriteReport(List<Seam> seams)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "kind;from;to;fromClip;toClip;fromSource;toSource;fadeSeconds;" +
                "peakDegrees;peakBone;meanDegrees;degreesPerSecond;crossSource");
            foreach (Seam seam in seams.OrderByDescending(s => s.PeakDegrees))
            {
                float rate = seam.TransitionSeconds > 0.0f
                    ? seam.PeakDegrees / seam.TransitionSeconds
                    : 0.0f;
                builder.AppendLine(string.Join(";", new[]
                {
                    seam.Kind,
                    seam.From,
                    seam.To,
                    seam.FromClip,
                    seam.ToClip,
                    seam.FromSource,
                    seam.ToSource,
                    Format(seam.TransitionSeconds),
                    Format(seam.PeakDegrees),
                    seam.PeakBone,
                    Format(seam.MeanDegrees),
                    Format(rate),
                    seam.FromSource != seam.ToSource ? "yes" : "no"
                }));
            }

            string directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(OutputPath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static void LogSummary(List<Seam> seams)
        {
            int crossSource = seams.Count(s => s.FromSource != s.ToSource);
            double mean = seams.Count > 0 ? seams.Average(s => (double)s.PeakDegrees) : 0.0;
            var builder = new StringBuilder();
            builder.AppendLine(
                $"[Animation Seams] {seams.Count} seam(s), {crossSource} cross-source, " +
                $"mean peak {mean:F1} deg. Report: {OutputPath}");
            foreach (Seam seam in seams.OrderByDescending(s => s.PeakDegrees).Take(15))
            {
                builder.AppendLine(
                    $"  {seam.PeakDegrees,5:F0} deg {seam.PeakBone,-16} over " +
                    $"{seam.TransitionSeconds:F2}s  {seam.Kind,-5} {seam.From} -> {seam.To}");
            }

            Debug.Log(builder.ToString());
        }

        private static string Format(float value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}