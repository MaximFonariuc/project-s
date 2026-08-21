using System;
using System.Linq;
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
        private const string ProcessedFolder =
            "Assets/_Game/Animations/Character/Processed/KurtzPel";
        private const string PlayerPrefabPath =
            "Assets/_Game/Prefabs/Character/Player.prefab";
        private const string CombatProfilePath =
            "Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset";
        private const string FeatureSetPath =
            "Assets/_Game/Settings/Character/AbillityData/AbilitiesData.asset";
        private const string DodgeSettingsPath =
            "Assets/_Game/Settings/Character/AbillityData/Abilities/DodgeAbilityData.asset";

        private const float DodgeDurationSeconds = 0.78f;
        private const float AttackDurationSeconds = 1.3f;

        [MenuItem("Tools/Animation/KurtzPel/Build Player Animator and Prefab")]
        public static void BuildPlayerAnimatorAndPrefab()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimatorController controller = KurtzPelPlayerAnimatorBuilder.CreateOrValidate(
                LoadClip(MovementPack, "HM_ENFJ_Wait"),
                LoadClip(MovementPack, "HM_ENFJ_Walk"),
                LoadClip(MovementPack, "HM_ENFJ_Run"),
                LoadClip(MovementPack, "HM_ENFJ_Run_Fast"),
                LoadClip(ProcessedFolder + "/HM_ENFJ_Air_Start.fbx", "HM_ENFJ_Air_Start"),
                LoadClip(ProcessedFolder + "/HM_ENFJ_Air_JumpUp.fbx", "HM_ENFJ_Air_JumpUp"),
                LoadClip(ProcessedFolder + "/HM_ENFJ_Air_JumpDown.fbx", "HM_ENFJ_Air_JumpDown"),
                LoadClip(ProcessedFolder + "/HM_ENFJ_Air_JumpLanding.fbx", "HM_ENFJ_Air_JumpLanding"),
                LoadClip(ProcessedFolder + "/HM_ENFJ_Dash.fbx", "HM_ENFJ_Dash"),
                LoadClip(ProcessedFolder + "/CC_C_Rage_Avoid.fbx", "CC_C_Rage_Avoid"),
                LoadClip(ProcessedFolder + "/HMGT_BasicCombo_01.fbx", "HMGT_BasicCombo_01"));

            ConfigureCombatProfile();
            ConfigureFeatureAssets();
            BodyColliderGenerationResult colliderResult = ConfigurePlayerPrefab(controller);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[KurtzPel Player Setup] Configured the phase-aligned Animator at " +
                $"'{KurtzPelPlayerAnimatorBuilder.ControllerPath}', Player prefab, dodge and " +
                $"the first unarmed attack. {colliderResult.Message}");
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

        private static BodyColliderGenerationResult ConfigurePlayerPrefab(
            RuntimeAnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                Animator animator = animators
                    .FirstOrDefault(candidate => candidate.name == "CharacterVisual");
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab has no CharacterVisual Animator to configure.");
                }

                if (animators.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Player prefab must contain one Animator, found {animators.Length}.");
                }

                if (animator.avatar == null || !animator.avatar.isValid)
                {
                    throw new InvalidOperationException(
                        "Player CharacterVisual requires a valid Generic or Humanoid Avatar.");
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
            ConfigureTimeline(serialized.FindProperty("_weaponTransition"), 0.8f, false);

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
            ConfigureTimeline(action.FindPropertyRelative("_action"), AttackDurationSeconds, true);

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
