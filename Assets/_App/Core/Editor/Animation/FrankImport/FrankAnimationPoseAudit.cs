using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal static class FrankAnimationPoseAudit
    {
        private const string ReportPath =
            "Assets/_Game/Animations/Frank/FrankHumanoidImportReport.json";
        private const string PreviewModelPath =
            "Assets/_Game/Models/Characters/UnityCharacter/DefaultMale.fbx";
        private const int PanelSize = 512;

        [Serializable]
        private sealed class ValidationReport
        {
            public List<ModelRecord> models = new();
        }

        [Serializable]
        private sealed class ModelRecord
        {
            public string assetPath;
            public List<ClipRecord> clips = new();
        }

        [Serializable]
        private sealed class ClipRecord
        {
            public int confirmedPoseSpikeCount;
            public List<KeyIssueRecord> keyIssues = new();
        }

        [Serializable]
        private sealed class KeyIssueRecord
        {
            public float time;
            public string issue;
        }

        [MenuItem("Project S/Animation/Frank/Render Reported Pose Spikes")]
        public static void RenderReportedPoseSpikes()
        {
            var absoluteReportPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ReportPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absoluteReportPath))
                throw new FileNotFoundException("Run Frank Humanoid validation first.", ReportPath);

            var report = JsonUtility.FromJson<ValidationReport>(
                File.ReadAllText(absoluteReportPath));
            var previewModel = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewModelPath);
            if (previewModel == null)
                throw new FileNotFoundException("Frank pose-audit model is missing.", PreviewModelPath);

            var outputDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Library/ProjectSAnimationImport/UnityPoseAudit");
            Directory.CreateDirectory(outputDirectory);
            var rendered = 0;
            var ownsAnimationMode = !AnimationMode.InAnimationMode();
            if (ownsAnimationMode)
                AnimationMode.StartAnimationMode();
            try
            {
                foreach (var model in report.models)
                {
                    var times = model.clips
                        .Where(clip => clip.confirmedPoseSpikeCount > 0)
                        .SelectMany(clip => clip.keyIssues)
                        .Where(issue => issue.issue.StartsWith(
                            "confirmed pose spike", StringComparison.Ordinal))
                        .Select(issue => issue.time)
                        .Distinct()
                        .OrderBy(time => time)
                        .ToArray();
                    if (times.Length == 0)
                        continue;

                    var clip = AssetDatabase.LoadAllAssetsAtPath(model.assetPath)
                        .OfType<AnimationClip>()
                        .FirstOrDefault(candidate => !candidate.name.StartsWith(
                            "__preview__", StringComparison.Ordinal));
                    if (clip == null)
                        continue;

                    foreach (var time in times)
                    {
                        var frameStep = 1f / Mathf.Max(1f, clip.frameRate);
                        var sampleTimes = new[]
                        {
                            Mathf.Max(0f, time - frameStep),
                            Mathf.Clamp(time, 0f, clip.length),
                            Mathf.Min(clip.length, time + frameStep),
                        };
                        var sheet = RenderContactSheet(previewModel, clip, sampleTimes);
                        var safeName = Path.GetFileNameWithoutExtension(model.assetPath);
                        var frame = Mathf.RoundToInt(time * clip.frameRate);
                        var outputPath = Path.Combine(
                            outputDirectory, $"{safeName}_frame-{frame:D4}.png");
                        File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(sheet);
                        rendered++;
                    }
                }

                Debug.Log(
                    $"Frank pose audit rendered {rendered} contact sheets to {outputDirectory}.");
            }
            finally
            {
                if (ownsAnimationMode && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            }
        }

        private static Texture2D RenderContactSheet(
            GameObject previewModel,
            AnimationClip clip,
            IReadOnlyList<float> times)
        {
            var sheet = new Texture2D(
                PanelSize * times.Count,
                PanelSize,
                TextureFormat.RGBA32,
                false);
            sheet.SetPixels(Enumerable.Repeat(
                new Color(0.08f, 0.08f, 0.08f, 1f),
                sheet.width * sheet.height).ToArray());

            for (var index = 0; index < times.Count; index++)
            {
                var panel = RenderPose(previewModel, clip, times[index]);
                sheet.SetPixels(index * PanelSize, 0, PanelSize, PanelSize, panel.GetPixels());
                UnityEngine.Object.DestroyImmediate(panel);
            }

            sheet.Apply(false, false);
            return sheet;
        }

        private static Texture2D RenderPose(
            GameObject previewModel,
            AnimationClip clip,
            float time)
        {
            var preview = new PreviewRenderUtility();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(previewModel);
                instance.hideFlags = HideFlags.HideAndDontSave;
                preview.AddSingleGO(instance);

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instance, clip, time);
                AnimationMode.EndSampling();

                var bounds = CalculateBounds(instance);
                var radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = radius * 20f;
                camera.fieldOfView = 30f;
                camera.transform.position = bounds.center
                                            + new Vector3(radius * 2.8f, radius * 0.15f, radius * 2.8f);
                camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.05f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                preview.lights[1].intensity = 1.0f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);

                preview.BeginStaticPreview(new Rect(0f, 0f, PanelSize, PanelSize));
                camera.Render();
                return preview.EndStaticPreview();
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                preview.Cleanup();
            }
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(instance.transform.position + Vector3.up, Vector3.one * 2f);

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
