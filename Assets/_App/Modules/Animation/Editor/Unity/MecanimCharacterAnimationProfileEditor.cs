using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
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
            EditorGUILayout.LabelField("Controller Contract", EditorStyles.boldLabel);
            DrawValidation((MecanimCharacterAnimationProfile)target);
        }

        private static void DrawValidation(MecanimCharacterAnimationProfile profile)
        {
            if (profile.ValidationController == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a validation controller to check semantic slots before entering Play Mode.",
                    MessageType.Info);
                return;
            }

            if (profile.ValidationController is not AnimatorController controller)
            {
                EditorGUILayout.HelpBox(
                    "The validation controller must be an AnimatorController asset.",
                    MessageType.Error);
                return;
            }

            IReadOnlyList<MecanimAnimationStateAuthoring> states = profile.States;
            var slots = new HashSet<string>(System.StringComparer.Ordinal);
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
            }

            for (int i = 0; i < states.Count; i++)
            {
                MecanimAnimationStateAuthoring mapping = states[i];
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.Slot))
                {
                    continue;
                }

                ValidateMapping(controller, mapping, slots);
            }

            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox("No semantic animation states are configured.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Inspected {states.Count} semantic animation mapping(s).",
                    MessageType.None);
            }
        }

        private static void ValidateMapping(
            AnimatorController controller,
            MecanimAnimationStateAuthoring mapping,
            HashSet<string> slots)
        {
            bool required = mapping.Required;
            string fallback = mapping.FallbackSlot?.Trim();
            if (!string.IsNullOrEmpty(fallback) && !slots.Contains(fallback))
            {
                DrawIssue(
                    $"'{mapping.Slot}' references missing fallback slot '{fallback}'.",
                    required);
            }

            AnimatorControllerLayer layer = FindLayer(controller, mapping.Layer);
            if (layer == null)
            {
                DrawIssue(
                    $"'{mapping.Slot}' references missing layer '{mapping.Layer}'.",
                    required);
                return;
            }

            if (!TryFindState(layer, mapping.State, out AnimatorState animatorState))
            {
                DrawIssue(
                    $"'{mapping.Slot}' references missing state '{mapping.State}' on layer '{layer.name}'.",
                    required);
                return;
            }

            if (animatorState.motion == null)
            {
                DrawIssue($"'{mapping.Slot}' resolves to state '{animatorState.name}' without a Motion.", required);
            }
            else if (mapping.ExpectedDuration <= 0.0f && animatorState.motion.averageDuration <= 0.0f)
            {
                DrawIssue(
                    $"'{mapping.Slot}' has no expected duration and its Motion has zero duration.",
                    required);
            }
        }

        private static AnimatorControllerLayer FindLayer(AnimatorController controller, string name)
        {
            for (int i = 0; i < controller.layers.Length; i++)
            {
                AnimatorControllerLayer layer = controller.layers[i];
                if (string.Equals(layer.name, name?.Trim(), System.StringComparison.Ordinal))
                {
                    return layer;
                }
            }

            return null;
        }

        private static bool TryFindState(
            AnimatorControllerLayer layer,
            string configuredPath,
            out AnimatorState state)
        {
            string path = configuredPath?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                state = null;
                return false;
            }

            string layerPrefix = layer.name + ".";
            if (path.StartsWith(layerPrefix, System.StringComparison.Ordinal))
            {
                path = path.Substring(layerPrefix.Length);
            }

            var matches = new List<AnimatorState>(2);
            CollectMatches(layer.stateMachine, string.Empty, path, matches);
            if (matches.Count == 1)
            {
                state = matches[0];
                return true;
            }

            state = null;
            return false;
        }

        private static void CollectMatches(
            AnimatorStateMachine machine,
            string prefix,
            string configuredPath,
            List<AnimatorState> matches)
        {
            for (int i = 0; i < machine.states.Length; i++)
            {
                AnimatorState state = machine.states[i].state;
                string path = prefix + state.name;
                if (string.Equals(path, configuredPath, System.StringComparison.Ordinal) ||
                    (!configuredPath.Contains(".") &&
                     string.Equals(state.name, configuredPath, System.StringComparison.Ordinal)))
                {
                    matches.Add(state);
                }
            }

            for (int i = 0; i < machine.stateMachines.Length; i++)
            {
                AnimatorStateMachine child = machine.stateMachines[i].stateMachine;
                CollectMatches(child, prefix + child.name + ".", configuredPath, matches);
            }
        }

        private static void DrawIssue(string message, bool error)
        {
            EditorGUILayout.HelpBox(message, error ? MessageType.Error : MessageType.Warning);
        }
    }
}
