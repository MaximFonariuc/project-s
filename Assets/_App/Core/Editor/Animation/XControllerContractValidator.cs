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
        private const string CombatActionProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset";
        private const string CharacterReactionProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCharacterReactionProfile.asset";
        private const string MenuPath =
            "Tools/Animation/Validate XController Contract";
        private const float TransitionTimingTolerance = 0.005f;
        private const float DodgeDurationToleranceSeconds = 0.05f;
        private const float CombatClipDurationToleranceSeconds = 0.02f;
        private const float ReactionClipDurationToleranceSeconds = 0.02f;

        private static readonly string[] RequiredMotionStatePaths =
        {
            XControllerAnimationContract.MoveStatePath,
            XControllerAnimationContract.RollForwardStatePath,
            XControllerAnimationContract.RollBackwardStatePath,
            XControllerAnimationContract.Attack1StatePath,
            XControllerAnimationContract.Attack2StatePath,
            XControllerAnimationContract.Attack3StatePath,
            XControllerAnimationContract.LauncherStatePath,
            XControllerAnimationContract.HeavyAttackStatePath,
            XControllerAnimationContract.SpecialAttackStatePath,
            XControllerAnimationContract.HitStunStatePath,
            XControllerAnimationContract.HitStunBackStatePath,
            XControllerAnimationContract.HitStunLeftStatePath,
            XControllerAnimationContract.HitStunRightStatePath,
            XControllerAnimationContract.KnockdownStatePath,
            XControllerAnimationContract.AirborneReactionStatePath,
            XControllerAnimationContract.DeadPrimaryStatePath,
            XControllerAnimationContract.DeadHeavyStatePath,
            XControllerAnimationContract.GetUpStatePath,
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
            XControllerAnimationContract.LauncherStatePath,
            XControllerAnimationContract.HeavyAttackStatePath,
            XControllerAnimationContract.SpecialAttackStatePath
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

            CharacterReactionProfile reactionProfile =
                AssetDatabase.LoadAssetAtPath<CharacterReactionProfile>(
                    CharacterReactionProfilePath);
            if (reactionProfile == null)
            {
                result.Error(
                    $"Missing character reaction profile: {CharacterReactionProfilePath}");
            }

            ValidateController(controller, reactionProfile, result);
            ValidateOverrideControllers(controller, reactionProfile, result);
            ValidatePrefab(
                PlayerPrefabPath,
                controller,
                reactionProfile,
                true,
                result);
            ValidatePrefab(
                TrainingTargetPrefabPath,
                controller,
                reactionProfile,
                false,
                result);
            return result;
        }

        private static void ValidateController(
            AnimatorController controller,
            CharacterReactionProfile reactionProfile,
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
            ValidateDirectionalLookTree(
                layers,
                XControllerAnimationContract.BodyLookLayerName,
                XControllerAnimationContract.BodyLookXParameterName,
                XControllerAnimationContract.BodyLookYParameterName,
                result);
            ValidateDirectionalLookTree(
                layers,
                XControllerAnimationContract.HeadLookLayerName,
                XControllerAnimationContract.HeadLookXParameterName,
                XControllerAnimationContract.HeadLookYParameterName,
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
            ValidateLocomotionBlendTree(states, result);
            ValidateMotions(states, result);
            ValidateFiniteStateTransitions(states, result);
            ValidateCombatActionTransitionTimings(states, result);
            ValidateReactionStates(states, result);
            if (reactionProfile != null)
            {
                ValidateReactionClipDurations(
                    states,
                    null,
                    controller.name,
                    reactionProfile,
                    result);
            }
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
                    continue;
                }

                ValidateParameterDefault(matches[0], result);
            }
        }

        private static void ValidateParameterDefault(
            AnimatorControllerParameter parameter,
            ValidationResult result)
        {
            if (parameter.name == XControllerAnimationContract.OnGroundParameterName)
            {
                if (!parameter.defaultBool)
                {
                    result.Error(
                        $"Parameter '{parameter.name}' must default to true for a " +
                        "stable grounded preview pose.");
                }

                return;
            }

            if (parameter.type != AnimatorControllerParameterType.Float)
            {
                return;
            }

            float expected = parameter.name ==
                XControllerAnimationContract.SpeedMultiplierParameterName
                ? 1.0f
                : 0.0f;
            if (!Mathf.Approximately(parameter.defaultFloat, expected))
            {
                result.Error(
                    $"Parameter '{parameter.name}' must default to {expected:F1}, " +
                    $"found {parameter.defaultFloat:F3}.");
            }
        }

        private static void ValidateDirectionalLookTree(
            IReadOnlyList<AnimatorControllerLayer> layers,
            string layerName,
            string horizontalParameter,
            string verticalParameter,
            ValidationResult result)
        {
            AnimatorControllerLayer layer = layers.FirstOrDefault(
                candidate => candidate.name == layerName);
            if (layer == null || layer.stateMachine.defaultState == null)
            {
                return;
            }

            if (layer.stateMachine.defaultState.motion is not BlendTree blendTree)
            {
                result.Error(
                    $"Layer '{layerName}' requires a two-dimensional Blend Tree.");
                return;
            }

            if (blendTree.blendType != BlendTreeType.SimpleDirectional2D)
            {
                result.Error(
                    $"Layer '{layerName}' must use SimpleDirectional2D, " +
                    $"found {blendTree.blendType}.");
            }

            if (blendTree.blendParameter != horizontalParameter ||
                blendTree.blendParameterY != verticalParameter)
            {
                result.Error(
                    $"Layer '{layerName}' must blend '{horizontalParameter}' and " +
                    $"'{verticalParameter}'.");
            }

            ChildMotion[] children = blendTree.children;
            int neutralCount = children.Count(
                child => child.motion != null && child.position.sqrMagnitude < 0.0001f);
            if (neutralCount != 1)
            {
                result.Error(
                    $"Layer '{layerName}' requires exactly one neutral motion at (0, 0); " +
                    $"found {neutralCount}.");
            }
        }

        private static void ValidateLocomotionBlendTree(
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            if (!states.TryGetValue(
                    XControllerAnimationContract.MoveStatePath,
                    out AnimatorState moveState))
            {
                return;
            }

            if (moveState.motion is not BlendTree gaitTree)
            {
                result.Error("Locomotion Move state requires a gait Blend Tree.");
                return;
            }

            if (gaitTree.blendType != BlendTreeType.Simple1D ||
                gaitTree.blendParameter !=
                XControllerAnimationContract.MovementTypeParameterName)
            {
                result.Error(
                    "Locomotion gait tree must use Simple1D with " +
                    $"'{XControllerAnimationContract.MovementTypeParameterName}'.");
            }

            ChildMotion[] gaits = gaitTree.children;
            float[] expectedThresholds = { 0.0f, 1.0f, 2.0f, 3.0f };
            if (gaits.Length != expectedThresholds.Length)
            {
                result.Error(
                    "Locomotion gait tree requires Idle, Walk, Run and Sprint " +
                    $"children; found {gaits.Length}.");
                return;
            }

            for (int i = 0; i < gaits.Length; i++)
            {
                if (!Mathf.Approximately(gaits[i].threshold, expectedThresholds[i]))
                {
                    result.Error(
                        $"Locomotion gait child {i} requires threshold " +
                        $"{expectedThresholds[i]:F1}, found {gaits[i].threshold:F3}.");
                }

                if (gaits[i].motion == null)
                {
                    result.Error($"Locomotion gait child {i} requires a motion.");
                }
            }

            for (int i = 1; i < gaits.Length; i++)
            {
                ValidateDirectionalGaitTree(gaits[i].motion, i, result);
            }
        }

        private static void ValidateDirectionalGaitTree(
            Motion motion,
            int gaitIndex,
            ValidationResult result)
        {
            if (motion is not BlendTree directionTree ||
                directionTree.blendType != BlendTreeType.Simple1D ||
                directionTree.blendParameter !=
                XControllerAnimationContract.MovementDirectionParameterName)
            {
                result.Error(
                    $"Locomotion gait child {gaitIndex} requires a Simple1D " +
                    $"'{XControllerAnimationContract.MovementDirectionParameterName}' tree.");
                return;
            }

            ChildMotion[] directions = directionTree.children;
            if (directions.Length != 2)
            {
                result.Error(
                    $"Locomotion gait child {gaitIndex} requires backward and " +
                    $"forward motions; found {directions.Length}.");
                return;
            }

            if (!Mathf.Approximately(directions[0].threshold, -0.5f) ||
                !Mathf.Approximately(directions[1].threshold, 0.5f))
            {
                result.Error(
                    $"Locomotion gait child {gaitIndex} direction thresholds must " +
                    "be -0.5 and 0.5.");
            }

            if (directions[0].motion == null || directions[1].motion == null)
            {
                result.Error(
                    $"Locomotion gait child {gaitIndex} requires both direction motions.");
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
                if (!states.TryGetValue(path, out AnimatorState state))
                {
                    continue;
                }

                if (state.transitions.Length == 0)
                {
                    result.Error(
                        $"Finite state '{path}' requires an authored exit transition.");
                    continue;
                }

                int timedExitCount = state.transitions.Count(
                    transition => transition.hasExitTime);
                if (timedExitCount == 0)
                {
                    result.Error(
                        $"Finite state '{path}' requires at least one timed exit.");
                }
            }
        }

        private static void ValidateCombatActionTransitionTimings(
            IReadOnlyDictionary<string, AnimatorState> states,
            ValidationResult result)
        {
            PlayerCombatActionProfile profile =
                AssetDatabase.LoadAssetAtPath<PlayerCombatActionProfile>(
                    CombatActionProfilePath);
            if (profile == null)
            {
                result.Error($"Missing combat action profile: {CombatActionProfilePath}");
                return;
            }

            IReadOnlyList<PlayerCombatAnimationTimingRequirement> requirements;
            try
            {
                requirements = profile.CreateAnimationTimingRequirements();
            }
            catch (Exception exception)
            {
                result.Error(
                    $"Combat action profile cannot build its graph: {exception.Message}");
                return;
            }

            var expectedCompletions = new Dictionary<string, float>(
                StringComparer.Ordinal);
            for (int i = 0; i < requirements.Count; i++)
            {
                PlayerCombatAnimationTimingRequirement requirement = requirements[i];
                if (!TryResolveCombatStatePath(in requirement, out string statePath))
                {
                    result.Error(
                        $"Combat action '{requirement.ActionId.Value}' has no " +
                        "presentation state in the XController contract.");
                    continue;
                }

                float completion = requirement.CompletionTime;
                if (expectedCompletions.TryGetValue(statePath, out float existing) &&
                    !Mathf.Approximately(existing, completion))
                {
                    result.Error(
                        $"Combat actions mapped to '{statePath}' disagree on completion " +
                        $"({existing:F3} versus {completion:F3}).");
                    continue;
                }

                expectedCompletions[statePath] = completion;
            }

            foreach (KeyValuePair<string, float> expected in expectedCompletions)
            {
                if (!states.TryGetValue(expected.Key, out AnimatorState state))
                {
                    continue;
                }

                AnimatorStateTransition[] exits = state.transitions
                    .Where(transition => transition.hasExitTime)
                    .ToArray();
                if (exits.Length == 0)
                {
                    result.Error(
                        $"Combat state '{expected.Key}' requires a timed exit for " +
                        "gameplay timing validation.");
                    continue;
                }

                for (int i = 0; i < exits.Length; i++)
                {
                    AnimatorStateTransition transition = exits[i];
                    if (transition.hasFixedDuration)
                    {
                        result.Error(
                            $"Combat state '{expected.Key}' timed exit {i} must use " +
                            "normalized transition duration so Animator Override clips " +
                            "keep the same completion boundary.");
                        continue;
                    }

                    float animatorCompletion = transition.exitTime + transition.duration;
                    if (Mathf.Abs(animatorCompletion - expected.Value) >
                        TransitionTimingTolerance)
                    {
                        result.Error(
                            $"Combat state '{expected.Key}' timed exit {i} completes at " +
                            $"normalized {animatorCompletion:F3}; gameplay expects " +
                            $"{expected.Value:F3}.");
                    }
                }
            }
        }

        private static bool TryResolveCombatStatePath(
            in PlayerCombatAnimationTimingRequirement requirement,
            out string statePath)
        {
            return XControllerAnimationContract.TryGetCombatStatePath(
                requirement.PresentationKind,
                requirement.ComboIndex,
                requirement.AnimationId,
                out statePath);
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
                    if ((XControllerAnimationContract.HitStunStatePaths.Contains(path) ||
                         path == XControllerAnimationContract.KnockdownStatePath ||
                         path == XControllerAnimationContract.DeadPrimaryStatePath ||
                         path == XControllerAnimationContract.DeadHeavyStatePath ||
                         path == XControllerAnimationContract.GetUpStatePath) &&
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

        private static void ValidateReactionClipDurations(
            IReadOnlyDictionary<string, AnimatorState> states,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrides,
            string controllerName,
            CharacterReactionProfile profile,
            ValidationResult result)
        {
            var serializedProfile = new SerializedObject(profile);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.HitStunStatePath,
                serializedProfile.FindProperty("_hitStunDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.HitStunBackStatePath,
                serializedProfile.FindProperty("_hitStunDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.HitStunLeftStatePath,
                serializedProfile.FindProperty("_hitStunDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.HitStunRightStatePath,
                serializedProfile.FindProperty("_hitStunDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.KnockdownStatePath,
                serializedProfile.FindProperty("_knockdownDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.AirborneReactionStatePath,
                serializedProfile.FindProperty("_airborneDuration").floatValue,
                result);
            ValidateReactionClipDuration(
                states,
                overrides,
                controllerName,
                XControllerAnimationContract.GetUpStatePath,
                serializedProfile.FindProperty("_getUpDuration").floatValue,
                result);
        }

        private static void ValidateReactionClipDuration(
            IReadOnlyDictionary<string, AnimatorState> states,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrides,
            string controllerName,
            string statePath,
            float gameplayDuration,
            ValidationResult result)
        {
            if (!states.TryGetValue(statePath, out AnimatorState state))
            {
                return;
            }

            var sourceClips = new HashSet<AnimationClip>();
            CollectClips(state.motion, sourceClips);
            if (sourceClips.Count != 1 || state.speed <= 0.0f)
            {
                result.Error(
                    $"Reaction state '{statePath}' requires one clip and positive " +
                    "speed for duration validation.");
                return;
            }

            AnimationClip source = sourceClips.First();
            AnimationClip active = overrides != null &&
                                   overrides.TryGetValue(
                                       source,
                                       out AnimationClip replacement)
                ? replacement
                : source;
            float visualDuration = active.length / state.speed;
            if (visualDuration + ReactionClipDurationToleranceSeconds < gameplayDuration)
            {
                result.Error(
                    $"Controller '{controllerName}' reaction '{statePath}' plays " +
                    $"'{active.name}' for {visualDuration:F3}s; gameplay keeps the " +
                    $"reaction active for {gameplayDuration:F3}s.");
            }
        }

        private static void ValidateOverrideControllers(
            AnimatorController baseController,
            CharacterReactionProfile reactionProfile,
            ValidationResult result)
        {
            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            AnimatorControllerLayer[] layers = baseController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                CollectStates(layers[i].stateMachine, layers[i].name, states, result);
            }

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
                var resolvedClips = new Dictionary<AnimationClip, AnimationClip>();
                for (int j = 0; j < overrides.Count; j++)
                {
                    AnimationClip source = overrides[j].Key;
                    AnimationClip replacement = overrides[j].Value;
                    if (source != null)
                    {
                        resolvedClips[source] = replacement != null
                            ? replacement
                            : source;
                    }

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

                if (reactionProfile != null)
                {
                    ValidateReactionClipDurations(
                        states,
                        resolvedClips,
                        animationOverride.name,
                        reactionProfile,
                        result);
                }
            }
        }

        private static void ValidatePrefab(
            string prefabPath,
            AnimatorController baseController,
            CharacterReactionProfile expectedReactionProfile,
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

            ValidateReactionProfileBinding(
                prefab,
                prefabPath,
                expectedReactionProfile,
                validateLoadout,
                result);

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
            PlayerCombatActionProfile combatProfile = ValidatePlayerCombatProfile(
                serializedPlayer,
                prefabPath,
                result);
            DodgeSettings dodgeSettings = ResolvePlayerDodgeSettings(
                serializedPlayer,
                prefabPath,
                result);
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
                    continue;
                }

                if (dodgeSettings != null)
                {
                    ValidateDodgeClipDurations(
                        animationOverride,
                        baseController,
                        dodgeSettings.Duration,
                        result);
                }

                if (combatProfile != null)
                {
                    ValidateCombatClipDurations(
                        animationOverride,
                        baseController,
                        combatProfile,
                        result);
                }
            }
        }

        private static void ValidateReactionProfileBinding(
            GameObject prefab,
            string prefabPath,
            CharacterReactionProfile expectedProfile,
            bool playerPrefab,
            ValidationResult result)
        {
            UnityEngine.Object owner;
            string propertyName;
            if (playerPrefab)
            {
                owner = prefab.GetComponent<ThirdPersonController>();
                propertyName = "_reactionProfile";
            }
            else
            {
                owner = prefab.GetComponent<CharacterReactionHost>();
                propertyName = "_profile";
            }

            if (owner == null)
            {
                result.Error(
                    $"Prefab '{prefabPath}' has no reaction profile owner.");
                return;
            }

            SerializedProperty property =
                new SerializedObject(owner).FindProperty(propertyName);
            if (property?.objectReferenceValue == null)
            {
                result.Error(
                    $"Prefab '{prefabPath}' requires a character reaction profile.");
                return;
            }

            if (expectedProfile != null && property.objectReferenceValue != expectedProfile)
            {
                result.Error(
                    $"Prefab '{prefabPath}' uses reaction profile " +
                    $"'{property.objectReferenceValue.name}', but animation timing " +
                    $"validation uses '{CharacterReactionProfilePath}'.");
            }
        }

        private static void ValidateCombatClipDurations(
            AnimatorOverrideController animationOverride,
            AnimatorController baseController,
            PlayerCombatActionProfile profile,
            ValidationResult result)
        {
            IReadOnlyList<PlayerCombatAnimationTimingRequirement> requirements;
            try
            {
                requirements = profile.CreateAnimationTimingRequirements();
            }
            catch (Exception exception)
            {
                result.Error(
                    $"Combat action profile cannot expose animation timings: " +
                    exception.Message);
                return;
            }

            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            AnimatorControllerLayer[] layers = baseController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                CollectStates(layers[i].stateMachine, layers[i].name, states, result);
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(
                animationOverride.overridesCount);
            animationOverride.GetOverrides(overrides);
            var resolvedClips = new Dictionary<AnimationClip, AnimationClip>();
            for (int i = 0; i < overrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> pair = overrides[i];
                if (pair.Key != null)
                {
                    resolvedClips[pair.Key] = pair.Value != null
                        ? pair.Value
                        : pair.Key;
                }
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                PlayerCombatAnimationTimingRequirement requirement = requirements[i];
                if (!XControllerAnimationContract.TryGetCombatStatePath(
                        requirement.PresentationKind,
                        requirement.ComboIndex,
                        requirement.AnimationId,
                        out string statePath) ||
                    !states.TryGetValue(statePath, out AnimatorState state))
                {
                    continue;
                }

                var sourceClips = new HashSet<AnimationClip>();
                CollectClips(state.motion, sourceClips);
                if (sourceClips.Count != 1 || state.speed <= 0.0f)
                {
                    result.Error(
                        $"Combat state '{statePath}' requires one clip and positive " +
                        "speed for duration validation.");
                    continue;
                }

                AnimationClip source = sourceClips.First();
                AnimationClip active = resolvedClips.TryGetValue(
                    source,
                    out AnimationClip replacement)
                    ? replacement
                    : source;
                float visualDuration = active.length / state.speed;
                if (Mathf.Abs(visualDuration - requirement.DurationSeconds) >
                    CombatClipDurationToleranceSeconds)
                {
                    result.Error(
                        $"Override '{animationOverride.name}' action " +
                        $"'{requirement.ActionId.Value}' plays '{active.name}' for " +
                        $"{visualDuration:F3}s; gameplay expects " +
                        $"{requirement.DurationSeconds:F3}s.");
                }
            }
        }

        private static DodgeSettings ResolvePlayerDodgeSettings(
            SerializedObject serializedPlayer,
            string prefabPath,
            ValidationResult result)
        {
            SerializedProperty property =
                serializedPlayer.FindProperty("_characterFeatures");
            var features = property?.objectReferenceValue as CharacterFeatureSet;
            if (features == null)
            {
                result.Error(
                    $"Player prefab '{prefabPath}' requires a character feature set.");
                return null;
            }

            IReadOnlyList<CharacterFeatureEntry> entries = features.Features;
            if (entries == null)
            {
                result.Error(
                    $"Character feature set '{features.name}' has no feature list.");
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterFeatureEntry entry = entries[i];
                if (entry is { IsEnabled: true, Settings: DodgeSettings dodge })
                {
                    return dodge;
                }
            }

            return null;
        }

        private static void ValidateDodgeClipDurations(
            AnimatorOverrideController animationOverride,
            AnimatorController baseController,
            float gameplayDuration,
            ValidationResult result)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(
                animationOverride.overridesCount);
            animationOverride.GetOverrides(overrides);

            ValidateDodgeClipDuration(
                animationOverride,
                overrides,
                baseController,
                "RollFront",
                XControllerAnimationContract.RollForwardStatePath,
                gameplayDuration,
                result);
            ValidateDodgeClipDuration(
                animationOverride,
                overrides,
                baseController,
                "RollBack",
                XControllerAnimationContract.RollBackwardStatePath,
                gameplayDuration,
                result);
        }

        private static void ValidateDodgeClipDuration(
            AnimatorOverrideController animationOverride,
            IReadOnlyList<KeyValuePair<AnimationClip, AnimationClip>> overrides,
            AnimatorController baseController,
            string sourceClipName,
            string statePath,
            float gameplayDuration,
            ValidationResult result)
        {
            AnimationClip clip = null;
            for (int i = 0; i < overrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> pair = overrides[i];
                if (pair.Key != null && pair.Key.name == sourceClipName)
                {
                    clip = pair.Value != null ? pair.Value : pair.Key;
                    break;
                }
            }

            if (clip == null)
            {
                result.Error(
                    $"Override '{animationOverride.name}' has no dodge clip for " +
                    $"slot '{sourceClipName}'.");
                return;
            }

            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            AnimatorControllerLayer[] layers = baseController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                CollectStates(layers[i].stateMachine, layers[i].name, states, result);
            }

            if (!states.TryGetValue(statePath, out AnimatorState state) ||
                state.speed <= 0.0f)
            {
                result.Error(
                    $"Dodge state '{statePath}' requires a positive playback speed.");
                return;
            }

            float visualDuration = clip.length / state.speed;
            if (Mathf.Abs(visualDuration - gameplayDuration) >
                DodgeDurationToleranceSeconds)
            {
                result.Error(
                    $"Override '{animationOverride.name}' dodge slot " +
                    $"'{sourceClipName}' lasts {visualDuration:F3}s, but gameplay " +
                    $"DodgeSettings lasts {gameplayDuration:F3}s.");
            }
        }

        private static PlayerCombatActionProfile ValidatePlayerCombatProfile(
            SerializedObject serializedPlayer,
            string prefabPath,
            ValidationResult result)
        {
            SerializedProperty property =
                serializedPlayer.FindProperty("_combatActionProfile");
            if (property == null || property.objectReferenceValue == null)
            {
                result.Error(
                    $"Player prefab '{prefabPath}' requires a combat action profile.");
                return null;
            }

            PlayerCombatActionProfile expected =
                AssetDatabase.LoadAssetAtPath<PlayerCombatActionProfile>(
                    CombatActionProfilePath);
            if (expected == null)
            {
                result.Error($"Missing combat action profile: {CombatActionProfilePath}");
                return null;
            }

            if (property.objectReferenceValue != expected)
            {
                result.Error(
                    $"Player prefab '{prefabPath}' uses combat profile " +
                    $"'{property.objectReferenceValue.name}', but animation timing " +
                    $"validation uses '{CombatActionProfilePath}'.");
            }

            return expected;
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
