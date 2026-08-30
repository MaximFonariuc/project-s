using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    internal sealed class FrankHumanoidRetargetPostprocessor : AssetPostprocessor
    {
        private const float KeyTimeTolerance = 0.0002f;

        private readonly struct Correction
        {
            public Correction(string assetPath, string property, float time)
            {
                AssetPath = assetPath;
                Property = property;
                Time = time;
            }

            public string AssetPath { get; }
            public string Property { get; }
            public float Time { get; }
        }

        private readonly struct MuscleRangeCorrection
        {
            public MuscleRangeCorrection(
                string assetPath,
                string property,
                float minimum,
                float maximum)
            {
                AssetPath = assetPath;
                Property = property;
                Minimum = minimum;
                Maximum = maximum;
            }

            public string AssetPath { get; }
            public string Property { get; }
            public float Minimum { get; }
            public float Maximum { get; }
        }

        private static readonly Correction[] Corrections =
        {
            new(
                "Assets/_Game/Animations/Frank/DualSwords/Animations/RootMotion/Frank_RPG_Dual_Jump_02.fbx",
                "Right Lower Leg Twist In-Out", 0.18333334f),
            new(
                "Assets/_Game/Animations/Frank/StealthKills/Animations/RootMotion/Movement/Frank_Stealth_Kill_Sneak_Run_FL.fbx",
                "Left Foot Twist In-Out", 0.18750001f),
            new(
                "Assets/_Game/Animations/Frank/SwordTwoFemale/Animations/InPlace/Equipment/Frank_Sword2_equip_Run_Turbo2.fbx",
                "Left Foot Twist In-Out", 0.125f),
            new(
                "Assets/_Game/Animations/Frank/SwordTwoFemale/Animations/InPlace/Equipment/Frank_Sword2_Unequip_Run_Turbo2.fbx",
                "Left Foot Twist In-Out", 0.125f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/InPlace/Attacks/Frank_RPG_2Hand_Combo05_2_in_Place.fbx",
                "Left Upper Leg Twist In-Out", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/InPlace/Attacks/Frank_RPG_2Hand_Combo05_2_in_Place.fbx",
                "Left Lower Leg Stretch", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/InPlace/Attacks/Frank_RPG_2Hand_Combo05_2_in_Place.fbx",
                "Left Lower Leg Twist In-Out", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/RootMotion/Attacks/Frank_RPG_2Hand_Combo05_2.fbx",
                "Left Upper Leg Twist In-Out", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/RootMotion/Attacks/Frank_RPG_2Hand_Combo05_2.fbx",
                "Left Lower Leg Stretch", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/RootMotion/Attacks/Frank_RPG_2Hand_Combo05_2.fbx",
                "Left Lower Leg Twist In-Out", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/WhipFemale/Animations/InPlace/Equipment/Frank_Whip_Unequip_Run_Turbo2.fbx",
                "Left Foot Twist In-Out", 0.125f),
        };

        private static readonly MuscleRangeCorrection[] MuscleRangeCorrections =
        {
            new(
                "Assets/_Game/Animations/Frank/DualSwords/Animations/InPlace/Frank_RPG_Dual_Air_Combo_1_IP.fbx",
                "Left Foot Up-Down", -1f, 1f),
            new(
                "Assets/_Game/Animations/Frank/DualSwords/Animations/InPlace/Frank_RPG_Dual_Air_Combo_1_IP.fbx",
                "Left Foot Twist In-Out", -1f, 1f),
            new(
                "Assets/_Game/Animations/Frank/DualSwords/Animations/InPlace/Frank_RPG_Dual_Air_Combo_1_IP.fbx",
                "Right Foot Up-Down", -1f, 1f),
            new(
                "Assets/_Game/Animations/Frank/DualSwords/Animations/InPlace/Frank_RPG_Dual_Air_Combo_1_IP.fbx",
                "Right Foot Twist In-Out", -1f, 1f),
        };

        internal static int CorrectionCount =>
            Corrections.Length + MuscleRangeCorrections.Length;

        internal static int CorrectedClipCount => Corrections
            .Select(correction => correction.AssetPath)
            .Concat(MuscleRangeCorrections.Select(correction => correction.AssetPath))
            .Distinct(StringComparer.Ordinal)
            .Count();

        internal static bool TryGetAllowedMuscleRange(
            string targetAssetPath,
            string property,
            out float minimum,
            out float maximum)
        {
            foreach (var correction in MuscleRangeCorrections)
            {
                if (!string.Equals(
                        targetAssetPath, correction.AssetPath, StringComparison.Ordinal)
                    || !string.Equals(
                        property, correction.Property, StringComparison.Ordinal))
                {
                    continue;
                }

                minimum = correction.Minimum;
                maximum = correction.Maximum;
                return true;
            }

            minimum = 0f;
            maximum = 0f;
            return false;
        }

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            foreach (var correction in Corrections)
            {
                if (!string.Equals(assetPath, correction.AssetPath, StringComparison.Ordinal))
                    continue;

                ApplyCorrection(clip, correction);
            }

            foreach (var correction in MuscleRangeCorrections)
            {
                if (!string.Equals(assetPath, correction.AssetPath, StringComparison.Ordinal))
                    continue;

                ApplyRangeCorrection(clip, correction);
            }
        }

        private static void ApplyCorrection(AnimationClip clip, Correction correction)
        {
            var binding = AnimationUtility.GetCurveBindings(clip)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.propertyName, correction.Property, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(binding.propertyName))
                throw new InvalidOperationException(
                    $"Frank Humanoid correction curve is missing: {correction.AssetPath} / "
                    + correction.Property);

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var keys = curve.keys;
            var keyIndex = -1;
            for (var index = 1; index < keys.Length - 1; index++)
            {
                if (Mathf.Abs(keys[index].time - correction.Time) <= KeyTimeTolerance)
                {
                    keyIndex = index;
                    break;
                }
            }

            if (keyIndex < 1 || keyIndex >= keys.Length - 1)
                throw new InvalidOperationException(
                    $"Frank Humanoid correction key is missing: {correction.AssetPath} / "
                    + $"{correction.Property} @ {correction.Time:F6}.");

            var previous = keys[keyIndex - 1];
            var current = keys[keyIndex];
            var next = keys[keyIndex + 1];
            var interpolation = Mathf.InverseLerp(previous.time, next.time, current.time);
            current.value = Mathf.Lerp(previous.value, next.value, interpolation);
            current.weightedMode = WeightedMode.None;
            curve.MoveKey(keyIndex, current);
            AnimationUtility.SetKeyRightTangentMode(
                curve, keyIndex - 1, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(
                curve, keyIndex, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(
                curve, keyIndex, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(
                curve, keyIndex + 1, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void ApplyRangeCorrection(
            AnimationClip clip,
            MuscleRangeCorrection correction)
        {
            var binding = AnimationUtility.GetCurveBindings(clip)
                .FirstOrDefault(candidate => string.Equals(
                    candidate.propertyName, correction.Property, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(binding.propertyName))
                throw new InvalidOperationException(
                    $"Frank Humanoid range-correction curve is missing: "
                    + $"{correction.AssetPath} / {correction.Property}");

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var requiredScale = 1f;
            foreach (var key in curve.keys)
            {
                if (key.value < correction.Minimum)
                    requiredScale = Mathf.Max(
                        requiredScale, key.value / correction.Minimum);
                else if (key.value > correction.Maximum)
                    requiredScale = Mathf.Max(
                        requiredScale, key.value / correction.Maximum);
            }

            if (requiredScale <= 1f)
                return;

            var valueScale = 1f / requiredScale;
            var keys = curve.keys;
            for (var index = 0; index < keys.Length; index++)
            {
                var key = keys[index];
                key.value *= valueScale;
                if (!float.IsInfinity(key.inTangent))
                    key.inTangent *= valueScale;
                if (!float.IsInfinity(key.outTangent))
                    key.outTangent *= valueScale;
                keys[index] = key;
            }

            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
