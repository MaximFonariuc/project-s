using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAnimation.Unity.Editor
{
    [CustomEditor(typeof(MecanimCharacterAnimationProfile))]
    public sealed class MecanimCharacterAnimationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playable Graph Contract", EditorStyles.boldLabel);
            DrawValidation((MecanimCharacterAnimationProfile)target);
        }

        private static void DrawValidation(MecanimCharacterAnimationProfile profile)
        {
            var clips = new HashSet<AnimationClip>();
            MecanimLocomotionClipSet locomotion = profile.Locomotion;
            if (locomotion == null || locomotion.Idle == null)
            {
                DrawIssue("Locomotion requires an idle clip.", true);
            }
            else
            {
                AddClip(locomotion.Idle, clips);
                AddDirectional(locomotion.Walk, clips);
                AddDirectional(locomotion.Run, clips);
                AddDirectional(locomotion.Sprint, clips);
            }

            IReadOnlyList<MecanimAnimationStateAuthoring> states = profile.States;
            var slots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < states.Count; i++)
            {
                MecanimAnimationStateAuthoring state = states[i];
                if (state == null)
                {
                    DrawIssue($"Mapping {i} is null.", true);
                    continue;
                }

                string slot = state.Slot?.Trim();
                if (string.IsNullOrEmpty(slot))
                {
                    DrawIssue($"Mapping {i} has no semantic slot.", true);
                    continue;
                }

                if (!slots.Add(slot))
                {
                    DrawIssue($"Semantic slot '{slot}' is duplicated.", true);
                }

                if (!state.UsesLocomotion && state.Clip == null &&
                    string.IsNullOrWhiteSpace(state.FallbackSlot))
                {
                    DrawIssue($"'{slot}' has neither a clip nor a fallback.", state.Required);
                }

                AddClip(state.Clip, clips);
            }

            for (int i = 0; i < states.Count; i++)
            {
                MecanimAnimationStateAuthoring state = states[i];
                string fallback = state?.FallbackSlot?.Trim();
                if (!string.IsNullOrEmpty(fallback) && !slots.Contains(fallback))
                {
                    DrawIssue(
                        $"'{state.Slot}' references missing fallback slot '{fallback}'.",
                        state.Required);
                }
            }

            ValidateClipKinds(clips);
            EditorGUILayout.HelpBox(
                $"Configured {states.Count} semantic slot(s) and {clips.Count} unique clip(s). " +
                "Rig compatibility is validated against the Animator Avatar when the gameplay scope is created.",
                MessageType.None);
        }

        private static void AddDirectional(
            MecanimDirectionalClipSet directional,
            HashSet<AnimationClip> clips)
        {
            if (directional == null)
            {
                return;
            }

            AddClip(directional.Forward, clips);
            AddClip(directional.Backward, clips);
            AddClip(directional.Left, clips);
            AddClip(directional.Right, clips);
        }

        private static void AddClip(AnimationClip clip, HashSet<AnimationClip> clips)
        {
            if (clip != null)
            {
                clips.Add(clip);
            }
        }

        private static void ValidateClipKinds(HashSet<AnimationClip> clips)
        {
            bool hasHuman = false;
            bool hasGeneric = false;
            foreach (AnimationClip clip in clips)
            {
                if (clip.legacy)
                {
                    DrawIssue($"Clip '{clip.name}' is Legacy.", true);
                }

                hasHuman |= clip.humanMotion;
                hasGeneric |= !clip.humanMotion;
            }

            if (hasHuman && hasGeneric)
            {
                DrawIssue(
                    "The profile mixes Generic and Humanoid clips. Use one profile per rig family.",
                    true);
            }
        }

        private static void DrawIssue(string message, bool error)
        {
            EditorGUILayout.HelpBox(message, error ? MessageType.Error : MessageType.Warning);
        }
    }
}
