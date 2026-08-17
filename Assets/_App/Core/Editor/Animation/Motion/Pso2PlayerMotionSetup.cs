using System;
using System.Collections.Generic;
using System.IO;
using ModularAnimation.Unity;
using ModularAnimation.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.Animation
{
    public static class Pso2PlayerMotionSetup
    {
        private const string MotionRootPath = "pl_rbd_100000_bw";
        private const string GeneratedRoot =
            "Assets/_Game/Animations/Generated/PSO2NGS/Male100000/Motion";
        private const string AnimationProfilePath =
            "Assets/_Game/Settings/Character/Animations/Player/Player_MecanimAnimationProfile.asset";
        private const string CombatProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset";
        private const string DodgeSettingsPath =
            "Assets/_Game/Settings/Character/AbillityData/Abilities/DodgeAbilityData.asset";
        private const string TwoHandedSetPath =
            "Assets/_Game/Settings/Character/Animations/Player/TwoHanded/TwoHanded_CombatAnimationSet.asset";
        private const string UnarmedSetPath =
            "Assets/_Game/Settings/Character/Animations/Player/TwoHanded/Unarmed_CombatAnimationSet.asset";

        private static readonly Quaternion SourceToCharacterRotation =
            Quaternion.Euler(0.0f, 180.0f, 0.0f);

        [MenuItem("Tools/Project S/Animation/Bake Player Motion Trajectories")]
        public static void BakePlayerMotionTrajectories()
        {
            MecanimCharacterAnimationProfile animationProfile = LoadRequired<
                MecanimCharacterAnimationProfile>(AnimationProfilePath);
            _App.PlayerCombatActionProfile combatProfile = LoadRequired<
                _App.PlayerCombatActionProfile>(CombatProfilePath);
            _App.DodgeSettings dodgeSettings = LoadRequired<_App.DodgeSettings>(
                DodgeSettingsPath);
            _App.CombatAnimationSet twoHandedSet = LoadRequired<_App.CombatAnimationSet>(
                TwoHandedSetPath);
            _App.CombatAnimationSet unarmedSet = LoadRequired<_App.CombatAnimationSet>(
                UnarmedSetPath);

            RestoreSourceClipReferences(twoHandedSet);
            RestoreSourceClipReferences(unarmedSet);
            RestoreSourceClipReferences(animationProfile);

            var baked = new Dictionary<AnimationClip, AnimationMotionTrajectory>();
            var comboTrajectories = new AnimationMotionTrajectory[
                twoHandedSet.ComboActions.Count];
            for (int i = 0; i < twoHandedSet.ComboActions.Count; i++)
            {
                AnimationClip source = twoHandedSet.ComboActions[i]?.Clip;
                AnimationMotionTrajectory result = Bake(source, baked);
                comboTrajectories[i] = result.HasPlanarTranslation
                    ? result
                    : null;
            }

            if (!twoHandedSet.TryGetNamedAction(
                    _App.PlayerCombatAnimationIds.Special,
                    out _App.CharacterAnimationActionPresentation specialPresentation))
            {
                throw new InvalidOperationException(
                    $"'{TwoHandedSetPath}' requires the special combat animation.");
            }

            AnimationMotionTrajectory specialResult = Bake(
                specialPresentation.Clip,
                baked);
            AnimationMotionTrajectory specialTrajectory =
                specialResult.HasPlanarTranslation
                    ? specialResult
                    : null;

            MecanimAnimationStateAuthoring forwardDodge = FindState(
                animationProfile,
                "Locomotion.Dodge.Forward");
            MecanimAnimationStateAuthoring backwardDodge = FindState(
                animationProfile,
                "Locomotion.Dodge.Backward");
            AnimationMotionTrajectory forwardDodgeResult = Bake(
                forwardDodge.Clip,
                baked);
            AnimationMotionTrajectory backwardDodgeResult = Bake(
                backwardDodge.Clip,
                baked);

            ConfigureCombatProfile(
                combatProfile,
                comboTrajectories,
                specialTrajectory);
            ConfigureDodge(
                dodgeSettings,
                animationProfile,
                forwardDodgeResult,
                backwardDodgeResult);

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Baked {baked.Count} player motion trajectories. " +
                "Combat and dodge now use collision-resolved motor motion.");
        }

        private static AnimationMotionTrajectory Bake(
            AnimationClip source,
            IDictionary<AnimationClip, AnimationMotionTrajectory> cache)
        {
            if (source == null)
            {
                throw new InvalidOperationException("A source animation clip is missing.");
            }

            if (cache.TryGetValue(source, out AnimationMotionTrajectory cached))
            {
                return cached;
            }

            string safeName = SanitizeFileName(source.name);
            var request = new AnimationMotionBakeRequest(
                source,
                MotionRootPath,
                $"{GeneratedRoot}/{safeName}_Motion.asset",
                SourceToCharacterRotation,
                0.6f,
                60,
                false,
                false);
            AnimationMotionTrajectory result = AnimationMotionTrajectoryBaker.Bake(
                in request);
            cache.Add(source, result);
            return result;
        }

        private static void ConfigureCombatProfile(
            _App.PlayerCombatActionProfile profile,
            IReadOnlyList<AnimationMotionTrajectory> comboTrajectories,
            AnimationMotionTrajectory specialTrajectory)
        {
            var serialized = new SerializedObject(profile);
            SerializedProperty actions = serialized.FindProperty("_actions");
            for (int i = 0; i < actions.arraySize; i++)
            {
                SerializedProperty action = actions.GetArrayElementAtIndex(i);
                int presentation = action.FindPropertyRelative("_presentation").enumValueIndex;
                AnimationMotionTrajectory trajectory = null;
                if (presentation == (int)_App.PlayerCombatPresentationKind.ComboStep)
                {
                    int comboIndex = action.FindPropertyRelative("_comboIndex").intValue;
                    if (comboIndex >= 0 && comboIndex < comboTrajectories.Count)
                    {
                        trajectory = comboTrajectories[comboIndex];
                    }
                }
                else if (string.Equals(
                             action.FindPropertyRelative("_animationId").stringValue,
                             _App.PlayerCombatAnimationIds.Special.Value,
                             StringComparison.Ordinal))
                {
                    trajectory = specialTrajectory;
                }

                SerializedProperty timeline = action.FindPropertyRelative("_action");
                timeline.FindPropertyRelative("_motionTrajectory").objectReferenceValue = trajectory;
                timeline.FindPropertyRelative("_motionTrajectoryScale").floatValue = 1.0f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void ConfigureDodge(
            _App.DodgeSettings settings,
            MecanimCharacterAnimationProfile animationProfile,
            AnimationMotionTrajectory forward,
            AnimationMotionTrajectory backward)
        {
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("_forwardMotionTrajectory").objectReferenceValue =
                forward.HasPlanarTranslation ? forward : null;
            serializedSettings.FindProperty("_backwardMotionTrajectory").objectReferenceValue =
                backward.HasPlanarTranslation ? backward : null;
            serializedSettings.FindProperty("_motionTrajectoryScale").floatValue = 1.0f;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            var serializedProfile = new SerializedObject(animationProfile);
            serializedProfile.FindProperty("_planarMotionRootPath").stringValue =
                MotionRootPath;
            SerializedProperty states = serializedProfile.FindProperty("_states");
            for (int i = 0; i < states.arraySize; i++)
            {
                SerializedProperty state = states.GetArrayElementAtIndex(i);
                string slot = state.FindPropertyRelative("_slot").stringValue;
                if (slot == "Locomotion.Dodge.Forward" ||
                    slot == "Locomotion.Dodge.Backward")
                {
                    state.FindPropertyRelative("_expectedDuration").floatValue =
                        settings.Duration;
                }
            }

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animationProfile);
        }

        private static void RestoreSourceClipReferences(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue is not AnimationClip clip)
                {
                    continue;
                }

                string clipPath = AssetDatabase.GetAssetPath(clip);
                if (!clipPath.StartsWith(GeneratedRoot, StringComparison.Ordinal) ||
                    !clip.name.EndsWith("_InPlace", StringComparison.Ordinal))
                {
                    continue;
                }

                string sourceName = clip.name.Substring(
                    0,
                    clip.name.Length - "_InPlace".Length);
                AnimationMotionTrajectory trajectory =
                    AssetDatabase.LoadAssetAtPath<AnimationMotionTrajectory>(
                        $"{GeneratedRoot}/{sourceName}_Motion.asset");
                if (trajectory == null || trajectory.SourceClip == null)
                {
                    throw new InvalidOperationException(
                        $"Generated clip '{clipPath}' has no matching source trajectory.");
                }

                property.objectReferenceValue = trajectory.SourceClip;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static MecanimAnimationStateAuthoring FindState(
            MecanimCharacterAnimationProfile profile,
            string slot)
        {
            for (int i = 0; i < profile.States.Count; i++)
            {
                MecanimAnimationStateAuthoring state = profile.States[i];
                if (state != null && string.Equals(state.Slot, slot, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            throw new InvalidOperationException(
                $"Animation profile '{profile.name}' has no '{slot}' state.");
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Required asset '{path}' was not found.");
        }

        private static string SanitizeFileName(string value)
        {
            string result = value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                result = result.Replace(invalid[i], '_');
            }

            return result;
        }
    }
}
