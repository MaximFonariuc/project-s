using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _App.Editor
{
    internal sealed class BodyColliderBuildDefinition
    {
        public string Id { get; }
        public BodyHitZone Zone { get; }
        public string CustomZoneId { get; }
        public BodyColliderShape Shape { get; }
        public Transform StartBone { get; }
        public Transform EndBone { get; }
        public bool AnchorAtStart { get; }
        public float RadiusScale { get; }
        public float Padding { get; }
        public float DamageMultiplier { get; }

        public string ZoneId => BodyColliderBoneDefinition.GetZoneId(Zone, CustomZoneId);

        public BodyColliderBuildDefinition(
            string id,
            BodyHitZone zone,
            string customZoneId,
            BodyColliderShape shape,
            Transform startBone,
            Transform endBone,
            bool anchorAtStart,
            float radiusScale,
            float padding,
            float damageMultiplier)
        {
            Id = id;
            Zone = zone;
            CustomZoneId = customZoneId;
            Shape = shape;
            StartBone = startBone;
            EndBone = endBone;
            AnchorAtStart = anchorAtStart;
            RadiusScale = radiusScale;
            Padding = padding;
            DamageMultiplier = damageMultiplier;
        }
    }

    internal static class BodyColliderBoneResolver
    {
        private const float AutomaticSurfacePadding = 0f;

        public static List<BodyColliderBuildDefinition> Resolve(BodyColliderAuthoring authoring, out BodyRigType resolvedRigType)
        {
            Animator animator = authoring.Animator;
            resolvedRigType = ResolveRigType(authoring.RigType, animator);

            return resolvedRigType == BodyRigType.Humanoid
                ? ResolveHumanoid(animator, authoring.Profile)
                : ResolveGeneric(authoring);
        }

        private static BodyRigType ResolveRigType(BodyRigType requested, Animator animator)
        {
            if (requested != BodyRigType.Auto)
            {
                return requested;
            }

            return animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman
                ? BodyRigType.Humanoid
                : BodyRigType.Generic;
        }

        private static List<BodyColliderBuildDefinition> ResolveHumanoid(Animator animator, BodyColliderProfile profile)
        {
            var result = new List<BodyColliderBuildDefinition>(18);

            Transform hips = GetBone(animator, HumanBodyBones.Hips);
            Transform spine = GetBone(animator, HumanBodyBones.Spine);
            Transform chest = GetBone(animator, HumanBodyBones.Chest) ?? GetBone(animator, HumanBodyBones.UpperChest);
            Transform neck = GetBone(animator, HumanBodyBones.Neck);
            Transform head = GetBone(animator, HumanBodyBones.Head);

            Add(result, profile, "pelvis", BodyHitZone.Pelvis, BodyColliderShape.Box, hips, spine, false, 0.32f, AutomaticSurfacePadding);
            Add(result, profile, "abdomen", BodyHitZone.Abdomen, BodyColliderShape.Box, spine, chest, false, 0.30f, AutomaticSurfacePadding);
            Add(result, profile, "chest", BodyHitZone.Chest, BodyColliderShape.Box, chest ?? spine, neck, false, 0.34f, AutomaticSurfacePadding);
            Add(result, profile, "neck", BodyHitZone.Neck, BodyColliderShape.Capsule, neck, head, false, 0.25f, AutomaticSurfacePadding);
            Add(result, profile, "head", BodyHitZone.Head, BodyColliderShape.Sphere, head, neck, true, 0.48f, AutomaticSurfacePadding);

            AddLimb(result, profile, animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, "left-upper-arm", BodyHitZone.LeftUpperArm, 0.20f);
            AddLimb(result, profile, animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "left-lower-arm", BodyHitZone.LeftLowerArm, 0.18f);
            AddLimb(result, profile, animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "right-upper-arm", BodyHitZone.RightUpperArm, 0.20f);
            AddLimb(result, profile, animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "right-lower-arm", BodyHitZone.RightLowerArm, 0.18f);
            AddAnchor(result, profile, animator, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, "left-hand", BodyHitZone.LeftHand, BodyColliderShape.Sphere, 0.32f);
            AddAnchor(result, profile, animator, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, "right-hand", BodyHitZone.RightHand, BodyColliderShape.Sphere, 0.32f);

            AddLimb(result, profile, animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, "left-upper-leg", BodyHitZone.LeftUpperLeg, 0.22f);
            AddLimb(result, profile, animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "left-lower-leg", BodyHitZone.LeftLowerLeg, 0.19f);
            AddLimb(result, profile, animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "right-upper-leg", BodyHitZone.RightUpperLeg, 0.22f);
            AddLimb(result, profile, animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "right-lower-leg", BodyHitZone.RightLowerLeg, 0.19f);
            AddAnchor(result, profile, animator, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg, "left-foot", BodyHitZone.LeftFoot, BodyColliderShape.Box, 0.30f);
            AddAnchor(result, profile, animator, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg, "right-foot", BodyHitZone.RightFoot, BodyColliderShape.Box, 0.30f);

            return result;
        }

        private static List<BodyColliderBuildDefinition> ResolveGeneric(BodyColliderAuthoring authoring)
        {
            var explicitDefinitions = new List<BodyColliderBuildDefinition>();
            IReadOnlyList<BodyColliderBoneDefinition> source = authoring.GenericBones;
            BodyColliderProfile profile = authoring.Profile;

            for (int i = 0; i < source.Count; i++)
            {
                BodyColliderBoneDefinition value = source[i];
                if (value == null || !value.Enabled || value.StartBone == null)
                {
                    continue;
                }

                if (value.Shape != BodyColliderShape.Sphere && value.EndBone == null)
                {
                    continue;
                }

                explicitDefinitions.Add(new BodyColliderBuildDefinition(
                    value.EffectiveId,
                    value.Zone,
                    value.CustomZoneId,
                    value.Shape,
                    value.StartBone,
                    value.EndBone,
                    value.AnchorAtStart,
                    profile != null ? profile.GetRadiusScale(value.Zone, value.RadiusScale) : value.RadiusScale,
                    profile != null ? profile.GetPadding(value.Zone, value.Padding) : value.Padding,
                    profile != null ? profile.GetDamageMultiplier(value.Zone, value.DamageMultiplier) : value.DamageMultiplier));
            }

            if (explicitDefinitions.Count > 0)
            {
                return explicitDefinitions;
            }

            return ResolveGenericBiped(authoring.Animator, authoring.Profile);
        }

        private static List<BodyColliderBuildDefinition> ResolveGenericBiped(Animator animator, BodyColliderProfile profile)
        {
            var result = new List<BodyColliderBuildDefinition>(18);
            var bones = new List<Transform>();

            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Transform[] rendererBones = renderers[rendererIndex].bones;
                for (int boneIndex = 0; boneIndex < rendererBones.Length; boneIndex++)
                {
                    Transform bone = rendererBones[boneIndex];
                    if (bone != null && !bones.Contains(bone))
                    {
                        bones.Add(bone);
                    }
                }
            }

            Transform pelvis = FindBone(bones, "pelvis", "hips", "hip");
            Transform spine = FindBone(bones, "spine");
            Transform chest = FindBoneExcluding(bones, spine, "upperchest", "chest", "spine2", "spine1");
            Transform neck = FindBone(bones, "neck");
            Transform head = FindBone(bones, "head");

            Add(result, profile, "pelvis", BodyHitZone.Pelvis, BodyColliderShape.Box, pelvis, spine, false, 0.32f, AutomaticSurfacePadding);
            Add(result, profile, "abdomen", BodyHitZone.Abdomen, BodyColliderShape.Box, spine, chest, false, 0.30f, AutomaticSurfacePadding);
            Add(result, profile, "chest", BodyHitZone.Chest, BodyColliderShape.Box, chest ?? spine, neck, false, 0.34f, AutomaticSurfacePadding);
            Add(result, profile, "neck", BodyHitZone.Neck, BodyColliderShape.Capsule, neck, head, false, 0.25f, AutomaticSurfacePadding);
            Add(result, profile, "head", BodyHitZone.Head, BodyColliderShape.Sphere, head, neck, true, 0.48f, AutomaticSurfacePadding);

            AddGenericSide(result, profile, bones, true);
            AddGenericSide(result, profile, bones, false);
            return result;
        }

        private static void AddGenericSide(List<BodyColliderBuildDefinition> result, BodyColliderProfile profile, List<Transform> bones, bool left)
        {
            string side = left ? "left" : "right";
            string shortSide = left ? "l" : "r";
            string suffix = left ? "l" : "r";

            Transform upperArm = FindBone(bones, side + "upperarm", shortSide + "upperarm", "upperarm" + suffix);
            Transform lowerArm = FindBone(bones, side + "forearm", shortSide + "forearm", side + "lowerarm", shortSide + "lowerarm", "forearm" + suffix, "lowerarm" + suffix);
            Transform hand = FindBone(bones, side + "hand", shortSide + "hand", "hand" + suffix);
            Transform upperLeg = FindBone(bones, side + "thigh", shortSide + "thigh", side + "upperleg", shortSide + "upperleg", "thigh" + suffix, "upperleg" + suffix);
            Transform lowerLeg = FindBone(bones, side + "calf", shortSide + "calf", side + "lowerleg", shortSide + "lowerleg", "calf" + suffix, "lowerleg" + suffix);
            Transform foot = FindBone(bones, side + "foot", shortSide + "foot", "foot" + suffix);

            if (left)
            {
                Add(result, profile, "left-upper-arm", BodyHitZone.LeftUpperArm, BodyColliderShape.Capsule, upperArm, lowerArm, false, 0.20f, AutomaticSurfacePadding);
                Add(result, profile, "left-lower-arm", BodyHitZone.LeftLowerArm, BodyColliderShape.Capsule, lowerArm, hand, false, 0.18f, AutomaticSurfacePadding);
                Add(result, profile, "left-hand", BodyHitZone.LeftHand, BodyColliderShape.Sphere, hand, lowerArm, true, 0.32f, AutomaticSurfacePadding);
                Add(result, profile, "left-upper-leg", BodyHitZone.LeftUpperLeg, BodyColliderShape.Capsule, upperLeg, lowerLeg, false, 0.22f, AutomaticSurfacePadding);
                Add(result, profile, "left-lower-leg", BodyHitZone.LeftLowerLeg, BodyColliderShape.Capsule, lowerLeg, foot, false, 0.19f, AutomaticSurfacePadding);
                Add(result, profile, "left-foot", BodyHitZone.LeftFoot, BodyColliderShape.Box, foot, lowerLeg, true, 0.30f, AutomaticSurfacePadding);
            }
            else
            {
                Add(result, profile, "right-upper-arm", BodyHitZone.RightUpperArm, BodyColliderShape.Capsule, upperArm, lowerArm, false, 0.20f, AutomaticSurfacePadding);
                Add(result, profile, "right-lower-arm", BodyHitZone.RightLowerArm, BodyColliderShape.Capsule, lowerArm, hand, false, 0.18f, AutomaticSurfacePadding);
                Add(result, profile, "right-hand", BodyHitZone.RightHand, BodyColliderShape.Sphere, hand, lowerArm, true, 0.32f, AutomaticSurfacePadding);
                Add(result, profile, "right-upper-leg", BodyHitZone.RightUpperLeg, BodyColliderShape.Capsule, upperLeg, lowerLeg, false, 0.22f, AutomaticSurfacePadding);
                Add(result, profile, "right-lower-leg", BodyHitZone.RightLowerLeg, BodyColliderShape.Capsule, lowerLeg, foot, false, 0.19f, AutomaticSurfacePadding);
                Add(result, profile, "right-foot", BodyHitZone.RightFoot, BodyColliderShape.Box, foot, lowerLeg, true, 0.30f, AutomaticSurfacePadding);
            }
        }

        private static void AddLimb(
            List<BodyColliderBuildDefinition> result,
            BodyColliderProfile profile,
            Animator animator,
            HumanBodyBones start,
            HumanBodyBones end,
            string id,
            BodyHitZone zone,
            float radiusScale)
        {
            Add(result, profile, id, zone, BodyColliderShape.Capsule, GetBone(animator, start), GetBone(animator, end), false, radiusScale, AutomaticSurfacePadding);
        }

        private static void AddAnchor(
            List<BodyColliderBuildDefinition> result,
            BodyColliderProfile profile,
            Animator animator,
            HumanBodyBones start,
            HumanBodyBones reference,
            string id,
            BodyHitZone zone,
            BodyColliderShape shape,
            float radiusScale)
        {
            Add(result, profile, id, zone, shape, GetBone(animator, start), GetBone(animator, reference), true, radiusScale, AutomaticSurfacePadding);
        }

        private static void Add(
            List<BodyColliderBuildDefinition> result,
            BodyColliderProfile profile,
            string id,
            BodyHitZone zone,
            BodyColliderShape shape,
            Transform start,
            Transform end,
            bool anchorAtStart,
            float radiusScale,
            float padding)
        {
            if (start == null || (shape != BodyColliderShape.Sphere && end == null))
            {
                return;
            }

            result.Add(new BodyColliderBuildDefinition(
                id,
                zone,
                null,
                shape,
                start,
                end,
                anchorAtStart,
                profile != null ? profile.GetRadiusScale(zone, radiusScale) : radiusScale,
                profile != null ? profile.GetPadding(zone, padding) : padding,
                profile != null ? profile.GetDamageMultiplier(zone) : 1f));
        }

        private static Transform GetBone(Animator animator, HumanBodyBones bone)
        {
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

        private static Transform FindBoneExcluding(List<Transform> bones, Transform excluded, params string[] aliases)
        {
            Transform found = FindBone(bones, aliases);
            return found == excluded ? null : found;
        }

        private static Transform FindBone(List<Transform> bones, params string[] aliases)
        {
            Transform best = null;
            int bestScore = int.MinValue;

            for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
            {
                Transform bone = bones[boneIndex];
                if (bone == null)
                {
                    continue;
                }

                string candidate = Normalize(bone.name);
                for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
                {
                    string alias = Normalize(aliases[aliasIndex]);
                    int score = Score(candidate, alias);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = bone;
                    }
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static int Score(string candidate, string alias)
        {
            if (candidate == alias)
            {
                return 10000;
            }

            if (candidate.EndsWith(alias, StringComparison.Ordinal))
            {
                return 5000 - candidate.Length;
            }

            return candidate.Contains(alias) ? 1000 - candidate.Length : int.MinValue;
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }
    }
}
