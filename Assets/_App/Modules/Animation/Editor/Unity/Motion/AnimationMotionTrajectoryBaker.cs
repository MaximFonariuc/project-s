using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ModularAnimation.Unity.Editor
{
    public readonly struct AnimationMotionBakeRequest
    {
        public AnimationClip SourceClip { get; }
        public string MotionRootPath { get; }
        public string TrajectoryAssetPath { get; }
        public Quaternion SourceToCharacterRotation { get; }
        public float PositionScale { get; }
        public int SampleRate { get; }
        public bool ExtractVerticalTranslation { get; }
        public bool ExtractRotation { get; }

        public AnimationMotionBakeRequest(
            AnimationClip sourceClip,
            string motionRootPath,
            string trajectoryAssetPath,
            Quaternion sourceToCharacterRotation,
            float positionScale,
            int sampleRate = 60,
            bool extractVerticalTranslation = false,
            bool extractRotation = false)
        {
            SourceClip = sourceClip;
            MotionRootPath = motionRootPath;
            TrajectoryAssetPath = trajectoryAssetPath;
            SourceToCharacterRotation = sourceToCharacterRotation;
            PositionScale = positionScale;
            SampleRate = sampleRate;
            ExtractVerticalTranslation = extractVerticalTranslation;
            ExtractRotation = extractRotation;
        }
    }

    public static class AnimationMotionTrajectoryBaker
    {
        public static AnimationMotionTrajectory Bake(in AnimationMotionBakeRequest request)
        {
            Validate(in request);
            EnsureAssetFolder(
                Path.GetDirectoryName(request.TrajectoryAssetPath)?.Replace('\\', '/'));

            Sample(
                in request,
                out Vector3[] positions,
                out Quaternion[] rotations);

            return UpsertTrajectory(
                in request,
                positions,
                rotations);
        }

        private static void Validate(in AnimationMotionBakeRequest request)
        {
            if (request.SourceClip == null)
            {
                throw new ArgumentNullException(nameof(request.SourceClip));
            }

            if (request.SourceClip.length <= 0.0f)
            {
                throw new ArgumentException("The source clip has no duration.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.MotionRootPath))
            {
                throw new ArgumentException("A motion root path is required.", nameof(request));
            }

            ValidateAssetPath(
                request.TrajectoryAssetPath,
                ".asset",
                nameof(request.TrajectoryAssetPath));
            if (request.SampleRate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(request.SampleRate));
            }

            if (request.PositionScale <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(request.PositionScale));
            }
        }

        private static void Sample(
            in AnimationMotionBakeRequest request,
            out Vector3[] positions,
            out Quaternion[] rotations)
        {
            string sourcePath = AssetDatabase.GetAssetPath(request.SourceClip);
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceModel == null)
            {
                throw new InvalidOperationException(
                    $"Animation '{request.SourceClip.name}' is not stored in a model asset.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(sourceModel);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform motionRoot = instance.transform.Find(request.MotionRootPath);
                if (motionRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Motion root '{request.MotionRootPath}' was not found in '{sourcePath}'.");
                }

                int sampleCount = Mathf.Max(
                    2,
                    Mathf.CeilToInt(request.SourceClip.length * request.SampleRate) + 1);
                positions = new Vector3[sampleCount];
                rotations = new Quaternion[sampleCount];

                request.SourceClip.SampleAnimation(instance, 0.0f);
                Vector3 initialPosition = motionRoot.localPosition;
                Quaternion initialRotation = motionRoot.localRotation;
                Quaternion inverseCorrection = Quaternion.Inverse(
                    request.SourceToCharacterRotation);

                for (int i = 0; i < sampleCount; i++)
                {
                    float normalizedTime = i / (float)(sampleCount - 1);
                    request.SourceClip.SampleAnimation(
                        instance,
                        normalizedTime * request.SourceClip.length);

                    Vector3 sourceDisplacement = motionRoot.localPosition - initialPosition;
                    if (!request.ExtractVerticalTranslation)
                    {
                        sourceDisplacement.y = 0.0f;
                    }

                    positions[i] = request.SourceToCharacterRotation * sourceDisplacement *
                                   request.PositionScale;

                    Quaternion sourceRotation =
                        Quaternion.Inverse(initialRotation) * motionRoot.localRotation;
                    rotations[i] = request.ExtractRotation
                        ? request.SourceToCharacterRotation * sourceRotation * inverseCorrection
                        : Quaternion.identity;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static AnimationMotionTrajectory UpsertTrajectory(
            in AnimationMotionBakeRequest request,
            Vector3[] positions,
            Quaternion[] rotations)
        {
            AnimationMotionTrajectory trajectory =
                AssetDatabase.LoadAssetAtPath<AnimationMotionTrajectory>(
                    request.TrajectoryAssetPath);
            if (trajectory == null)
            {
                trajectory = ScriptableObject.CreateInstance<AnimationMotionTrajectory>();
                AssetDatabase.CreateAsset(trajectory, request.TrajectoryAssetPath);
            }

            trajectory.SetBakedData(
                request.SourceClip,
                request.MotionRootPath,
                request.SampleRate,
                positions,
                rotations);
            EditorUtility.SetDirty(trajectory);
            return trajectory;
        }

        private static void ValidateAssetPath(
            string path,
            string extension,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Generated assets must use an Assets-relative '{extension}' path.",
                    parameterName);
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
