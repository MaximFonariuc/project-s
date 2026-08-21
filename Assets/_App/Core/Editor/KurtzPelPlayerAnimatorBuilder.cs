using System;
using System.Linq;
using ModularAnimation.Unity;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class KurtzPelPlayerAnimatorBuilder
    {
        public const string ControllerPath =
            "Assets/_Game/Animations/Character/1. Controllers/PlayerKurtzPel.controller";

        private const float IdleEnterSpeed = 0.10f;
        private const float MoveEnterSpeed = 0.14f;
        private const float WalkReferenceSpeed = 3.0f;
        private const float RunReferenceSpeed = 5.0f;
        private const float SprintReferenceSpeed = 10.0f;

        private const float LocomotionBlendSeconds = 0.12f;
        private const float JumpBlendSeconds = 0.045f;
        private const float LandingMoveBlendSeconds = 0.10f;
        private const float LandingIdleBlendSeconds = 0.12f;
        private const float ActionEntryBlendSeconds = 0.055f;
        private const float ActionExitBlendSeconds = 0.08f;
        private const float AttackExitBlendSeconds = 0.12f;
        private const float DodgeDurationSeconds = 0.48f;
        private const float DodgeExitTime = 0.75f;
        private const float AttackExitTime = 0.88f;

        public static AnimatorController CreateOrValidate(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip sprint,
            AnimationClip jumpStart,
            AnimationClip jumpRise,
            AnimationClip fall,
            AnimationClip landing,
            AnimationClip dodgeForward,
            AnimationClip dodgeBackward,
            AnimationClip lightAttack)
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                KurtzPelPlayerAnimatorValidator.ValidateOrThrow(existing);
                return existing;
            }

            EnsureParentFolder();
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddParameters(controller);

            AnimatorControllerLayer layer = controller.layers[0];
            layer.name = "Base Layer";
            layer.defaultWeight = 1.0f;
            controller.layers = new[] { layer };

            AnimatorStateMachine root = layer.stateMachine;
            root.name = "Base Layer";
            root.anyStatePosition = new Vector3(20f, -120f, 0f);
            root.entryPosition = new Vector3(20f, 10f, 0f);

            AnimatorState idleState = AddState(root, "Idle", idle, new Vector3(230f, 10f, 0f), "Locomotion");
            AnimatorState moveState = AddState(
                root,
                "Locomotion",
                CreateLocomotionTree(controller, walk, run, sprint),
                new Vector3(490f, 10f, 0f),
                "Locomotion");
            moveState.speedParameterActive = true;
            moveState.speedParameter = MecanimCharacterParameters.LocomotionPlaybackRate;

            AnimatorState jumpStartState = AddState(root, "Jump Start", jumpStart, new Vector3(230f, 180f, 0f), "Airborne");
            AnimatorState jumpRiseState = AddState(root, "Jump Rise", jumpRise, new Vector3(490f, 180f, 0f), "Airborne");
            AnimatorState fallState = AddState(root, "Fall", fall, new Vector3(730f, 180f, 0f), "Airborne");
            AnimatorState landingState = AddState(root, "Landing", landing, new Vector3(490f, 330f, 0f), "Airborne");

            AnimatorState dodgeForwardState = AddState(
                root,
                "Dodge Forward",
                dodgeForward,
                new Vector3(230f, -190f, 0f),
                "Action");
            AnimatorState dodgeBackwardState = AddState(
                root,
                "Dodge Backward",
                dodgeBackward,
                new Vector3(490f, -190f, 0f),
                "Action");
            AnimatorState lightAttackState = AddState(
                root,
                "Light Attack 1",
                lightAttack,
                new Vector3(730f, -190f, 0f),
                "Action");

            root.defaultState = idleState;
            ConfigureLocomotionTransitions(idleState, moveState);
            ConfigureAirTransitions(
                idleState,
                moveState,
                jumpStartState,
                jumpRiseState,
                fallState,
                landingState);
            ConfigureActions(
                root,
                idleState,
                moveState,
                fallState,
                dodgeForwardState,
                dodgeBackwardState,
                lightAttackState);

            FitDodgeState(dodgeForwardState, dodgeForward);
            FitDodgeState(dodgeBackwardState, dodgeBackward);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            KurtzPelPlayerAnimatorValidator.ValidateOrThrow(controller);
            return controller;
        }

        private static void ConfigureLocomotionTransitions(
            AnimatorState idle,
            AnimatorState locomotion)
        {
            AnimatorStateTransition beginMove = AddImmediateTransition(
                idle,
                locomotion,
                LocomotionBlendSeconds,
                TransitionInterruptionSource.DestinationThenSource);
            beginMove.AddCondition(
                AnimatorConditionMode.Greater,
                MoveEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);

            AnimatorStateTransition stopMove = AddImmediateTransition(
                locomotion,
                idle,
                LocomotionBlendSeconds,
                TransitionInterruptionSource.DestinationThenSource);
            stopMove.AddCondition(
                AnimatorConditionMode.Less,
                IdleEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);
        }

        private static void ConfigureAirTransitions(
            AnimatorState idle,
            AnimatorState locomotion,
            AnimatorState jumpStart,
            AnimatorState jumpRise,
            AnimatorState fall,
            AnimatorState landing)
        {
            AddJumpRequest(idle, jumpStart);
            AddJumpRequest(locomotion, jumpStart);
            AddAirborneEntry(idle, jumpRise, fall);
            AddAirborneEntry(locomotion, jumpRise, fall);

            AnimatorStateTransition finishAnticipation = AddImmediateTransition(
                jumpStart,
                jumpRise,
                JumpBlendSeconds,
                TransitionInterruptionSource.Destination);
            finishAnticipation.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.JumpStarting);

            AnimatorStateTransition reachApex = AddImmediateTransition(
                jumpRise,
                fall,
                0.08f,
                TransitionInterruptionSource.Destination);
            reachApex.AddCondition(
                AnimatorConditionMode.Less,
                0f,
                MecanimCharacterParameters.VerticalSpeed);

            AddGroundedTransition(jumpRise, landing, 0.07f);
            AddGroundedTransition(fall, landing, 0.08f);

            AnimatorStateTransition landToMove = AddExitTransition(
                landing,
                locomotion,
                0.20f,
                LandingMoveBlendSeconds);
            landToMove.AddCondition(
                AnimatorConditionMode.Greater,
                MoveEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);

            AnimatorStateTransition landToIdle = AddExitTransition(
                landing,
                idle,
                0.55f,
                LandingIdleBlendSeconds);
            landToIdle.AddCondition(
                AnimatorConditionMode.Less,
                MoveEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);

            AddAirborneEntry(landing, jumpRise, fall);
        }

        private static void ConfigureActions(
            AnimatorStateMachine root,
            AnimatorState idle,
            AnimatorState locomotion,
            AnimatorState fall,
            AnimatorState dodgeForward,
            AnimatorState dodgeBackward,
            AnimatorState lightAttack)
        {
            AddActionEntry(root, dodgeForward, 1, 0);
            AddActionEntry(root, dodgeBackward, 1, 1);
            AddActionEntry(root, lightAttack, 2, null);

            ConfigureActionExit(dodgeForward, idle, locomotion, fall, DodgeExitTime, ActionExitBlendSeconds);
            ConfigureActionExit(dodgeBackward, idle, locomotion, fall, DodgeExitTime, ActionExitBlendSeconds);
            ConfigureActionExit(lightAttack, idle, locomotion, fall, AttackExitTime, AttackExitBlendSeconds);
        }

        private static void ConfigureActionExit(
            AnimatorState action,
            AnimatorState idle,
            AnimatorState locomotion,
            AnimatorState fall,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition airborne = AddImmediateTransition(
                action,
                fall,
                0.05f,
                TransitionInterruptionSource.Destination);
            airborne.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.Grounded);

            AddSpeedPartitionedExit(action, idle, locomotion, exitTime, duration, false);
            AddSpeedPartitionedExit(action, idle, locomotion, 0f, 0.055f, true);
        }

        private static void AddSpeedPartitionedExit(
            AnimatorState source,
            AnimatorState idle,
            AnimatorState locomotion,
            float exitTime,
            float duration,
            bool cancel)
        {
            AnimatorStateTransition toLocomotion = cancel
                ? AddImmediateTransition(source, locomotion, duration, TransitionInterruptionSource.Destination)
                : AddExitTransition(source, locomotion, exitTime, duration);
            toLocomotion.AddCondition(
                AnimatorConditionMode.Greater,
                MoveEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);
            if (cancel)
            {
                toLocomotion.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    MecanimCharacterParameters.CancelAction);
            }

            AnimatorStateTransition toIdle = cancel
                ? AddImmediateTransition(source, idle, duration, TransitionInterruptionSource.Destination)
                : AddExitTransition(source, idle, exitTime, duration);
            toIdle.AddCondition(
                AnimatorConditionMode.Less,
                MoveEnterSpeed,
                MecanimCharacterParameters.PlanarSpeed);
            if (cancel)
            {
                toIdle.AddCondition(
                    AnimatorConditionMode.If,
                    0f,
                    MecanimCharacterParameters.CancelAction);
            }
        }

        private static void AddJumpRequest(AnimatorState source, AnimatorState jumpStart)
        {
            AnimatorStateTransition transition = AddImmediateTransition(
                source,
                jumpStart,
                JumpBlendSeconds,
                TransitionInterruptionSource.Destination);
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.JumpStarting);
        }

        private static void AddAirborneEntry(
            AnimatorState source,
            AnimatorState jumpRise,
            AnimatorState fall)
        {
            AnimatorStateTransition rising = AddImmediateTransition(
                source,
                jumpRise,
                JumpBlendSeconds,
                TransitionInterruptionSource.Destination);
            rising.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.Grounded);
            rising.AddCondition(
                AnimatorConditionMode.Greater,
                0.02f,
                MecanimCharacterParameters.VerticalSpeed);

            AnimatorStateTransition falling = AddImmediateTransition(
                source,
                fall,
                0.08f,
                TransitionInterruptionSource.Destination);
            falling.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                MecanimCharacterParameters.Grounded);
            falling.AddCondition(
                AnimatorConditionMode.Less,
                0.021f,
                MecanimCharacterParameters.VerticalSpeed);
        }

        private static void AddGroundedTransition(
            AnimatorState source,
            AnimatorState landing,
            float duration)
        {
            AnimatorStateTransition transition = AddImmediateTransition(
                source,
                landing,
                duration,
                TransitionInterruptionSource.Destination);
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                MecanimCharacterParameters.Grounded);
        }

        private static void AddActionEntry(
            AnimatorStateMachine root,
            AnimatorState destination,
            int actionId,
            int? variant)
        {
            AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
            ConfigureImmediateTransition(
                transition,
                ActionEntryBlendSeconds,
                TransitionInterruptionSource.None);
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

        private static AnimatorStateTransition AddImmediateTransition(
            AnimatorState source,
            AnimatorState destination,
            float duration,
            TransitionInterruptionSource interruption)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            ConfigureImmediateTransition(transition, duration, interruption);
            return transition;
        }

        private static AnimatorStateTransition AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.Destination;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            return transition;
        }

        private static void ConfigureImmediateTransition(
            AnimatorStateTransition transition,
            float duration,
            TransitionInterruptionSource interruption)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.offset = 0f;
            transition.interruptionSource = interruption;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
        }

        private static BlendTree CreateLocomotionTree(
            AnimatorController controller,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip sprint)
        {
            var tree = new BlendTree
            {
                name = "Locomotion Speed",
                blendType = BlendTreeType.Simple1D,
                blendParameter = MecanimCharacterParameters.PlanarSpeed,
                useAutomaticThresholds = false,
                minThreshold = WalkReferenceSpeed,
                maxThreshold = SprintReferenceSpeed,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(walk, WalkReferenceSpeed);
            tree.AddChild(run, RunReferenceSpeed);
            tree.AddChild(sprint, SprintReferenceSpeed);

            // Full-rig sampling places the Walk left/right contacts at
            // 0.1875/0.6875, Run at 0.5/0.03125 and Sprint at
            // 0.65625/0.1875. These offsets align the contact windows rather
            // than merely aligning clip frame zero.
            ChildMotion[] children = tree.children;
            children[0].cycleOffset = 0f;
            children[1].cycleOffset = 0.32f;
            children[2].cycleOffset = 0.47f;
            for (int i = 0; i < children.Length; i++)
            {
                children[i].timeScale = 1f;
                children[i].mirror = false;
            }

            tree.children = children;
            EditorUtility.SetDirty(tree);
            return tree;
        }

        private static AnimatorState AddState(
            AnimatorStateMachine machine,
            string name,
            Motion motion,
            Vector3 position,
            string tag)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = motion;
            state.speed = 1f;
            state.tag = tag;
            state.writeDefaultValues = false;
            state.iKOnFeet = false;
            return state;
        }

        private static void FitDodgeState(AnimatorState state, AnimationClip clip)
        {
            float sourceSecondsBeforeBlend =
                Mathf.Max(0.01f, DodgeDurationSeconds - ActionExitBlendSeconds);
            float desiredStateDuration = sourceSecondsBeforeBlend / DodgeExitTime;
            state.speed = clip.length / desiredStateDuration;
        }

        private static void AddParameters(AnimatorController controller)
        {
            AddParameter(controller, MecanimCharacterParameters.PlanarSpeed, AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.NormalizedSpeed, AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.MoveX, AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.MoveY, AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.VerticalSpeed, AnimatorControllerParameterType.Float);
            AddParameter(controller, MecanimCharacterParameters.Grounded, AnimatorControllerParameterType.Bool, defaultBool: true);
            AddParameter(controller, MecanimCharacterParameters.LocomotionPlaybackRate, AnimatorControllerParameterType.Float, defaultFloat: 1f);
            AddParameter(controller, MecanimCharacterParameters.JumpStarting, AnimatorControllerParameterType.Bool);
            AddParameter(controller, MecanimCharacterParameters.Action, AnimatorControllerParameterType.Trigger);
            AddParameter(controller, MecanimCharacterParameters.ActionId, AnimatorControllerParameterType.Int);
            AddParameter(controller, MecanimCharacterParameters.ActionVariant, AnimatorControllerParameterType.Int);
            AddParameter(controller, MecanimCharacterParameters.CancelAction, AnimatorControllerParameterType.Trigger);
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            float defaultFloat = 0f,
            bool defaultBool = false)
        {
            var parameter = new AnimatorControllerParameter
            {
                name = name,
                type = type,
                defaultFloat = defaultFloat,
                defaultBool = defaultBool
            };
            controller.AddParameter(parameter);
        }

        private static void EnsureParentFolder()
        {
            string parent = ControllerPath[..ControllerPath.LastIndexOf('/')];
            if (AssetDatabase.IsValidFolder(parent))
            {
                return;
            }

            throw new InvalidOperationException($"Animator Controller folder is missing: {parent}");
        }
    }

    internal static class KurtzPelPlayerAnimatorValidator
    {
        private static readonly (string Name, AnimatorControllerParameterType Type)[] RequiredParameters =
        {
            (MecanimCharacterParameters.PlanarSpeed, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.NormalizedSpeed, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.MoveX, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.MoveY, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.VerticalSpeed, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.Grounded, AnimatorControllerParameterType.Bool),
            (MecanimCharacterParameters.LocomotionPlaybackRate, AnimatorControllerParameterType.Float),
            (MecanimCharacterParameters.JumpStarting, AnimatorControllerParameterType.Bool),
            (MecanimCharacterParameters.Action, AnimatorControllerParameterType.Trigger),
            (MecanimCharacterParameters.ActionId, AnimatorControllerParameterType.Int),
            (MecanimCharacterParameters.ActionVariant, AnimatorControllerParameterType.Int),
            (MecanimCharacterParameters.CancelAction, AnimatorControllerParameterType.Trigger)
        };

        [MenuItem("Tools/Animation/KurtzPel/Validate Player Animator")]
        private static void ValidateMenu()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(KurtzPelPlayerAnimatorBuilder.ControllerPath);
            ValidateOrThrow(controller);
            Debug.Log($"[Player Animator] '{KurtzPelPlayerAnimatorBuilder.ControllerPath}' is valid.");
        }

        public static void ValidateOrThrow(AnimatorController controller)
        {
            if (controller == null)
            {
                throw new InvalidOperationException(
                    $"Player Animator Controller is missing: {KurtzPelPlayerAnimatorBuilder.ControllerPath}");
            }

            if (controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Player Animator must have exactly one base layer, found {controller.layers.Length}.");
            }

            foreach ((string name, AnimatorControllerParameterType type) in RequiredParameters)
            {
                AnimatorControllerParameter parameter = controller.parameters
                    .FirstOrDefault(candidate => candidate.name == name);
                if (parameter == null || parameter.type != type)
                {
                    throw new InvalidOperationException(
                        $"Player Animator requires parameter '{name}' of type {type}.");
                }
            }

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            string[] requiredStates =
            {
                "Idle",
                "Locomotion",
                "Jump Start",
                "Jump Rise",
                "Fall",
                "Landing",
                "Dodge Forward",
                "Dodge Backward",
                "Light Attack 1"
            };
            foreach (string stateName in requiredStates)
            {
                AnimatorState state = root.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name == stateName);
                if (state == null || state.motion == null)
                {
                    throw new InvalidOperationException(
                        $"Player Animator state '{stateName}' is missing or has no Motion.");
                }

                if (state.writeDefaultValues)
                {
                    throw new InvalidOperationException(
                        $"Player Animator state '{stateName}' must use Write Defaults Off.");
                }
            }

            AnimatorState locomotion = root.states
                .Select(child => child.state)
                .First(state => state.name == "Locomotion");
            if (locomotion.motion is not BlendTree tree || tree.children.Length != 3)
            {
                throw new InvalidOperationException(
                    "Locomotion must contain the phase-aligned Walk/Run/Sprint Blend Tree.");
            }

            if (root.anyStateTransitions.Length != 3)
            {
                throw new InvalidOperationException(
                    $"Player Animator expects three semantic action entries, found {root.anyStateTransitions.Length}.");
            }
        }
    }
}
