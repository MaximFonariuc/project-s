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
            public float PoseRms { get; }
            public float VelocityRms { get; }

            public Seam(
                string kind,
                string from,
                string to,
                AnimationClip fromClip,
                AnimationClip toClip,
                float transitionSeconds,
                float poseRms,
                float velocityRms)
            {
                Kind = kind;
                From = from;
                To = to;
                FromClip = fromClip != null ? fromClip.name : "none";
                ToClip = toClip != null ? toClip.name : "none";
                FromSource = DescribeSource(fromClip);
                ToSource = DescribeSource(toClip);
                TransitionSeconds = transitionSeconds;
                PoseRms = poseRms;
                VelocityRms = velocityRms;
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

            List<Seam> seams = CollectSeams(controller);
            WriteReport(seams);
            LogSummary(seams);
        }

        private static List<Seam> CollectSeams(AnimatorController controller)
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
                AddSeam(seams, "enter", move, action, fade, 1.0f);
                AddSeam(seams, "exit", action, move, fade, 1.0f);
            }

            foreach (KeyValuePair<string, string> chain in ChainPairs())
            {
                if (states.TryGetValue(chain.Key, out AnimatorState from) &&
                    states.TryGetValue(chain.Value, out AnimatorState to))
                {
                    AddSeam(seams, "chain", from, to, ActionCrossFadeSeconds, ChainExitTime);
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
            string kind,
            AnimatorState from,
            AnimatorState to,
            float transitionSeconds,
            float exitTime)
        {
            AnimationClip fromClip = ResolveClip(from.motion);
            AnimationClip toClip = ResolveClip(to.motion);
            ComparePoses(
                fromClip,
                toClip,
                exitTime,
                out float poseRms,
                out float velocityRms);

            seams.Add(new Seam(
                kind,
                from.name,
                to.name,
                fromClip,
                toClip,
                transitionSeconds,
                poseRms,
                velocityRms));
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
            AnimationClip from,
            AnimationClip to,
            float exitTime,
            out float poseRms,
            out float velocityRms)
        {
            poseRms = 0.0f;
            velocityRms = 0.0f;
            if (from == null || to == null)
            {
                return;
            }

            Dictionary<string, AnimationCurve> fromCurves = SampleCurves(from);
            Dictionary<string, AnimationCurve> toCurves = SampleCurves(to);
            float fromTime = Mathf.Clamp01(exitTime) * from.length;
            const float step = 1.0f / 30.0f;

            double poseSum = 0.0;
            double velocitySum = 0.0;
            int count = 0;
            foreach (KeyValuePair<string, AnimationCurve> pair in fromCurves)
            {
                if (!toCurves.TryGetValue(pair.Key, out AnimationCurve toCurve))
                {
                    continue;
                }

                float delta = pair.Value.Evaluate(fromTime) - toCurve.Evaluate(0.0f);
                poseSum += delta * delta;

                float fromRate =
                    (pair.Value.Evaluate(fromTime) -
                     pair.Value.Evaluate(Mathf.Max(0.0f, fromTime - step))) / step;
                float toRate =
                    (toCurve.Evaluate(Mathf.Min(to.length, step)) - toCurve.Evaluate(0.0f)) / step;
                float rateDelta = fromRate - toRate;
                velocitySum += rateDelta * rateDelta;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            poseRms = Mathf.Sqrt((float)(poseSum / count));
            velocityRms = Mathf.Sqrt((float)(velocitySum / count));
        }

        private static Dictionary<string, AnimationCurve> SampleCurves(AnimationClip clip)
        {
            var curves = new Dictionary<string, AnimationCurve>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                {
                    curves[binding.path + "/" + binding.propertyName] = curve;
                }
            }

            return curves;
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
                "kind;from;to;fromClip;toClip;fromSource;toSource;fadeSeconds;poseRms;velocityRms;crossSource");
            foreach (Seam seam in seams.OrderByDescending(s => s.PoseRms))
            {
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
                    Format(seam.PoseRms),
                    Format(seam.VelocityRms),
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
            double mean = seams.Count > 0 ? seams.Average(s => (double)s.PoseRms) : 0.0;
            var builder = new StringBuilder();
            builder.AppendLine(
                $"[Animation Seams] {seams.Count} seam(s), {crossSource} cross-source, " +
                $"mean pose rms {mean:F4}. Report: {OutputPath}");
            foreach (Seam seam in seams.OrderByDescending(s => s.PoseRms).Take(15))
            {
                builder.AppendLine(
                    $"  rms {Format(seam.PoseRms)}  {seam.Kind,-5} {seam.From} -> {seam.To}" +
                    $"  [{seam.FromSource} -> {seam.ToSource}]");
            }

            Debug.Log(builder.ToString());
        }

        private static string Format(float value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }
    }
}
