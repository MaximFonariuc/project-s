using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _App.Editor
{
    public readonly struct BodyColliderGenerationResult
    {
        public bool Success { get; }
        public BodyRigType RigType { get; }
        public int GeneratedCount { get; }
        public int RemovedCount { get; }
        public string Message { get; }

        public BodyColliderGenerationResult(bool success, BodyRigType rigType, int generatedCount, int removedCount, string message)
        {
            Success = success;
            RigType = rigType;
            GeneratedCount = generatedCount;
            RemovedCount = removedCount;
            Message = message;
        }
    }

    public static class BodyColliderGenerator
    {
        private const string DefaultLayerName = "Hitbox";
        private const float DefaultMinimumRadius = 0.025f;
        private const float DefaultMinimumBoneWeight = 0.2f;
        private const float DefaultVertexPercentile = 0.92f;

        public static BodyColliderGenerationResult Generate(BodyColliderAuthoring authoring)
        {
            if (authoring == null)
            {
                return Failure("Select an actor with BodyColliderAuthoring.");
            }

            Undo.RecordObject(authoring, "Resolve body collider references");
            authoring.ResolveMissingReferences();
            if (!TryValidate(authoring, out string validationError))
            {
                return Failure(validationError);
            }

            List<BodyColliderBuildDefinition> definitions = BodyColliderBoneResolver.Resolve(authoring, out BodyRigType rigType);
            if (authoring.RigType == BodyRigType.Humanoid && !authoring.Animator.isHuman)
            {
                return Failure("The authoring component is forced to Humanoid, but its Animator does not have a Humanoid avatar.", rigType);
            }

            if (definitions.Count == 0)
            {
                return Failure(
                    rigType == BodyRigType.Generic
                        ? "No Generic body bones were resolved. Add explicit Generic Bones mappings to BodyColliderAuthoring."
                        : "No supported Humanoid bones were resolved.",
                    rigType);
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate body colliders");

            try
            {
                BodyColliderRig rig = GetOrAddRig(authoring.gameObject);
                var existingByKey = GetExistingByKey(authoring, rig);
                var used = new HashSet<GeneratedBodyCollider>();
                var zones = new List<Hurtbox>(definitions.Count);
                SkinnedMeshRenderer[] sourceMeshes = ResolveSourceMeshes(authoring);
                int layer = ResolveLayer(authoring.Profile, authoring.gameObject.layer);

                using (var skinSamples = new SkinSampleCache(sourceMeshes))
                {
                    var weightedPoints = new List<Vector3>(256);

                    for (int i = 0; i < definitions.Count; i++)
                    {
                        BodyColliderBuildDefinition definition = definitions[i];
                        string key = BuildGenerationKey(authoring.transform, definition);
                        existingByKey.TryGetValue(key, out GeneratedBodyCollider marker);

                        if (marker == null)
                        {
                            marker = CreateGeneratedNode(definition, rig, key);
                        }

                        used.Add(marker);
                        weightedPoints.Clear();
                        skinSamples.CollectWeightedPoints(
                            definition.StartBone,
                            authoring.Profile != null ? authoring.Profile.MinimumBoneWeight : DefaultMinimumBoneWeight,
                            weightedPoints);

                        Hurtbox hurtbox = ConfigureNode(
                            marker,
                            rig,
                            authoring.Health,
                            definition,
                            key,
                            layer,
                            weightedPoints,
                            authoring.Profile);
                        zones.Add(hurtbox);
                    }
                }

                int removed = RemoveStaleGeneratedNodes(authoring, rig, used);
                Undo.RecordObject(rig, "Configure body collider rig");
                rig.Configure(authoring.Health, zones);
                EditorUtility.SetDirty(rig);
                EditorUtility.SetDirty(authoring);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
                Undo.CollapseUndoOperations(undoGroup);

                string message = $"Generated {zones.Count} {rigType} body colliders for '{authoring.name}'.";
                return new BodyColliderGenerationResult(true, rigType, zones.Count, removed, message);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Failure($"Body collider generation failed: {exception.Message}", rigType);
            }
        }

        public static BodyColliderGenerationResult ClearGenerated(BodyColliderAuthoring authoring)
        {
            if (authoring == null)
            {
                return Failure("Select an actor with BodyColliderAuthoring.");
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear generated body colliders");
            BodyColliderRig rig = authoring.GetComponent<BodyColliderRig>();
            GeneratedBodyCollider[] markers = authoring.GetComponentsInChildren<GeneratedBodyCollider>(true);
            int removed = 0;

            for (int i = markers.Length - 1; i >= 0; i--)
            {
                GeneratedBodyCollider marker = markers[i];
                if (rig == null || marker.Rig == null || marker.Rig == rig)
                {
                    Undo.DestroyObjectImmediate(marker.gameObject);
                    removed++;
                }
            }

            if (rig != null)
            {
                Undo.RecordObject(rig, "Clear body collider rig");
                rig.Configure(authoring.Health, Array.Empty<Hurtbox>());
                EditorUtility.SetDirty(rig);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return new BodyColliderGenerationResult(true, BodyRigType.Auto, 0, removed, $"Removed {removed} generated body colliders from '{authoring.name}'.");
        }

        public static BodyColliderAuthoring GetOrAddAuthoring(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            BodyColliderAuthoring authoring = actor.GetComponent<BodyColliderAuthoring>();
            if (authoring == null)
            {
                authoring = Undo.AddComponent<BodyColliderAuthoring>(actor);
            }

            Undo.RecordObject(authoring, "Configure body collider authoring");
            authoring.ResolveMissingReferences();
            EditorUtility.SetDirty(authoring);
            return authoring;
        }

        private static bool TryValidate(BodyColliderAuthoring authoring, out string error)
        {
            if (authoring.Animator == null)
            {
                error = $"'{authoring.name}' does not contain an Animator.";
                return false;
            }

            if (authoring.Health == null)
            {
                error = $"'{authoring.name}' requires a Health component on the authoring root.";
                return false;
            }

            error = null;
            return true;
        }

        private static BodyColliderRig GetOrAddRig(GameObject actor)
        {
            BodyColliderRig rig = actor.GetComponent<BodyColliderRig>();
            return rig != null ? rig : Undo.AddComponent<BodyColliderRig>(actor);
        }

        private static Dictionary<string, GeneratedBodyCollider> GetExistingByKey(BodyColliderAuthoring authoring, BodyColliderRig rig)
        {
            var result = new Dictionary<string, GeneratedBodyCollider>(StringComparer.Ordinal);
            GeneratedBodyCollider[] markers = authoring.GetComponentsInChildren<GeneratedBodyCollider>(true);

            for (int i = 0; i < markers.Length; i++)
            {
                GeneratedBodyCollider marker = markers[i];
                if ((marker.Rig == null || marker.Rig == rig) && !string.IsNullOrEmpty(marker.GenerationKey) && !result.ContainsKey(marker.GenerationKey))
                {
                    result.Add(marker.GenerationKey, marker);
                }
            }

            return result;
        }

        private static GeneratedBodyCollider CreateGeneratedNode(BodyColliderBuildDefinition definition, BodyColliderRig rig, string key)
        {
            var node = new GameObject($"Hurtbox_{definition.ZoneId}");
            Undo.RegisterCreatedObjectUndo(node, "Create generated body collider");
            Undo.SetTransformParent(node.transform, definition.StartBone, "Parent generated body collider");
            GeneratedBodyCollider marker = Undo.AddComponent<GeneratedBodyCollider>(node);
            marker.Configure(rig, key);
            return marker;
        }

        private static Hurtbox ConfigureNode(
            GeneratedBodyCollider marker,
            BodyColliderRig rig,
            Health health,
            BodyColliderBuildDefinition definition,
            string key,
            int layer,
            List<Vector3> weightedPoints,
            BodyColliderProfile profile)
        {
            GameObject node = marker.gameObject;
            Undo.RecordObject(marker, "Configure generated body collider");
            Undo.RecordObject(node, "Configure generated body collider");
            marker.Configure(rig, key);
            node.name = $"Hurtbox_{definition.ZoneId}";
            node.layer = layer;

            Collider collider = EnsureCollider(marker, definition.Shape);
            Hurtbox hurtbox = node.GetComponent<Hurtbox>();
            if (hurtbox == null)
            {
                hurtbox = Undo.AddComponent<Hurtbox>(node);
            }

            Undo.RecordObject(collider, "Fit generated body collider");
            Undo.RecordObject(hurtbox, "Configure hurtbox zone");
            collider.isTrigger = profile == null || profile.IsTrigger;

            if (!marker.LockGeometry)
            {
                ConfigureTransform(marker.transform, definition);
                FitCollider(collider, marker.transform, definition, weightedPoints, profile);
            }

            hurtbox.Configure(rig, health, definition.Zone, definition.CustomZoneId, definition.DamageMultiplier, collider);
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(hurtbox);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hurtbox);
            return hurtbox;
        }

        private static Collider EnsureCollider(GeneratedBodyCollider marker, BodyColliderShape shape)
        {
            Collider current = marker.GetComponent<Collider>();
            Type requiredType = shape == BodyColliderShape.Box
                ? typeof(BoxCollider)
                : shape == BodyColliderShape.Sphere
                    ? typeof(SphereCollider)
                    : typeof(CapsuleCollider);

            if (current != null && (current.GetType() == requiredType || marker.LockGeometry))
            {
                return current;
            }

            if (current != null)
            {
                Undo.DestroyObjectImmediate(current);
            }

            return (Collider)Undo.AddComponent(marker.gameObject, requiredType);
        }

        private static void ConfigureTransform(Transform node, BodyColliderBuildDefinition definition)
        {
            if (node.parent != definition.StartBone)
            {
                Undo.SetTransformParent(node, definition.StartBone, "Reparent generated body collider");
            }

            Undo.RecordObject(node, "Align generated body collider");
            node.localScale = Vector3.one;

            Vector3 worldDirection = definition.EndBone != null
                ? definition.EndBone.position - definition.StartBone.position
                : definition.StartBone.up;
            if (worldDirection.sqrMagnitude <= 0.000001f)
            {
                worldDirection = definition.StartBone.up;
            }

            node.localPosition = definition.AnchorAtStart
                ? Vector3.zero
                : definition.StartBone.InverseTransformPoint((definition.StartBone.position + definition.EndBone.position) * 0.5f);
            node.localRotation = definition.Shape == BodyColliderShape.Sphere
                ? Quaternion.identity
                : Quaternion.FromToRotation(Vector3.up, definition.StartBone.InverseTransformDirection(worldDirection.normalized));
        }

        private static void FitCollider(
            Collider collider,
            Transform node,
            BodyColliderBuildDefinition definition,
            List<Vector3> worldPoints,
            BodyColliderProfile profile)
        {
            var localPoints = new List<Vector3>(worldPoints.Count);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                localPoints.Add(node.InverseTransformPoint(worldPoints[i]));
            }

            float referenceLength = GetReferenceLength(node, definition);
            float minimumRadius = WorldToLocalLength(node, profile != null ? profile.MinimumRadius : DefaultMinimumRadius);
            float padding = WorldToLocalLength(node, definition.Padding);
            float percentile = profile != null ? profile.VertexPercentile : DefaultVertexPercentile;

            if (collider is CapsuleCollider capsule)
            {
                FitCapsule(capsule, localPoints, referenceLength, definition.RadiusScale, minimumRadius, padding, percentile);
            }
            else if (collider is SphereCollider sphere)
            {
                FitSphere(sphere, localPoints, referenceLength, definition.RadiusScale, minimumRadius, padding, percentile);
            }
            else if (collider is BoxCollider box)
            {
                FitBox(box, localPoints, referenceLength, definition.RadiusScale, minimumRadius, padding);
            }
        }

        private static void FitCapsule(
            CapsuleCollider collider,
            List<Vector3> points,
            float referenceLength,
            float radiusScale,
            float minimumRadius,
            float padding,
            float percentile)
        {
            collider.direction = 1;
            Vector3 center = Vector3.zero;
            float axialSpan = referenceLength;
            float radius = Mathf.Max(minimumRadius, referenceLength * radiusScale);

            if (TryGetBounds(points, out Bounds bounds))
            {
                center = bounds.center;
                axialSpan = Mathf.Max(referenceLength, bounds.size.y);
                var radialDistances = new List<float>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 offset = points[i] - center;
                    radialDistances.Add(Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z));
                }

                radius = Mathf.Max(minimumRadius, Percentile(radialDistances, percentile) + padding);
            }

            collider.center = center;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, axialSpan + radius * 2f + padding * 2f);
        }

        private static void FitSphere(
            SphereCollider collider,
            List<Vector3> points,
            float referenceLength,
            float radiusScale,
            float minimumRadius,
            float padding,
            float percentile)
        {
            Vector3 center = Vector3.zero;
            float radius = Mathf.Max(minimumRadius, referenceLength * radiusScale);

            if (TryGetBounds(points, out Bounds bounds))
            {
                center = bounds.center;
                var distances = new List<float>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    distances.Add(Vector3.Distance(center, points[i]));
                }

                radius = Mathf.Max(minimumRadius, Percentile(distances, percentile) + padding);
            }

            collider.center = center;
            collider.radius = radius;
        }

        private static void FitBox(
            BoxCollider collider,
            List<Vector3> points,
            float referenceLength,
            float radiusScale,
            float minimumRadius,
            float padding)
        {
            if (TryGetBounds(points, out Bounds bounds))
            {
                collider.center = bounds.center;
                Vector3 size = bounds.size + Vector3.one * (padding * 2f);
                size.x = Mathf.Max(size.x, minimumRadius * 2f);
                size.y = Mathf.Max(size.y, minimumRadius * 2f);
                size.z = Mathf.Max(size.z, minimumRadius * 2f);
                collider.size = size;
                return;
            }

            float radius = Mathf.Max(minimumRadius, referenceLength * radiusScale);
            collider.center = Vector3.zero;
            collider.size = new Vector3(radius * 2f, Mathf.Max(referenceLength + padding * 2f, radius * 2f), radius * 2f);
        }

        private static bool TryGetBounds(List<Vector3> points, out Bounds bounds)
        {
            if (points.Count == 0)
            {
                bounds = default;
                return false;
            }

            bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return true;
        }

        private static float Percentile(List<float> values, float percentile)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            values.Sort();
            int index = Mathf.Clamp(Mathf.CeilToInt((values.Count - 1) * percentile), 0, values.Count - 1);
            return values[index];
        }

        private static float GetReferenceLength(Transform node, BodyColliderBuildDefinition definition)
        {
            if (definition.EndBone == null)
            {
                return 0.1f;
            }

            Vector3 start = node.InverseTransformPoint(definition.StartBone.position);
            Vector3 end = node.InverseTransformPoint(definition.EndBone.position);
            return Mathf.Max(0.001f, Vector3.Distance(start, end));
        }

        private static float WorldToLocalLength(Transform transform, float worldLength)
        {
            Vector3 scale = transform.lossyScale;
            float averageScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
            return worldLength / Mathf.Max(averageScale, 0.0001f);
        }

        private static int RemoveStaleGeneratedNodes(BodyColliderAuthoring authoring, BodyColliderRig rig, HashSet<GeneratedBodyCollider> used)
        {
            GeneratedBodyCollider[] markers = authoring.GetComponentsInChildren<GeneratedBodyCollider>(true);
            int removed = 0;

            for (int i = markers.Length - 1; i >= 0; i--)
            {
                GeneratedBodyCollider marker = markers[i];
                if ((marker.Rig == null || marker.Rig == rig) && !used.Contains(marker))
                {
                    Undo.DestroyObjectImmediate(marker.gameObject);
                    removed++;
                }
            }

            return removed;
        }

        private static SkinnedMeshRenderer[] ResolveSourceMeshes(BodyColliderAuthoring authoring)
        {
            var explicitSources = new List<SkinnedMeshRenderer>();
            IReadOnlyList<SkinnedMeshRenderer> source = authoring.SourceMeshes;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && !explicitSources.Contains(source[i]))
                {
                    explicitSources.Add(source[i]);
                }
            }

            return explicitSources.Count > 0
                ? explicitSources.ToArray()
                : authoring.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private static int ResolveLayer(BodyColliderProfile profile, int fallbackLayer)
        {
            string layerName = profile != null ? profile.LayerName : DefaultLayerName;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                return layer;
            }

            Debug.LogWarning($"Body collider layer '{layerName}' does not exist. Generated objects will use the actor layer.");
            return fallbackLayer;
        }

        private static string BuildGenerationKey(Transform root, BodyColliderBuildDefinition definition)
        {
            string startPath = AnimationUtility.CalculateTransformPath(definition.StartBone, root);
            string endPath = definition.EndBone != null
                ? AnimationUtility.CalculateTransformPath(definition.EndBone, root)
                : string.Empty;
            return $"{definition.Id}|{startPath}|{endPath}";
        }

        private static BodyColliderGenerationResult Failure(string message, BodyRigType rigType = BodyRigType.Auto)
        {
            return new BodyColliderGenerationResult(false, rigType, 0, 0, message);
        }

        private sealed class SkinSampleCache : IDisposable
        {
            private readonly List<SkinSample> _samples = new List<SkinSample>();

            public SkinSampleCache(SkinnedMeshRenderer[] renderers)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    SkinSample sample = SkinSample.Create(renderers[i]);
                    if (sample != null)
                    {
                        _samples.Add(sample);
                    }
                }
            }

            public void CollectWeightedPoints(Transform bone, float minimumWeight, List<Vector3> output)
            {
                for (int i = 0; i < _samples.Count; i++)
                {
                    _samples[i].CollectWeightedPoints(bone, minimumWeight, output);
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _samples.Count; i++)
                {
                    _samples[i].Dispose();
                }

                _samples.Clear();
            }
        }

        private sealed class SkinSample : IDisposable
        {
            private readonly SkinnedMeshRenderer _renderer;
            private readonly Transform[] _bones;
            private readonly BoneWeight[] _weights;
            private readonly Mesh _bakedMesh;
            private readonly Vector3[] _vertices;

            private SkinSample(SkinnedMeshRenderer renderer, Transform[] bones, BoneWeight[] weights, Mesh bakedMesh, Vector3[] vertices)
            {
                _renderer = renderer;
                _bones = bones;
                _weights = weights;
                _bakedMesh = bakedMesh;
                _vertices = vertices;
            }

            public static SkinSample Create(SkinnedMeshRenderer renderer)
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return null;
                }

                try
                {
                    BoneWeight[] weights = renderer.sharedMesh.boneWeights;
                    var bakedMesh = new Mesh { name = $"{renderer.name}_BodyColliderBake" };
                    // Compensate for the renderer scale here because sampled vertices are
                    // converted to world space with renderer.transform.TransformPoint below.
                    // Without compensation a scaled renderer applies its scale twice.
                    renderer.BakeMesh(bakedMesh, true);
                    Vector3[] vertices = bakedMesh.vertices;

                    if (weights.Length != vertices.Length)
                    {
                        UnityEngine.Object.DestroyImmediate(bakedMesh);
                        return null;
                    }

                    return new SkinSample(renderer, renderer.bones, weights, bakedMesh, vertices);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not sample skin weights from '{renderer.name}': {exception.Message}", renderer);
                    return null;
                }
            }

            public void CollectWeightedPoints(Transform bone, float minimumWeight, List<Vector3> output)
            {
                int boneIndex = Array.IndexOf(_bones, bone);
                if (boneIndex < 0)
                {
                    return;
                }

                for (int i = 0; i < _weights.Length; i++)
                {
                    if (GetWeight(_weights[i], boneIndex) >= minimumWeight)
                    {
                        output.Add(_renderer.transform.TransformPoint(_vertices[i]));
                    }
                }
            }

            public void Dispose()
            {
                if (_bakedMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(_bakedMesh);
                }
            }

            private static float GetWeight(BoneWeight weight, int boneIndex)
            {
                float result = 0f;
                if (weight.boneIndex0 == boneIndex) result = Mathf.Max(result, weight.weight0);
                if (weight.boneIndex1 == boneIndex) result = Mathf.Max(result, weight.weight1);
                if (weight.boneIndex2 == boneIndex) result = Mathf.Max(result, weight.weight2);
                if (weight.boneIndex3 == boneIndex) result = Mathf.Max(result, weight.weight3);
                return result;
            }
        }
    }
}
