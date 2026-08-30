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

        private static readonly Correction[] Corrections =
        {
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
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/RootMotion/Attacks/Frank_RPG_2Hand_Combo05_2.fbx",
                "Left Upper Leg Twist In-Out", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/TwoHandedAxe/Animations/RootMotion/Attacks/Frank_RPG_2Hand_Combo05_2.fbx",
                "Left Lower Leg Stretch", 1.25000012f),
            new(
                "Assets/_Game/Animations/Frank/WhipFemale/Animations/InPlace/Equipment/Frank_Whip_Unequip_Run_Turbo2.fbx",
                "Left Foot Twist In-Out", 0.125f),
        };

        internal static int CorrectionCount => Corrections.Length;

        internal static int CorrectedClipCount => Corrections
            .Select(correction => correction.AssetPath)
            .Distinct(StringComparer.Ordinal)
            .Count();

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            foreach (var correction in Corrections)
            {
                if (!string.Equals(assetPath, correction.AssetPath, StringComparison.Ordinal))
                    continue;

                ApplyCorrection(clip, correction);
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
    }
}
