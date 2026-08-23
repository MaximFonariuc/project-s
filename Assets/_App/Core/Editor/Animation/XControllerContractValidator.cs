using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class XControllerContractValidator
    {
        private const string ControllerPath =
            "Assets/_Game/Animations/Character/1. Controllers/XController.controller";
        private const string PlayerPrefabPath =
            "Assets/_Game/Prefabs/Character/Player.prefab";
        private const string TrainingTargetPrefabPath =
            "Assets/_Game/Prefabs/Enemies/CombatTrainingTarget.prefab";
        private const string MenuPath =
            "Tools/Animation/Validate XController Contract";

        private static readonly string[] RequiredMotionStatePaths =
        {
            XControllerAnimationContract.MoveStatePath,
            XControllerAnimationContract.RollForwardStatePath,
            XControllerAnimationContract.RollBackwardStatePath,
            XControllerAnimationContract.Attack1StatePath,
            XControllerAnimationContract.Attack2StatePath,
            XControllerAnimationContract.Attack3StatePath,
            XControllerAnimationContract.Attack4StatePath,
            XControllerAnimationContract.HitStunStatePath,
            XControllerAnimationContract.KnockdownStatePath,
            XControllerAnimationContract.AirborneReactionStatePath,
            XControllerAnimationContract.DeadStatePath,
            XControllerAnimationContract.WeaponSheatheStatePath,
            XControllerAnimationContract.WeaponUnsheatheStatePath
        };

        private static readonly string[] StatesWithAuthoredExit =
        {
            XControllerAnimationContract.RollForwardStatePath,
            XControllerAnimationContract.RollBackwardStatePath,
            XControllerAnimationContract.Attack1StatePath,
            XControllerAnimationContract.Attack2StatePath,
            XControllerAnimationContract.Attack3StatePath,
            XControllerAnimationContract.Attack4StatePath
        };

        [MenuItem(MenuPath)]
        private static void ValidateMenu()
        {
            ValidationResult result = ValidateProject();
            string report = result.BuildReport();
            if (result.IsValid)
            {
                Debug.Log(report, result.Controller);
            }
            else
            {
                Debug.LogError(report, result.Controller);
            }
        }

        internal static ValidationResult ValidateProject()
        {
            var result = new ValidationResult();
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            result.Controller = controller;
            if (controller == null)
            {
                result.Error($"Missing controller: {ControllerPath}");
                return result;
            }

            ValidateController(controller, result);
            ValidateOverrideControllers(controller, result);
            ValidatePrefab(PlayerPrefabPath, controller, true, result);
            ValidatePrefab(TrainingTargetPrefabPath, controller, false, result);
            return result;
        }

        private static void ValidateController(
            AnimatorController controller,
            ValidationResult result)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0 ||
                layers[XControllerAnimationContract.BaseLayerIndex].name !=
                XControllerAnimationContract.BaseLayerName)
            {
                result.Error(
                    $"Layer {XControllerAnimationContract.BaseLayerIndex} must be " +
                    $"'{XControllerAnimationContract.BaseLayerName}'.");
                return;
            }

            ValidateLayer(
                layers,
                XControllerAnimationContract.SwitchWeaponLayerName,
                AnimatorLayerBlendingMode.Override,
                true,
                result);
            ValidateLayer(
                layers,
                XControllerAnimationContract.BodyLookLayerName,
                AnimatorLayerBlendingMode.Additive,
                false,
                result);
            ValidateLayer(
                layers,
                XControllerAnimationContract.HeadLookLayerName,
                AnimatorLayerBlendingMode.Additive,
                false,
                result);
            ValidateParameters(controller, result);

            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            for (int i = 0; i < layers.Length; i++)
            {
                CollectStates(layers[i].stateMachine, layers[i].name, states, result);
            }

            ValidateRequiredStates(
                XControllerAnimationContract.RequiredBaseLayerStatePaths,
                states,
                result);
            ValidateRequiredStates(
                XControllerAnimationContract.RequiredSwitchWeaponStatePaths,
                states,
                result);
            ValidateMotions(states, result);
            ValidateFiniteStateTransitions(states, result);
            ValidateReactionStates(states, result);
        }

        private static void ValidateLayer(
            AnimatorControllerLayer[] layers,
            string layerName,
            AnimatorLayerBlendingMode blendingMode,
            bool requiresMask,
            ValidationResult result)
        {
            AnimatorControllerLayer[] matches = layers
                .Where(layer => layer.name == layerName)
                .ToArray();
            if (matches.Length != 1)
            {
                result.Error(
                    $"Expected one layer '{layerName}', found {matches.Length}.");
                return;
            }

            AnimatorControllerLayer layer = matches[0];
            if (layer.blendingMode != blendingMode)
            {
                result.Error(
                    $"Layer '{layerName}' must use {blendingMode} blending.");
            }

            if (layer.defaultWeight < 0.999f)
            {
                result.Error($"Layer '{layerName}' must have default weight 1.");
            }

            if (requiresMask && layer.avatarMask == null)
            {
                result.Error($"Layer '{layerName}' requires an Avatar Mask.");
            }

            if (layer.stateMachine.defaultState == null ||
                layer.stateMachine.defaultState.motion == null &&
                layerName != XControllerAnimationContract.SwitchWeaponLayerName)
            {
                result.Error($"Layer '{layerName}' requires a default motion.");
            }
        }

        private static void ValidateParameters(
            AnimatorController controller,
            ValidationResult result)
        {
            IReadOnlyList<XControllerParameterRequirement> requirements =
                XControllerAnimationContract.RequiredParameters;
            for (int i = 0; i < requirements.Count; i++)
            {
                XControllerParameterRequirement requirement = requirements[i];
                AnimatorControllerParameter[] matches = controller.parameters
                    .Where(parameter => parameter.name == requirement.Name)
                    .ToArray();
                if (matches.Length != 1)
                {
                    result.Error(
                        $"Expected one parameter '{requirement.Name}', " +
                        $"found {matches.Length}.");
                    continue;
                }

                if (matches[0].type != requirement.Type)
                {
                    result.Error(
                        $"Parameter '{requirement.Name}' must be " +
                        $"{requirement.Type}, found {matches[0].type}.");
                }
            }
        }

        private static void CollectStates(
            AnimatorStateMachine stateMachine,
            string prefix,
            Dictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            ChildAnimatorState[] childStates = stateMachine.states;
            for (int i = 0; i < childStates.Length; i++)
            {
                AnimatorState state = childStates[i].state;
                string path = $"{prefix}.{state.name}";
                if (!states.TryAdd(path, state))
                {
                    result.Error($"Duplicate Animator state path '{path}'.");
                }
            }

            ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
            for (int i = 0; i < childMachines.Length; i++)
            {
                AnimatorStateMachine child = childMachines[i].stateMachine;
                CollectStates(child, $"{prefix}.{child.name}", states, result);
            }
        }

        private static void ValidateRequiredStates(
            IReadOnlyList<string> requiredPaths,
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            for (int i = 0; i < requiredPaths.Count; i++)
            {
                if (!states.ContainsKey(requiredPaths[i]))
                {
                    result.Error($"Missing required state '{requiredPaths[i]}'.");
                }
            }
        }

        private static void ValidateMotions(
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            for (int i = 0; i < RequiredMotionStatePaths.Length; i++)
            {
                string path = RequiredMotionStatePaths[i];
                if (!states.TryGetValue(path, out AnimatorState state))
                {
                    continue;
                }

                if (state.motion == null)
                {
                    result.Error($"State '{path}' has no motion.");
                    continue;
                }

                var clips = new HashSet<AnimationClip>();
                CollectClips(state.motion, clips);
                if (clips.Count == 0)
                {
                    result.Error($"State '{path}' does not resolve to an AnimationClip.");
                    continue;
                }

                foreach (AnimationClip clip in clips)
                {
                    if (!clip.humanMotion || clip.legacy)
                    {
                        result.Error(
                            $"State '{path}' uses non-Humanoid clip '{clip.name}'.");
                    }
                }
            }
        }

        private static void CollectClips(Motion motion, HashSet<AnimationClip> clips)
        {
            if (motion is AnimationClip clip)
            {
                clips.Add(clip);
                return;
            }

            if (motion is not BlendTree blendTree)
            {
                return;
            }

            ChildMotion[] children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion != null)
                {
                    CollectClips(children[i].motion, clips);
                }
            }
        }

        private static void ValidateFiniteStateTransitions(
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            for (int i = 0; i < StatesWithAuthoredExit.Length; i++)
            {
                string path = StatesWithAuthoredExit[i];
                if (states.TryGetValue(path, out AnimatorState state) &&
                    state.transitions.Length == 0)
                {
                    result.Error(
                        $"Finite state '{path}' requires an authored exit transition.");
                }
            }
        }

        private static void ValidateReactionStates(
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            IReadOnlyList<string> reactionPaths =
                XControllerAnimationContract.ReactionStatePaths;
            for (int i = 0; i < reactionPaths.Count; i++)
            {
                string path = reactionPaths[i];
                if (!states.TryGetValue(path, out AnimatorState state))
                {
                    continue;
                }

                if (state.transitions.Length != 0)
                {
                    result.Error(
                        $"Reaction state '{path}' must be exited by gameplay cancellation.");
                }

                var clips = new HashSet<AnimationClip>();
                CollectClips(state.motion, clips);
                foreach (AnimationClip clip in clips)
                {
                    if ((path == XControllerAnimationContract.HitStunStatePath ||
                         path == XControllerAnimationContract.KnockdownStatePath ||
                         path == XControllerAnimationContract.DeadStatePath) &&
                        clip.isLooping)
                    {
                        result.Error(
                            $"One-shot reaction '{path}' uses looping clip '{clip.name}'.");
                    }

                    if (AnimationUtility.GetAnimationEvents(clip).Length > 0)
                    {
                        result.Warning(
                            $"Reaction clip '{clip.name}' contains Animation Events; " +
                            "gameplay ownership must remain outside Animator.");
                    }
                }
            }
        }

        private static void ValidateOverrideControllers(
            AnimatorController baseController,
            ValidationResult result)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:AnimatorOverrideController",
                new[] { "Assets/_Game" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AnimatorOverrideController animationOverride =
                    AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (animationOverride == null ||
                    ResolveBaseController(animationOverride) != baseController)
                {
                    continue;
                }

                var overrides =
                    new List<KeyValuePair<AnimationClip, AnimationClip>>(
                        animationOverride.overridesCount);
                animationOverride.GetOverrides(overrides);
                for (int j = 0; j < overrides.Count; j++)
                {
                    AnimationClip source = overrides[j].Key;
                    AnimationClip replacement = overrides[j].Value;
                    if (replacement == null)
                    {
                        continue;
                    }

                    if (source != null &&
                        source.humanMotion &&
                        (!replacement.humanMotion || replacement.legacy))
                    {
                        result.Error(
                            $"Override '{path}' replaces Humanoid slot " +
                            $"'{source.name}' with incompatible clip " +
                            $"'{replacement.name}'.");
                    }
                }
            }
        }

        private static void ValidatePrefab(
            string prefabPath,
            AnimatorController baseController,
            bool validateLoadout,
            ValidationResult result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                result.Error($"Missing prefab: {prefabPath}");
                return;
            }

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                result.Error($"Prefab '{prefabPath}' has no Animator.");
                return;
            }

            if (animator.avatar == null ||
                !animator.avatar.isValid ||
                !animator.avatar.isHuman)
            {
                result.Error($"Prefab '{prefabPath}' requires a valid Humanoid Avatar.");
            }

            if (ResolveBaseController(animator.runtimeAnimatorController) !=
                baseController)
            {
                result.Error(
                    $"Prefab '{prefabPath}' does not resolve to " +
                    $"'{baseController.name}'.");
            }

            if (animator.applyRootMotion)
            {
                result.Error(
                    $"Prefab '{prefabPath}' must keep Animator root motion disabled.");
            }

            if (validateLoadout)
            {
                ValidatePlayerLoadout(prefab, prefabPath, baseController, result);
            }
        }

        private static void ValidatePlayerLoadout(
            GameObject prefab,
            string prefabPath,
            AnimatorController baseController,
            ValidationResult result)
        {
            ThirdPersonController player = prefab.GetComponent<ThirdPersonController>();
            if (player == null)
            {
                result.Error($"Prefab '{prefabPath}' has no ThirdPersonController.");
                return;
            }

            var serializedPlayer = new SerializedObject(player);
            SerializedProperty bindings =
                serializedPlayer.FindProperty("_animationOverrides");
            if (bindings == null)
            {
                result.Error(
                    $"Prefab '{prefabPath}' is missing animation override bindings.");
                return;
            }

            var weaponTypes = new HashSet<int>();
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                SerializedProperty weaponType =
                    binding.FindPropertyRelative("_weaponType");
                SerializedProperty controller =
                    binding.FindPropertyRelative("_controller");
                if (controller.objectReferenceValue == null)
                {
                    result.Error(
                        $"Player animation override entry {i} has a missing controller.");
                    continue;
                }

                if (!weaponTypes.Add(weaponType.enumValueIndex))
                {
                    result.Error(
                        $"Player has duplicate animation override weapon " +
                        $"'{weaponType.enumDisplayNames[weaponType.enumValueIndex]}'.");
                }

                var animationOverride =
                    controller.objectReferenceValue as AnimatorOverrideController;
                if (animationOverride == null ||
                    ResolveBaseController(animationOverride) != baseController)
                {
                    result.Error(
                        $"Player override entry {i} does not use " +
                        $"'{baseController.name}' as its base.");
                }
            }
        }

        private static RuntimeAnimatorController ResolveBaseController(
            RuntimeAnimatorController controller)
        {
            RuntimeAnimatorController current = controller;
            while (current is AnimatorOverrideController animationOverride)
            {
                current = animationOverride.runtimeAnimatorController;
            }

            return current;
        }

        internal sealed class ValidationResult
        {
            private readonly List<string> _errors = new List<string>();
            private readonly List<string> _warnings = new List<string>();

            public AnimatorController Controller { get; set; }
            public bool IsValid => _errors.Count == 0;

            public void Error(string message)
            {
                _errors.Add(message);
            }

            public void Warning(string message)
            {
                _warnings.Add(message);
            }

            public string BuildReport()
            {
                var report = new StringBuilder();
                report.Append("[XController Contract] ");
                report.Append(IsValid ? "PASS" : "FAIL");
                report.Append(" — ");
                report.Append(_errors.Count);
                report.Append(" error(s), ");
                report.Append(_warnings.Count);
                report.AppendLine(" warning(s).");

                for (int i = 0; i < _errors.Count; i++)
                {
                    report.Append("ERROR: ");
                    report.AppendLine(_errors[i]);
                }

                for (int i = 0; i < _warnings.Count; i++)
                {
                    report.Append("WARNING: ");
                    report.AppendLine(_warnings[i]);
                }

                return report.ToString().TrimEnd();
            }
        }
    }
}
