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
        private const string PreviewPrefabPath =
            RootFolder + "/Preview/KurtzPel_MaleReference.prefab";
        private const string ManifestPath = RootFolder + "/KurtzPel_AnimationManifest.csv";
        private const string ControllerFolder =
            "Assets/_Game/Animations/Character/Controllers";
        private const string ControllerPath = ControllerFolder + "/Player_KurtzPel.controller";
        private const string PlayerPrefabPath =
            "Assets/_Game/Prefabs/Character/Player.prefab";
        private const string CombatProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset";
        private const string FeatureSetPath =
            "Assets/_Game/Settings/Character/AbillityData/AbilitiesData.asset";
        private const string DodgeSettingsPath =
            "Assets/_Game/Settings/Character/AbillityData/Abilities/DodgeAbilityData.asset";

        private const float TransitionSeconds = 0.1f;
        private const float ActionTransitionSeconds = 0.07f;
        private const float DodgeDurationSeconds = 0.48f;
        private const float AttackDurationSeconds = 1.3f;
        private const float WalkBlendSpeed = 3.0f;
        private const float RunBlendSpeed = 5.0f;
        private const float SprintBlendSpeed = 10.0f;
        private const float KurtzPelVisualYaw = -90.0f;

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
                $"[KurtzPel Player Setup] Built '{ControllerPath}', configured the Player " +
                $"for grounded locomotion, jump/fall/landing, dodge and one unarmed attack. " +
                $"{colliderResult.Message} Swimming and ladder animation remain intentionally unconfigured.");
        }

        private static AnimatorController BuildController(
            IReadOnlyDictionary<string, float> sourceFrameRates)
        {
            EnsureFolder(ControllerFolder);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null &&
                !AssetDatabase.DeleteAsset(ControllerPath))
            {
                throw new InvalidOperationException(
                    $"Could not replace Animator Controller: {ControllerPath}");
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddParameters(controller);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.name = "Player Grounded Action Graph";
            machine.anyStatePosition = new Vector3(300f, -100f, 0f);
            machine.entryPosition = new Vector3(40f, 20f, 0f);

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

            BlendTree locomotionTree = new BlendTree
            {
                name = "Grounded Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = MecanimCharacterParameters.MotionSpeed,
                useAutomaticThresholds = false,
                minThreshold = 0f,
                maxThreshold = SprintBlendSpeed,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(locomotionTree, controller);
            AddBlendChild(locomotionTree, idle, 0f, SourceSpeed(idle, sourceFrameRates));
            AddBlendChild(
                locomotionTree,
                walk,
                WalkBlendSpeed,
                SourceSpeed(walk, sourceFrameRates));
            AddBlendChild(
                locomotionTree,
                run,
                RunBlendSpeed,
                SourceSpeed(run, sourceFrameRates));
            AddBlendChild(
                locomotionTree,
                sprint,
                SprintBlendSpeed,
                SourceSpeed(sprint, sourceFrameRates));

            AnimatorState locomotion = AddState(
                machine,
                "Locomotion",
                locomotionTree,
                1f,
                new Vector3(320f, 20f, 0f));
            AnimatorState jumpUp = AddState(
                machine,
                "Jump Up",
                jump,
                SourceSpeed(jump, sourceFrameRates),
                new Vector3(600f, -20f, 0f));
            AnimatorState falling = AddState(
                machine,
                "Fall",
                fall,
                SourceSpeed(fall, sourceFrameRates),
                new Vector3(820f, -20f, 0f));
            AnimatorState landing = AddState(
                machine,
                "Landing",
                land,
                SourceSpeed(land, sourceFrameRates),
                new Vector3(1050f, 20f, 0f));
            AnimatorState dodgeF = AddState(
                machine,
                "Dodge Forward",
                dodgeForward,
                SpeedForDuration(dodgeForward, DodgeDurationSeconds / 0.9f),
                new Vector3(560f, 180f, 0f));
            AnimatorState dodgeB = AddState(
                machine,
                "Dodge Backward",
                dodgeBackward,
                SpeedForDuration(dodgeBackward, DodgeDurationSeconds / 0.9f),
                new Vector3(800f, 180f, 0f));
            AnimatorState attack = AddState(
                machine,
                "Unarmed Light Attack",
                lightAttack,
                SourceSpeed(lightAttack, sourceFrameRates),
                new Vector3(680f, 330f, 0f));

            machine.defaultState = locomotion;

            AddConditionTransition(
                locomotion,
                jumpUp,
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.Grounded,
                AnimatorConditionMode.Greater,
                0.05f,
                MecanimCharacterParameters.VerticalSpeed);
            AddConditionTransition(
                locomotion,
                falling,
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.Grounded,
                AnimatorConditionMode.Less,
                0.05f,
                MecanimCharacterParameters.VerticalSpeed);
            AddConditionTransition(
                jumpUp,
                falling,
                AnimatorConditionMode.Less,
                -0.05f,
                MecanimCharacterParameters.VerticalSpeed);
            AddConditionTransition(
                jumpUp,
                landing,
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.Grounded);
            AddConditionTransition(
                falling,
                landing,
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.Grounded);
            AddExitTransition(landing, locomotion, 0.72f, TransitionSeconds);

            AddActionTransition(machine, dodgeF, 1, 0);
            AddActionTransition(machine, dodgeB, 1, 1);
            AddActionTransition(machine, attack, 2, null);
            AddExitTransition(dodgeF, locomotion, 0.9f, TransitionSeconds);
            AddExitTransition(dodgeB, locomotion, 0.9f, TransitionSeconds);
            AddExitTransition(attack, locomotion, 0.95f, TransitionSeconds);
            AddCancelTransition(dodgeF, locomotion);
            AddCancelTransition(dodgeB, locomotion);
            AddCancelTransition(attack, locomotion);

            EditorUtility.SetDirty(locomotionTree);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddParameters(AnimatorController controller)
        {
            AddParameter(controller, MecanimCharacterParameters.MotionForward,
                AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.MotionLateral,
                AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.MotionSpeed,
                AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.VerticalSpeed,
                AnimatorControllerParameterType.Float);

            var grounded = new AnimatorControllerParameter
            {
                name = MecanimCharacterParameters.Grounded,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            controller.AddParameter(grounded);

            var gait = new AnimatorControllerParameter
            {
                name = MecanimCharacterParameters.Gait,
                type = AnimatorControllerParameterType.Int,
                defaultInt = 2
            };
            controller.AddParameter(gait);
            AddParameter(controller, MecanimCharacterParameters.LocomotionMode,
                AnimatorControllerParameterType.Int);
            AddParameter(controller, MecanimCharacterParameters.Action,
                AnimatorControllerParameterType.Trigger);
            AddParameter(controller, MecanimCharacterParameters.ActionId,
                AnimatorControllerParameterType.Int);
            AddParameter(controller, MecanimCharacterParameters.ActionVariant,
                AnimatorControllerParameterType.Int);
            AddParameter(controller, MecanimCharacterParameters.CancelAction,
                AnimatorControllerParameterType.Trigger);
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            controller.AddParameter(name, type);
        }

        private static AnimatorState AddState(
            AnimatorStateMachine machine,
            string name,
            Motion motion,
            float speed,
            Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = motion;
            state.speed = Mathf.Max(0.01f, speed);
            state.writeDefaultValues = false;
            return state;
        }

        private static void AddBlendChild(
            BlendTree tree,
            Motion motion,
            float threshold,
            float timeScale)
        {
            tree.AddChild(motion, threshold);
            ChildMotion[] children = tree.children;
            ChildMotion child = children[^1];
            child.timeScale = Mathf.Max(0.01f, timeScale);
            children[^1] = child;
            tree.children = children;
        }

        private static void AddConditionTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode mode,
            float threshold,
            string parameter)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            ConfigureImmediateTransition(transition, TransitionSeconds);
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddConditionTransition(
            AnimatorState source,
            AnimatorState destination,
            AnimatorConditionMode firstMode,
            float firstThreshold,
            string firstParameter,
            AnimatorConditionMode secondMode,
            float secondThreshold,
            string secondParameter)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            ConfigureImmediateTransition(transition, TransitionSeconds);
            transition.AddCondition(firstMode, firstThreshold, firstParameter);
            transition.AddCondition(secondMode, secondThreshold, secondParameter);
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

        private static void AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float durationSeconds)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = durationSeconds;
            transition.interruptionSource = TransitionInterruptionSource.None;
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
            GameObject visualPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
            if (visualPrefab == null)
            {
                throw new InvalidOperationException(
                    $"KurtzPel preview prefab is missing: {PreviewPrefabPath}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform[] oldVisuals = root.GetComponentsInChildren<Transform>(true)
                    .Where(transform =>
                        transform != root.transform &&
                        transform.name == "CharacterVisual")
                    .ToArray();
                for (int i = oldVisuals.Length - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(oldVisuals[i].gameObject);
                }

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(
                    visualPrefab,
                    root.scene);
                visual.name = "CharacterVisual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0.0f, KurtzPelVisualYaw, 0.0f);
                visual.transform.localScale = Vector3.one * 0.6f;

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        "KurtzPel player visual has no Animator.");
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                Transform leftHand = FindRequiredBone(visual.transform, "Hand_L");
                Transform rightHand = FindRequiredBone(visual.transform, "Hand_R");
                Weapon leftHitbox = CreateUnarmedHitbox(leftHand, "Hitbox_LeftHand");
                Weapon rightHitbox = CreateUnarmedHitbox(rightHand, "Hitbox_RightHand");

                ThirdPersonController player = root.GetComponent<ThirdPersonController>();
                if (player == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab has no ThirdPersonController.");
                }

                var playerObject = new SerializedObject(player);
                playerObject.FindProperty("_animator").objectReferenceValue = animator;
                SetObjectArray(
                    playerObject.FindProperty("_unarmedHitboxes"),
                    leftHitbox,
                    rightHitbox);
                playerObject.FindProperty("_weaponHolder").objectReferenceValue = null;
                playerObject.FindProperty("_weaponHand").objectReferenceValue = null;
                playerObject.FindProperty("_weapon").objectReferenceValue = null;
                playerObject.ApplyModifiedPropertiesWithoutUndo();

                BodyColliderAuthoring authoring = root.GetComponent<BodyColliderAuthoring>();
                if (authoring == null)
                {
                    authoring = root.AddComponent<BodyColliderAuthoring>();
                }

                var authoringObject = new SerializedObject(authoring);
                authoringObject.FindProperty("_rigType").enumValueIndex = (int)BodyRigType.Auto;
                authoringObject.FindProperty("_animator").objectReferenceValue = animator;
                authoringObject.FindProperty("_health").objectReferenceValue =
                    root.GetComponent<Health>();
                authoringObject.FindProperty("_sourceMeshes").ClearArray();
                authoringObject.FindProperty("_genericBones").ClearArray();
                authoringObject.ApplyModifiedPropertiesWithoutUndo();

                BodyColliderGenerationResult colliderResult =
                    BodyColliderGenerator.Generate(authoring);
                if (!colliderResult.Success)
                {
                    throw new InvalidOperationException(colliderResult.Message);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return colliderResult;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Weapon CreateUnarmedHitbox(Transform hand, string objectName)
        {
            var node = new GameObject(objectName);
            node.transform.SetParent(hand, false);
            node.layer = Mathf.Max(0, LayerMask.NameToLayer("Hitbox"));

            SphereCollider collider = node.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = WorldToLocalRadius(node.transform, 0.14f);
            collider.enabled = false;

            Weapon weapon = node.AddComponent<Weapon>();
            var serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("_hitbox").objectReferenceValue = collider;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            return weapon;
        }

        private static float WorldToLocalRadius(Transform transform, float worldRadius)
        {
            Vector3 scale = transform.lossyScale;
            float averageScale =
                (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
            return worldRadius / Mathf.Max(0.0001f, averageScale);
        }

        private static Transform FindRequiredBone(Transform root, string boneName)
        {
            Transform bone = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == boneName);
            return bone != null
                ? bone
                : throw new InvalidOperationException(
                    $"KurtzPel player bone '{boneName}' was not found.");
        }

        private static void SetObjectArray(
            SerializedProperty property,
            params UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separatorIndex = folderPath.LastIndexOf('/');
            string parentPath = folderPath[..separatorIndex];
            string folderName = folderPath[(separatorIndex + 1)..];
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
