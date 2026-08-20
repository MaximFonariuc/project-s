using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModularAnimation.Unity;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class KurtzPelPlayerAnimationSetup
    {
        private const string RootFolder =
            "Assets/_Game/Animations/Reference/KurtzPel/MaleComplete";
        private const string MovementPack = RootFolder + "/Movement/KurtzPel_Male_ENFJ.fbx";
        private const string CommonPack = RootFolder + "/Movement/KurtzPel_Common_Ninja.fbx";
        private const string CombatPack = RootFolder + "/Combat/KurtzPel_BlazingFist.fbx";
        private const string ManifestPath = RootFolder + "/KurtzPel_AnimationManifest.csv";
        private const string ControllerPath =
            "Assets/_Game/Animations/Character/1. Controllers/XController.controller";
        private const string PlayerPrefabPath =
            "Assets/_Game/Prefabs/Character/Player.prefab";
        private const string CombatProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset";
        private const string FeatureSetPath =
            "Assets/_Game/Settings/Character/AbillityData/AbilitiesData.asset";
        private const string DodgeSettingsPath =
            "Assets/_Game/Settings/Character/AbillityData/Abilities/DodgeAbilityData.asset";

        private const float ActionTransitionSeconds = 0.25f;
        private const float DodgeDurationSeconds = 0.48f;
        private const float AttackDurationSeconds = 1.3f;

        [MenuItem("Tools/Animation/KurtzPel/Build Player Animator and Prefab")]
        public static void BuildPlayerAnimatorAndPrefab()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Dictionary<string, float> sourceFrameRates = ReadSourceFrameRates();
            AnimatorController controller = BuildController(sourceFrameRates);
            ConfigureCombatProfile();
            ConfigureFeatureAssets();
            BodyColliderGenerationResult colliderResult = ConfigurePlayerPrefab(controller);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[KurtzPel Player Setup] Restored '{ControllerPath}', configured the Player " +
                $"for grounded locomotion, jump/fall/landing, dodge and one unarmed attack. " +
                $"{colliderResult.Message} Swimming and ladder animation remain intentionally unconfigured.");
        }

        private static AnimatorController BuildController(
            IReadOnlyDictionary<string, float> sourceFrameRates)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"The recovered historical Animator Controller is missing: {ControllerPath}");
            }

            AddParameters(controller);

            AnimationClip idle = LoadClip(MovementPack, "HM_ENFJ_Wait");
            AnimationClip walk = LoadClip(MovementPack, "HM_ENFJ_Walk");
            AnimationClip run = LoadClip(MovementPack, "HM_ENFJ_Run");
            AnimationClip sprint = LoadClip(MovementPack, "HM_ENFJ_Run_Fast");
            AnimationClip jump = LoadClip(MovementPack, "HM_ENFJ_Air_JumpUp");
            AnimationClip fall = LoadClip(MovementPack, "HM_ENFJ_Air_JumpDown");
            AnimationClip land = LoadClip(MovementPack, "HM_ENFJ_Air_JumpLanding");
            AnimationClip dodgeForward = LoadClip(MovementPack, "HM_ENFJ_Dash");
            AnimationClip dodgeBackward = LoadClip(CommonPack, "CC_C_Rage_Avoid");
            AnimationClip lightAttack = LoadClip(CombatPack, "HMGT_BasicCombo_01");

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState move = FindState(machine, "Move");
            BlendTree locomotionTree = move.motion as BlendTree;
            if (locomotionTree == null || locomotionTree.children.Length != 4)
            {
                throw new InvalidOperationException(
                    "The historical Move state no longer contains its four-way locomotion Blend Tree.");
            }

            ConfigureLocomotionTree(
                locomotionTree,
                idle,
                walk,
                run,
                sprint,
                sourceFrameRates);

            ConfigureStateMotion(machine, "JumpWalking", jump, sourceFrameRates);
            ConfigureStateMotion(machine, "JumpRunning", jump, sourceFrameRates);
            ConfigureStateMotion(machine, "JumpSprinting", jump, sourceFrameRates);
            ConfigureStateMotion(machine, "Fall", fall, sourceFrameRates);
            ConfigureStateMotion(machine, "LandDefault", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandRun", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandWalk", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandHard", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandSoft", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandSprint", land, sourceFrameRates);
            ConfigureStateMotion(machine, "LandMedium", land, sourceFrameRates);

            AnimatorState dodgeF = FindState(machine, "RollFront");
            AnimatorState dodgeB = FindState(machine, "RollBack");
            AnimatorState attack = FindState(machine, "BaseAttack1");
            ConfigureStateMotion(
                dodgeF,
                dodgeForward,
                SpeedForDuration(dodgeForward, DodgeDurationSeconds / 0.9f));
            ConfigureStateMotion(
                dodgeB,
                dodgeBackward,
                SpeedForDuration(dodgeBackward, DodgeDurationSeconds / 0.9f));
            ConfigureStateMotion(
                attack,
                lightAttack,
                SourceSpeed(lightAttack, sourceFrameRates));

            RemoveSetupTransitions(machine, dodgeF, dodgeB, attack);
            AddActionTransition(machine, dodgeF, 1, 0);
            AddActionTransition(machine, dodgeB, 1, 1);
            AddActionTransition(machine, attack, 2, null);
            AddCancelTransition(dodgeF, move);
            AddCancelTransition(dodgeB, move);
            AddCancelTransition(attack, move);

            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 1; i < layers.Length; i++)
            {
                layers[i].defaultWeight = 0f;
            }
            controller.layers = layers;

            EditorUtility.SetDirty(locomotionTree);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddParameters(AnimatorController controller)
        {
            EnsureParameter(controller, MecanimCharacterParameters.MovementType,
                AnimatorControllerParameterType.Float);
            EnsureParameter(controller, MecanimCharacterParameters.MovementDirection,
                AnimatorControllerParameterType.Float);
            EnsureParameter(controller, MecanimCharacterParameters.FallingDuration,
                AnimatorControllerParameterType.Float);
            EnsureParameter(controller, MecanimCharacterParameters.OnGround,
                AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, MecanimCharacterParameters.Action,
                AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, MecanimCharacterParameters.ActionId,
                AnimatorControllerParameterType.Int);
            EnsureParameter(controller, MecanimCharacterParameters.ActionVariant,
                AnimatorControllerParameterType.Int);
            EnsureParameter(controller, MecanimCharacterParameters.CancelAction,
                AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter existing = controller.parameters
                .FirstOrDefault(parameter => parameter.name == name);
            if (existing != null)
            {
                if (existing.type != type)
                {
                    throw new InvalidOperationException(
                        $"Animator parameter '{name}' has type {existing.type}, expected {type}.");
                }

                return;
            }

            controller.AddParameter(name, type);
        }

        private static void ConfigureLocomotionTree(
            BlendTree tree,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip sprint,
            IReadOnlyDictionary<string, float> sourceFrameRates)
        {
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = MecanimCharacterParameters.MovementType;
            tree.useAutomaticThresholds = false;
            tree.minThreshold = 0f;
            tree.maxThreshold = 3f;

            Motion[] motions = { idle, walk, run, sprint };
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                children[i].motion = motions[i];
                children[i].threshold = i;
                children[i].timeScale = SourceSpeed(
                    (AnimationClip)motions[i],
                    sourceFrameRates);
            }

            tree.children = children;
        }

        private static void ConfigureStateMotion(
            AnimatorStateMachine machine,
            string stateName,
            AnimationClip motion,
            IReadOnlyDictionary<string, float> sourceFrameRates)
        {
            ConfigureStateMotion(
                FindState(machine, stateName),
                motion,
                SourceSpeed(motion, sourceFrameRates));
        }

        private static void ConfigureStateMotion(
            AnimatorState state,
            Motion motion,
            float speed)
        {
            state.motion = motion;
            state.speed = Mathf.Max(0.01f, speed);
            EditorUtility.SetDirty(state);
        }

        private static AnimatorState FindState(
            AnimatorStateMachine machine,
            string stateName)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state;
                }
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                try
                {
                    return FindState(child.stateMachine, stateName);
                }
                catch (InvalidOperationException)
                {
                    // Continue searching the remaining historical sub-state machines.
                }
            }

            throw new InvalidOperationException(
                $"Historical Animator state '{stateName}' was not found in '{machine.name}'.");
        }

        private static void RemoveSetupTransitions(
            AnimatorStateMachine machine,
            params AnimatorState[] actionStates)
        {
            AnimatorStateTransition[] anyStateTransitions = machine.anyStateTransitions;
            for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
            {
                if (anyStateTransitions[i].conditions.Any(condition =>
                        condition.parameter == MecanimCharacterParameters.Action))
                {
                    machine.RemoveAnyStateTransition(anyStateTransitions[i]);
                }
            }

            foreach (AnimatorState state in actionStates)
            {
                AnimatorStateTransition[] transitions = state.transitions;
                for (int i = transitions.Length - 1; i >= 0; i--)
                {
                    if (transitions[i].conditions.Any(condition =>
                            condition.parameter == MecanimCharacterParameters.CancelAction))
                    {
                        state.RemoveTransition(transitions[i]);
                    }
                }
            }
        }

        private static void AddActionTransition(
            AnimatorStateMachine machine,
            AnimatorState destination,
            int actionId,
            int? variant)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
            ConfigureImmediateTransition(transition, ActionTransitionSeconds);
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.Action);
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                actionId,
                MecanimCharacterParameters.ActionId);
            if (variant.HasValue)
            {
                transition.AddCondition(
                    AnimatorConditionMode.Equals,
                    variant.Value,
                    MecanimCharacterParameters.ActionVariant);
            }
        }

        private static void AddCancelTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            ConfigureImmediateTransition(transition, ActionTransitionSeconds);
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.CancelAction);
        }

        private static void ConfigureImmediateTransition(
            AnimatorStateTransition transition,
            float durationSeconds)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = durationSeconds;
            transition.interruptionSource = TransitionInterruptionSource.Source;
            transition.orderedInterruption = true;
        }

        private static float SourceSpeed(
            AnimationClip clip,
            IReadOnlyDictionary<string, float> sourceFrameRates)
        {
            if (!sourceFrameRates.TryGetValue(clip.name, out float sourceFrameRate))
            {
                throw new InvalidOperationException(
                    $"Source frame rate is missing for KurtzPel clip '{clip.name}'.");
            }

            return sourceFrameRate / Mathf.Max(1f, clip.frameRate);
        }

        private static float SpeedForDuration(AnimationClip clip, float durationSeconds)
        {
            return clip.length / Mathf.Max(0.01f, durationSeconds);
        }

        private static AnimationClip LoadClip(string assetPath, string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == clipName);
            return clip != null
                ? clip
                : throw new InvalidOperationException(
                    $"KurtzPel clip '{clipName}' was not found in '{assetPath}'.");
        }

        private static Dictionary<string, float> ReadSourceFrameRates()
        {
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException($"KurtzPel manifest is missing: {ManifestPath}");
            }

            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            string[] lines = manifest.text.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] columns = lines[i].Split(',');
                if (columns.Length < 4 ||
                    !float.TryParse(
                        columns[3],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float frameRate) ||
                    frameRate <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Invalid KurtzPel manifest row {i + 1}: '{lines[i]}'.");
                }

                result.Add(columns[1], frameRate);
            }

            if (result.Count != 437)
            {
                throw new InvalidOperationException(
                    $"KurtzPel manifest contains {result.Count} clips instead of 437.");
            }

            return result;
        }

        private static BodyColliderGenerationResult ConfigurePlayerPrefab(
            RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentsInChildren<Animator>(true)
                    .FirstOrDefault(candidate => candidate.name == "CharacterVisual");
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab has no CharacterVisual Animator to configure.");
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                ThirdPersonController player = root.GetComponent<ThirdPersonController>();
                if (player == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab has no ThirdPersonController.");
                }

                var playerObject = new SerializedObject(player);
                playerObject.FindProperty("_animator").objectReferenceValue = animator;
                playerObject.ApplyModifiedPropertiesWithoutUndo();

                BodyColliderAuthoring authoring = root.GetComponent<BodyColliderAuthoring>();
                if (authoring != null)
                {
                    var authoringObject = new SerializedObject(authoring);
                    authoringObject.FindProperty("_animator").objectReferenceValue = animator;
                    authoringObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                int colliderCount = root.GetComponentsInChildren<GeneratedBodyCollider>(true).Length;
                return new BodyColliderGenerationResult(
                    true,
                    BodyRigType.Auto,
                    colliderCount,
                    0,
                    $"Preserved {colliderCount} existing generated body colliders.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCombatProfile()
        {
            PlayerCombatActionProfile profile =
                AssetDatabase.LoadAssetAtPath<PlayerCombatActionProfile>(CombatProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Player combat profile is missing: {CombatProfilePath}");
            }

            var serialized = new SerializedObject(profile);
            ConfigureTimeline(
                serialized.FindProperty("_weaponTransition"),
                0.8f,
                false);

            SerializedProperty inputOverrides = serialized
                .FindProperty("_inputBuffer")
                .FindPropertyRelative("_actionOverrides");
            inputOverrides.ClearArray();

            SerializedProperty actions = serialized.FindProperty("_actions");
            actions.arraySize = 1;
            SerializedProperty action = actions.GetArrayElementAtIndex(0);
            action.FindPropertyRelative("_id").stringValue = "combo.ground.0";
            action.FindPropertyRelative("_environment").enumValueIndex =
                (int)PlayerCombatEnvironment.Grounded;
            action.FindPropertyRelative("_presentation").enumValueIndex =
                (int)PlayerCombatPresentationKind.ComboStep;
            action.FindPropertyRelative("_comboIndex").intValue = 0;
            ConfigureTimeline(
                action.FindPropertyRelative("_action"),
                AttackDurationSeconds,
                true);

            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_commandId").stringValue = "attack.light";
            entry.FindPropertyRelative("_targetActionId").stringValue = "combo.ground.0";
            serialized.FindProperty("_transitions").ClearArray();

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void ConfigureTimeline(
            SerializedProperty timeline,
            float durationSeconds,
            bool attack)
        {
            timeline.FindPropertyRelative("_durationSeconds").floatValue = durationSeconds;
            if (!attack)
            {
                return;
            }

            timeline.FindPropertyRelative("_useHitboxWindow").boolValue = true;
            timeline.FindPropertyRelative("_hitboxStart").floatValue = 0.16f;
            timeline.FindPropertyRelative("_hitboxEnd").floatValue = 0.45f;
            timeline.FindPropertyRelative("_useChainWindow").boolValue = false;
            timeline.FindPropertyRelative("_useCancelWindow").boolValue = true;
            timeline.FindPropertyRelative("_cancelStart").floatValue = 0.55f;
            timeline.FindPropertyRelative("_cancelEnd").floatValue = 0.82f;
            timeline.FindPropertyRelative("_useMovementConstraintWindow").boolValue = true;
            timeline.FindPropertyRelative("_movementConstraintStart").floatValue = 0f;
            timeline.FindPropertyRelative("_movementConstraintEnd").floatValue = 0.7f;
            timeline.FindPropertyRelative("_recoveryStart").floatValue = 0.68f;
            timeline.FindPropertyRelative("_completionTime").floatValue = 0.95f;
            timeline.FindPropertyRelative("_useCombatMotion").boolValue = true;
            timeline.FindPropertyRelative("_combatMotionStart").floatValue = 0f;
            timeline.FindPropertyRelative("_combatMotionEnd").floatValue = 0.32f;
            timeline.FindPropertyRelative("_overridePlanarVelocity").boolValue = true;
            timeline.FindPropertyRelative("_combatMotionDistance").floatValue = 0.6f;
            timeline.FindPropertyRelative("_faceCombatTarget").boolValue = true;
            timeline.FindPropertyRelative("_targetStoppingDistance").floatValue = 0.8f;
        }

        private static void ConfigureFeatureAssets()
        {
            DodgeSettings dodge = AssetDatabase.LoadAssetAtPath<DodgeSettings>(DodgeSettingsPath);
            if (dodge != null)
            {
                var serializedDodge = new SerializedObject(dodge);
                serializedDodge.FindProperty("_dodgeImpulse").floatValue = 6f;
                serializedDodge.FindProperty("_dodgeDuration").floatValue = DodgeDurationSeconds;
                serializedDodge.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dodge);
            }

            CharacterFeatureSet featureSet =
                AssetDatabase.LoadAssetAtPath<CharacterFeatureSet>(FeatureSetPath);
            if (featureSet == null)
            {
                return;
            }

            var serializedFeatures = new SerializedObject(featureSet);
            SerializedProperty features = serializedFeatures.FindProperty("_features");
            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty settings = features
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("_settings");
                if (settings.objectReferenceValue == null)
                {
                    features.DeleteArrayElementAtIndex(i);
                }
            }

            serializedFeatures.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(featureSet);
        }

    }
}
