using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal static class FrankHumanoidClipBaker
    {
        internal const string SourceRoot = "Assets/_Game/Animations/Frank/";
        internal const string OutputRoot = SourceRoot + "Humanoid/";
        internal const string TargetModelPath =
            "Assets/_Game/Models/Characters/UnityCharacter/DefaultMale.fbx";
        private const float MuscleKeyTolerance = 0.001f;
        private const float RootKeyTolerance = 0.0001f;
        private const float MaxMuscleUnitsPerSecond = 18f;

        private static readonly BoneChain[] BodyChains =
        {
            new("spine_01", "spine_02", HumanBodyBones.Spine, HumanBodyBones.Chest),
            new("spine_02", "spine_03", HumanBodyBones.Chest, HumanBodyBones.UpperChest),
            new("spine_03", "neck_01", HumanBodyBones.UpperChest, HumanBodyBones.Neck),
            new("neck_01", "head", HumanBodyBones.Neck, HumanBodyBones.Head),
            new("clavicle_l", "upperarm_l", HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm),
            new("upperarm_l", "lowerarm_l", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            new("lowerarm_l", "hand_l", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            new("clavicle_r", "upperarm_r", HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
            new("upperarm_r", "lowerarm_r", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            new("lowerarm_r", "hand_r", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            new("thigh_l", "calf_l", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            new("calf_l", "foot_l", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            new("foot_l", "ball_l", HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),
            new("thigh_r", "calf_r", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            new("calf_r", "foot_r", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            new("foot_r", "ball_r", HumanBodyBones.RightFoot, HumanBodyBones.RightToes),
        };

        private static readonly BoneChain[] FingerChains = CreateFingerChains();
        private static readonly Queue<string> PendingBatch = new();
        private static readonly List<string> BatchFailures = new();
        private static int _batchTotal;
        private static int _batchCompleted;

        [MenuItem("Project S/Animation/Frank/Bake Selected Humanoid Clips")]
        private static void BakeSelected()
        {
            var paths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(IsSourceClip)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                throw new InvalidOperationException("Select one or more Frank source FBXs.");

            Bake(paths);
        }

        [MenuItem("Project S/Animation/Frank/Bake Entire Humanoid Library")]
        private static void BakeAll()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { SourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsSourceClip)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Bake(paths);
        }

        [MenuItem("Project S/Animation/Frank/Bake Entire Humanoid Library In Background")]
        private static void StartBackgroundBake()
        {
            if (PendingBatch.Count > 0)
                throw new InvalidOperationException("A Frank Humanoid bake is already running.");

            foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { SourceRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(IsSourceClip)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                PendingBatch.Enqueue(path);
            }

            _batchTotal = PendingBatch.Count;
            _batchCompleted = 0;
            BatchFailures.Clear();
            if (_batchTotal == 0)
                throw new InvalidOperationException("No Frank animation FBXs were found.");

            EditorApplication.update -= ProcessBackgroundBake;
            EditorApplication.update += ProcessBackgroundBake;
            Debug.Log($"Frank background Humanoid bake started: {_batchTotal} clips.");
        }

        internal static string GetBackgroundBakeStatus()
        {
            return $"{_batchCompleted}/{_batchTotal}; pending={PendingBatch.Count}; "
                   + $"failed={BatchFailures.Count}; running={PendingBatch.Count > 0}";
        }

        private static void ProcessBackgroundBake()
        {
            if (PendingBatch.Count == 0)
            {
                EditorApplication.update -= ProcessBackgroundBake;
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                RebindOverrideControllers();
                Debug.Log(
                    $"Frank background Humanoid bake complete: {_batchCompleted}/{_batchTotal}; "
                    + $"failed={BatchFailures.Count}.");
                return;
            }

            var path = PendingBatch.Dequeue();
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Project S — Frank Humanoid Bake",
                    $"{_batchCompleted + 1}/{_batchTotal}: {Path.GetFileNameWithoutExtension(path)}",
                    (_batchCompleted + 1f) / _batchTotal);
                BakeOne(path);
            }
            catch (Exception exception)
            {
                BatchFailures.Add($"{path}: {exception.Message}");
                Debug.LogException(exception);
            }
            finally
            {
                _batchCompleted++;
            }
        }

        internal static string BakeOne(string sourcePath)
        {
            if (!IsSourceClip(sourcePath))
                throw new ArgumentException($"Not a Frank source FBX: {sourcePath}");

            ConfigureGenericSource(sourcePath);
            var targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetModelPath);
            if (targetPrefab == null)
                throw new InvalidOperationException($"Humanoid bake target is missing: {TargetModelPath}");

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (sourcePrefab == null || sourceClip == null)
                throw new InvalidOperationException($"Generic source clip failed to import: {sourcePath}");

            var outputPath = GetOutputPath(sourcePath);
            EnsureAssetFolder(Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
            BakeClip(sourcePrefab, sourceClip, targetPrefab, outputPath);
            return outputPath;
        }

        [MenuItem("Project S/Animation/Frank/Rebind Overrides To Baked Humanoid Clips")]
        internal static void RebindOverrideControllers()
        {
            var changedControllers = 0;
            var changedClips = 0;
            var missingOutputs = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:AnimatorOverrideController", new[] { "Assets/_Game" }))
            {
                var controllerPath = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    controllerPath);
                if (controller == null)
                    continue;

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                var controllerChanged = false;
                for (var index = 0; index < overrides.Count; index++)
                {
                    var sourceClip = overrides[index].Value;
                    var sourcePath = sourceClip != null
                        ? AssetDatabase.GetAssetPath(sourceClip)
                        : string.Empty;
                    if (!IsSourceClip(sourcePath))
                        continue;

                    var outputPath = GetOutputPath(sourcePath);
                    var outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                    if (outputClip == null || !outputClip.humanMotion)
                    {
                        missingOutputs.Add($"{controllerPath}: {outputPath}");
                        continue;
                    }

                    overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(
                        overrides[index].Key,
                        outputClip);
                    controllerChanged = true;
                    changedClips++;
                }

                if (!controllerChanged)
                    continue;

                controller.ApplyOverrides(overrides);
                EditorUtility.SetDirty(controller);
                changedControllers++;
            }

            if (missingOutputs.Count > 0)
            {
                throw new InvalidOperationException(
                    "Frank override rebinding found missing Humanoid outputs:\n"
                    + string.Join("\n", missingOutputs));
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Frank Humanoid override rebinding complete: {changedClips} clip(s) in "
                + $"{changedControllers} controller(s).");
        }

        private static void Bake(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
                throw new InvalidOperationException("No Frank animation FBXs were found.");

            try
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    var path = paths[index];
                    EditorUtility.DisplayProgressBar(
                        "Project S — Frank Humanoid Bake",
                        $"{index + 1}/{paths.Count}: {Path.GetFileNameWithoutExtension(path)}",
                        (index + 1f) / paths.Count);
                    BakeOne(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Frank Humanoid bake complete: {paths.Count} clips written under {OutputRoot}.");
        }

        private static void ConfigureGenericSource(string sourcePath)
        {
            if (AssetImporter.GetAtPath(sourcePath) is not ModelImporter importer)
                throw new InvalidOperationException($"ModelImporter is unavailable: {sourcePath}");

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.resampleCurves = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.importAnimatedCustomProperties = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.SaveAndReimport();
        }

        private static void BakeClip(
            GameObject sourcePrefab,
            AnimationClip sourceClip,
            GameObject targetPrefab,
            string outputPath)
        {
            var sourceObject = UnityEngine.Object.Instantiate(sourcePrefab);
            var targetObject = UnityEngine.Object.Instantiate(targetPrefab);
            sourceObject.hideFlags = HideFlags.HideAndDontSave;
            targetObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sourceTransforms = sourceObject.GetComponentsInChildren<Transform>(true)
                    .GroupBy(transform => transform.name)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                var targetAnimator = targetObject.GetComponentInChildren<Animator>(true);
                if (targetAnimator == null
                    || targetAnimator.avatar == null
                    || !targetAnimator.avatar.isValid
                    || !targetAnimator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        $"Bake target does not expose a valid Humanoid Animator: {TargetModelPath}");
                }

                ValidateRequiredBones(sourceTransforms, targetAnimator);
                var sourceSnapshots = sourceObject.GetComponentsInChildren<Transform>(true)
                    .Select(transform => new TransformSnapshot(transform))
                    .ToArray();
                var sourcePelvis = sourceTransforms["pelvis"];
                var sourcePelvisRestPosition = sourcePelvis.position;
                var sourcePelvisRestRotation = sourcePelvis.rotation;
                var targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
                var targetHipsRestRotation = targetHips.rotation;
                var translationScale = CalculateTranslationScale(sourceTransforms, targetAnimator);
                using var poseHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
                var basePose = new HumanPose();
                poseHandler.GetHumanPose(ref basePose);

                var frameRate = Mathf.Max(1f, sourceClip.frameRate);
                var frameCount = Mathf.Max(2, Mathf.RoundToInt(sourceClip.length * frameRate) + 1);
                var samples = new PoseSample[frameCount];
                var previousBodyRotation = Quaternion.identity;
                float[] previousMuscles = null;
                for (var frame = 0; frame < frameCount; frame++)
                {
                    var time = Mathf.Min(sourceClip.length, frame / frameRate);
                    foreach (var snapshot in sourceSnapshots)
                        snapshot.Restore();
                    sourceClip.SampleAnimation(sourceObject, time);

                    var neutralPose = basePose;
                    neutralPose.muscles = new float[HumanTrait.MuscleCount];
                    poseHandler.SetHumanPose(ref neutralPose);
                    targetHips.rotation = targetHipsRestRotation
                                          * Quaternion.Inverse(sourcePelvisRestRotation)
                                          * sourcePelvis.rotation;
                    ApplyChains(BodyChains, sourceTransforms, targetAnimator);
                    ApplyChains(FingerChains, sourceTransforms, targetAnimator);

                    var pose = new HumanPose();
                    poseHandler.GetHumanPose(ref pose);
                    var sourceDisplacement = sourcePelvis.position - sourcePelvisRestPosition;
                    pose.bodyPosition = basePose.bodyPosition + sourceDisplacement * translationScale;
                    SanitizeMuscles(pose.muscles);
                    StabilizeMuscles(pose.muscles, previousMuscles, frameRate);
                    if (frame > 0 && Quaternion.Dot(previousBodyRotation, pose.bodyRotation) < 0f)
                    {
                        pose.bodyRotation = new Quaternion(
                            -pose.bodyRotation.x,
                            -pose.bodyRotation.y,
                            -pose.bodyRotation.z,
                            -pose.bodyRotation.w);
                    }

                    previousBodyRotation = pose.bodyRotation;
                    samples[frame] = new PoseSample(time, pose);
                    previousMuscles = (float[])pose.muscles.Clone();
                }

                WriteClip(outputPath, sourceClip, samples);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static void ApplyChains(
            IEnumerable<BoneChain> chains,
            IReadOnlyDictionary<string, Transform> sourceTransforms,
            Animator targetAnimator)
        {
            foreach (var chain in chains)
            {
                if (!sourceTransforms.TryGetValue(chain.SourceBone, out var sourceBone)
                    || !sourceTransforms.TryGetValue(chain.SourceChild, out var sourceChild))
                {
                    continue;
                }

                var targetBone = targetAnimator.GetBoneTransform(chain.TargetBone);
                var targetChild = targetAnimator.GetBoneTransform(chain.TargetChild);
                if (targetBone == null || targetChild == null)
                    continue;

                var sourceDirection = sourceChild.position - sourceBone.position;
                var targetDirection = targetChild.position - targetBone.position;
                if (sourceDirection.sqrMagnitude < 1e-8f || targetDirection.sqrMagnitude < 1e-8f)
                    continue;

                targetBone.rotation = Quaternion.FromToRotation(
                                          targetDirection.normalized,
                                          sourceDirection.normalized)
                                      * targetBone.rotation;
            }
        }

        private static void SanitizeMuscles(float[] muscles)
        {
            for (var index = 0; index < muscles.Length; index++)
            {
                var value = float.IsFinite(muscles[index]) ? muscles[index] : 0f;
                muscles[index] = Mathf.Clamp(value, -1f, 1f);
            }
        }

        private static void StabilizeMuscles(
            float[] muscles,
            IReadOnlyList<float> previousMuscles,
            float frameRate)
        {
            if (previousMuscles == null)
                return;

            var maxDelta = MaxMuscleUnitsPerSecond / Mathf.Max(1f, frameRate);
            for (var index = 0; index < muscles.Length; index++)
            {
                muscles[index] = Mathf.MoveTowards(
                    previousMuscles[index],
                    muscles[index],
                    maxDelta);
            }
        }

        private static void WriteClip(
            string outputPath,
            AnimationClip sourceClip,
            IReadOnlyList<PoseSample> samples)
        {
            var outputClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (outputClip == null)
            {
                outputClip = new AnimationClip();
                AssetDatabase.CreateAsset(outputClip, outputPath);
            }
            else
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(outputClip))
                    AnimationUtility.SetEditorCurve(outputClip, binding, null);
            }

            outputClip.name = Path.GetFileNameWithoutExtension(outputPath);
            outputClip.frameRate = sourceClip.frameRate;
            for (var muscleIndex = 0; muscleIndex < HumanTrait.MuscleCount; muscleIndex++)
            {
                var capturedIndex = muscleIndex;
                SetCurve(
                    outputClip,
                    HumanTrait.MuscleName[muscleIndex],
                    samples.Select(sample => new Keyframe(sample.Time, sample.Muscles[capturedIndex])),
                    MuscleKeyTolerance);
            }

            SetCurve(outputClip, "RootT.x", samples.Select(sample => new Keyframe(sample.Time, sample.BodyPosition.x)), RootKeyTolerance);
            SetCurve(outputClip, "RootT.y", samples.Select(sample => new Keyframe(sample.Time, sample.BodyPosition.y)), RootKeyTolerance);
            SetCurve(outputClip, "RootT.z", samples.Select(sample => new Keyframe(sample.Time, sample.BodyPosition.z)), RootKeyTolerance);
            SetCurve(outputClip, "RootQ.x", samples.Select(sample => new Keyframe(sample.Time, sample.BodyRotation.x)), RootKeyTolerance);
            SetCurve(outputClip, "RootQ.y", samples.Select(sample => new Keyframe(sample.Time, sample.BodyRotation.y)), RootKeyTolerance);
            SetCurve(outputClip, "RootQ.z", samples.Select(sample => new Keyframe(sample.Time, sample.BodyRotation.z)), RootKeyTolerance);
            SetCurve(outputClip, "RootQ.w", samples.Select(sample => new Keyframe(sample.Time, sample.BodyRotation.w)), RootKeyTolerance);

            var settings = AnimationUtility.GetAnimationClipSettings(outputClip);
            settings.loopTime = IsLoopingClip(outputClip.name);
            settings.loopBlend = settings.loopTime;
            AnimationUtility.SetAnimationClipSettings(outputClip, settings);
            outputClip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(outputClip);
            AssetDatabase.SaveAssetIfDirty(outputClip);
        }

        private static void SetCurve(
            AnimationClip clip,
            string propertyName,
            IEnumerable<Keyframe> sourceKeys,
            float tolerance)
        {
            var keys = ReduceKeys(sourceKeys.ToArray(), tolerance);
            var curve = new AnimationCurve(keys);
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }

            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), propertyName),
                curve);
        }

        private static Keyframe[] ReduceKeys(IReadOnlyList<Keyframe> keys, float tolerance)
        {
            if (keys.Count <= 2)
                return keys.ToArray();

            var result = new List<Keyframe>(keys.Count) { keys[0] };
            for (var index = 1; index < keys.Count - 1; index++)
            {
                var previous = result[^1];
                var next = keys[index + 1];
                var duration = next.time - previous.time;
                var factor = duration > 1e-6f ? (keys[index].time - previous.time) / duration : 0f;
                var expected = Mathf.LerpUnclamped(previous.value, next.value, factor);
                if (Mathf.Abs(keys[index].value - expected) > tolerance)
                    result.Add(keys[index]);
            }
            result.Add(keys[^1]);
            return result.ToArray();
        }

        private static float CalculateTranslationScale(
            IReadOnlyDictionary<string, Transform> sourceTransforms,
            Animator targetAnimator)
        {
            var sourceHeight = Vector3.Distance(sourceTransforms["pelvis"].position, sourceTransforms["head"].position);
            var targetHeight = Vector3.Distance(
                targetAnimator.GetBoneTransform(HumanBodyBones.Hips).position,
                targetAnimator.GetBoneTransform(HumanBodyBones.Head).position);
            return sourceHeight > 1e-5f ? targetHeight / sourceHeight : 1f;
        }

        private static void ValidateRequiredBones(
            IReadOnlyDictionary<string, Transform> sourceTransforms,
            Animator targetAnimator)
        {
            foreach (var chain in BodyChains)
            {
                if (!sourceTransforms.ContainsKey(chain.SourceBone)
                    || !sourceTransforms.ContainsKey(chain.SourceChild))
                {
                    throw new InvalidOperationException(
                        $"Frank source skeleton is missing {chain.SourceBone}/{chain.SourceChild}.");
                }
                if (targetAnimator.GetBoneTransform(chain.TargetBone) == null
                    || targetAnimator.GetBoneTransform(chain.TargetChild) == null)
                {
                    throw new InvalidOperationException(
                        $"Humanoid target is missing {chain.TargetBone}/{chain.TargetChild}.");
                }
            }
        }

        private static BoneChain[] CreateFingerChains()
        {
            var result = new List<BoneChain>();
            foreach (var side in new[] { (Suffix: "l", Prefix: "Left"), (Suffix: "r", Prefix: "Right") })
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
            {
                var sourceFinger = finger.ToLowerInvariant();
                if (Enum.TryParse($"{side.Prefix}{finger}Proximal", out HumanBodyBones proximal)
                    && Enum.TryParse($"{side.Prefix}{finger}Intermediate", out HumanBodyBones intermediate)
                    && Enum.TryParse($"{side.Prefix}{finger}Distal", out HumanBodyBones distal))
                {
                    result.Add(new BoneChain(
                        $"{sourceFinger}_01_{side.Suffix}",
                        $"{sourceFinger}_02_{side.Suffix}",
                        proximal,
                        intermediate));
                    result.Add(new BoneChain(
                        $"{sourceFinger}_02_{side.Suffix}",
                        $"{sourceFinger}_03_{side.Suffix}",
                        intermediate,
                        distal));
                }
            }
            return result.ToArray();
        }

        internal static bool IsSourceClip(string path)
        {
            return path.StartsWith(SourceRoot, StringComparison.Ordinal)
                   && !path.StartsWith(OutputRoot, StringComparison.Ordinal)
                   && !path.Contains("/Rig/", StringComparison.Ordinal)
                   && !path.Contains("/Diagnostics/", StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetOutputPath(string sourcePath)
        {
            var relativePath = sourcePath.Substring(SourceRoot.Length);
            return OutputRoot + Path.ChangeExtension(relativePath, ".anim").Replace('\\', '/');
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;
            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        private static bool IsLoopingClip(string clipName)
        {
            if (ContainsAny(clipName, "Attack", "Combo", "Skill", "Hit", "Die", "Death", "Evade", "Step"))
                return false;
            return ContainsAny(clipName, "Idle", "Walk", "Run", "Sprint", "Loop");
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            return tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private readonly struct BoneChain
        {
            public BoneChain(
                string sourceBone,
                string sourceChild,
                HumanBodyBones targetBone,
                HumanBodyBones targetChild)
            {
                SourceBone = sourceBone;
                SourceChild = sourceChild;
                TargetBone = targetBone;
                TargetChild = targetChild;
            }

            public string SourceBone { get; }
            public string SourceChild { get; }
            public HumanBodyBones TargetBone { get; }
            public HumanBodyBones TargetChild { get; }
        }

        private readonly struct TransformSnapshot
        {
            private readonly Transform _transform;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _localScale;

            public TransformSnapshot(Transform transform)
            {
                _transform = transform;
                _localPosition = transform.localPosition;
                _localRotation = transform.localRotation;
                _localScale = transform.localScale;
            }

            public void Restore()
            {
                _transform.localPosition = _localPosition;
                _transform.localRotation = _localRotation;
                _transform.localScale = _localScale;
            }
        }

        private readonly struct PoseSample
        {
            public PoseSample(float time, HumanPose pose)
            {
                Time = time;
                BodyPosition = pose.bodyPosition;
                BodyRotation = pose.bodyRotation;
                Muscles = (float[])pose.muscles.Clone();
            }

            public float Time { get; }
            public Vector3 BodyPosition { get; }
            public Quaternion BodyRotation { get; }
            public float[] Muscles { get; }
        }
    }
}
