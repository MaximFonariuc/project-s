using System;
using System.Collections.Generic;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public readonly struct XControllerParameterRequirement
    {
        public string Name { get; }
        public AnimatorControllerParameterType Type { get; }

        public XControllerParameterRequirement(
            string name,
            AnimatorControllerParameterType type)
        {
            Name = name;
            Type = type;
        }
    }

    /// <summary>
    /// Stable Project S contract shared by the runtime driver and editor
    /// validation. It describes names only; gameplay state remains outside
    /// Mecanim.
    /// </summary>
    public static class XControllerAnimationContract
    {
        public const int BaseLayerIndex = 0;

        public const string BaseLayerName = "Base Layer";
        public const string SwitchWeaponLayerName = "SwitchWeapon Layer";
        public const string DefenseLayerName = "Defense Layer";
        public const string BodyLookLayerName = "BodyLook";
        public const string HeadLookLayerName = "HeadLook";

        public const string OnGroundParameterName = "OnGround";
        public const string MovementDirectionParameterName = "MovementDirection";
        public const string MovementTypeParameterName = "MovementType";
        public const string FallingDurationParameterName = "FallingDuration";
        public const string SpeedMultiplierParameterName = "SpeedMultiplier";
        public const string BodyLookXParameterName = "BodyLookX";
        public const string BodyLookYParameterName = "BodyLookY";
        public const string HeadLookXParameterName = "HeadLookX";
        public const string HeadLookYParameterName = "HeadLookY";

        public const string MoveStatePath = "Base Layer.Locomotion.Move";
        public const string RollForwardStatePath =
            "Base Layer.Locomotion.Roll.RollFront";
        public const string RollBackwardStatePath =
            "Base Layer.Locomotion.Roll.RollBack";
        public const string Attack1StatePath = "Base Layer.Attack.BaseAttack1";
        public const string Attack2StatePath = "Base Layer.Attack.BaseAttack2";
        public const string Attack3StatePath = "Base Layer.Attack.BaseAttack3";
        public const string Attack4StatePath = "Base Layer.Attack.BaseAttack4";
        public const string HeavyAttackStatePath = Attack4StatePath;
        public const string LauncherStatePath = "Base Layer.Attack.Launcher";
        public const string AirChaseStatePath =
            "Base Layer.Locomotion.Jump.Jump.JumpRunning";
        public const string SpecialAttackStatePath = "Base Layer.Attack.Skill";
        public const string AirAttack1StatePath = "Base Layer.Attack.AirAttack1";
        public const string AirAttack2StatePath = "Base Layer.Attack.AirAttack2";
        public const string AirAttack3StatePath = "Base Layer.Attack.AirAttack3";
        public const string HitStunStatePath = "Base Layer.Reaction.HitStun";
        public const string HitStunBackStatePath =
            "Base Layer.Reaction.HitStun_Back";
        public const string HitStunLeftStatePath =
            "Base Layer.Reaction.HitStun_Left";
        public const string HitStunRightStatePath =
            "Base Layer.Reaction.HitStun_Right";
        public const string KnockdownStatePath = "Base Layer.Reaction.Knockdown";
        public const string AirborneReactionStatePath =
            "Base Layer.Reaction.Airborne";
        public const string DeadPrimaryStatePath =
            "Base Layer.Reaction.Dead_1";
        public const string DeadHeavyStatePath =
            "Base Layer.Reaction.Dead_2";
        public const string GetUpStatePath = "Base Layer.Reaction.GetUp";
        public const string GuardStatePath = "Defense Layer.Guard";
        public const string BlockStatePath = "Base Layer.Reaction.Block";
        public const string ParryStatePath = "Base Layer.Reaction.Parry";
        public const string DefenseWaitingStatePath = "Defense Layer.Waiting";
        public const string WeaponWaitingStatePath =
            "SwitchWeapon Layer.Waiting";
        public const string WeaponSheatheStatePath =
            "SwitchWeapon Layer.Sheathe";
        public const string WeaponDrawStatePath =
            "SwitchWeapon Layer.Draw";

        public const string LadderEnterTopFrontStatePath =
            "Base Layer.Ladder.Ladder_Enter_Top_Front";
        public const string LadderEnterTopBackStatePath =
            "Base Layer.Ladder.Ladder_Enter_Top_Back";
        public const string LadderEnterBottomStatePath =
            "Base Layer.Ladder.Ladder_Enter_Bottom";
        public const string LadderIdleStatePath =
            "Base Layer.Ladder.Ladder_Idle";
        public const string LadderAscendStatePath =
            "Base Layer.Ladder.Ladder_Ascend";
        public const string LadderDescendStatePath =
            "Base Layer.Ladder.Ladder_Descend";
        public const string LadderExitTopStatePath =
            "Base Layer.Ladder.Ladder_Exit_Top";
        public const string LadderExitBottomStatePath =
            "Base Layer.Ladder.Ladder_Exit_Bottom";

        private static readonly IReadOnlyList<XControllerParameterRequirement>
            Parameters = Array.AsReadOnly(
                new[]
                {
                    new XControllerParameterRequirement(
                        OnGroundParameterName,
                        AnimatorControllerParameterType.Bool),
                    new XControllerParameterRequirement(
                        MovementDirectionParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        MovementTypeParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        FallingDurationParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        SpeedMultiplierParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        BodyLookXParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        BodyLookYParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        HeadLookXParameterName,
                        AnimatorControllerParameterType.Float),
                    new XControllerParameterRequirement(
                        HeadLookYParameterName,
                        AnimatorControllerParameterType.Float)
                });

        private static readonly IReadOnlyList<string> BaseLayerStates =
            Array.AsReadOnly(
                new[]
                {
                    MoveStatePath,
                    RollForwardStatePath,
                    RollBackwardStatePath,
                    Attack1StatePath,
                    Attack2StatePath,
                    Attack3StatePath,
                    LauncherStatePath,
                    HeavyAttackStatePath,
                    SpecialAttackStatePath,
                    AirAttack1StatePath,
                    AirAttack2StatePath,
                    AirAttack3StatePath,
                    AirChaseStatePath,
                    HitStunStatePath,
                    KnockdownStatePath,
                    AirborneReactionStatePath,
                    DeadPrimaryStatePath,
                    DeadHeavyStatePath,
                    GetUpStatePath,
                    HitStunBackStatePath,
                    HitStunLeftStatePath,
                    HitStunRightStatePath,
                    BlockStatePath,
                    ParryStatePath
                });

        private static readonly IReadOnlyList<string> TraversalStates =
            Array.AsReadOnly(
                new[]
                {
                    LadderEnterTopFrontStatePath,
                    LadderEnterTopBackStatePath,
                    LadderEnterBottomStatePath,
                    LadderIdleStatePath,
                    LadderAscendStatePath,
                    LadderDescendStatePath,
                    LadderExitTopStatePath,
                    LadderExitBottomStatePath
                });

        private static readonly IReadOnlyList<string> DefenseLayerStates =
            Array.AsReadOnly(
                new[]
                {
                    DefenseWaitingStatePath,
                    GuardStatePath
                });

        private static readonly IReadOnlyList<string> LightAttackStates =
            Array.AsReadOnly(
                new[]
                {
                    Attack1StatePath,
                    Attack2StatePath,
                    Attack3StatePath,
                    Attack4StatePath
                });

        private static readonly IReadOnlyList<string> ReactionStates =
            Array.AsReadOnly(
                new[]
                {
                    HitStunStatePath,
                    KnockdownStatePath,
                    AirborneReactionStatePath,
                    DeadPrimaryStatePath,
                    GetUpStatePath,
                    DeadHeavyStatePath,
                    HitStunBackStatePath,
                    HitStunLeftStatePath,
                    HitStunRightStatePath
                });

        private static readonly IReadOnlyList<string> AirAttackStates =
            Array.AsReadOnly(
                new[]
                {
                    AirAttack1StatePath,
                    AirAttack2StatePath,
                    AirAttack3StatePath
                });

        private static readonly IReadOnlyList<string> HitStunStates =
            Array.AsReadOnly(
                new[]
                {
                    HitStunStatePath,
                    HitStunBackStatePath,
                    HitStunLeftStatePath,
                    HitStunRightStatePath
                });

        private static readonly IReadOnlyList<string> SwitchWeaponStates =
            Array.AsReadOnly(
                new[]
                {
                    WeaponWaitingStatePath,
                    WeaponSheatheStatePath,
                    WeaponDrawStatePath
                });

        public static IReadOnlyList<XControllerParameterRequirement>
            RequiredParameters => Parameters;

        public static IReadOnlyList<string> RequiredBaseLayerStatePaths =>
            BaseLayerStates;

        public static IReadOnlyList<string> LightAttackStatePaths =>
            LightAttackStates;

        public static IReadOnlyList<string> AirAttackStatePaths =>
            AirAttackStates;

        public static IReadOnlyList<string> ReactionStatePaths =>
            ReactionStates;

        public static IReadOnlyList<string> TraversalStatePaths =>
            TraversalStates;

        public static IReadOnlyList<string> HitStunStatePaths =>
            HitStunStates;

        public static IReadOnlyList<string> RequiredSwitchWeaponStatePaths =>
            SwitchWeaponStates;

        public static IReadOnlyList<string> RequiredDefenseLayerStatePaths =>
            DefenseLayerStates;

        public static bool TryGetCombatStatePath(
            PlayerCombatPresentationKind presentationKind,
            int comboIndex,
            PlayerCombatAnimationId animationId,
            out string statePath)
        {
            if (!PlayerCombatPresentationResolver.TryResolve(
                    presentationKind,
                    comboIndex,
                    animationId,
                    out AnimationActionId action,
                    out int variant))
            {
                statePath = null;
                return false;
            }

            return TryGetCombatActionStatePath(action, variant, out statePath);
        }

        internal static bool TryGetCombatActionStatePath(
            AnimationActionId action,
            int variant,
            out string statePath)
        {
            if (action == PlayerAnimatorActions.LightAttack)
            {
                return TryGetStatePath(LightAttackStates, variant, out statePath);
            }

            if (action == PlayerAnimatorActions.AirAttack)
            {
                return TryGetStatePath(AirAttackStates, variant, out statePath);
            }

            if (action == PlayerAnimatorActions.HeavyAttack && variant == 0)
            {
                statePath = HeavyAttackStatePath;
                return true;
            }

            if (action == PlayerAnimatorActions.SpecialAttack && variant == 0)
            {
                statePath = SpecialAttackStatePath;
                return true;
            }

            if (action == PlayerAnimatorActions.Launcher && variant == 0)
            {
                statePath = LauncherStatePath;
                return true;
            }

            if (action == PlayerAnimatorActions.AirChase && variant == 0)
            {
                statePath = AirChaseStatePath;
                return true;
            }

            statePath = null;
            return false;
        }

        internal static bool TryGetDefenseActionStatePath(
            AnimationActionId action,
            int variant,
            out string statePath,
            out float normalizedTimeOffset)
        {
            normalizedTimeOffset = 0.0f;
            if (action == PlayerAnimatorActions.Guard && variant is 0 or 1)
            {
                statePath = GuardStatePath;
                normalizedTimeOffset = variant == 1 ? 1.0f : 0.0f;
                return true;
            }

            if (action == PlayerAnimatorActions.Block && variant == 0)
            {
                statePath = BlockStatePath;
                return true;
            }

            if (action == PlayerAnimatorActions.Parry && variant == 0)
            {
                statePath = ParryStatePath;
                return true;
            }

            statePath = null;
            return false;
        }

        private static bool TryGetStatePath(
            IReadOnlyList<string> paths,
            int index,
            out string statePath)
        {
            if (index >= 0 && index < paths.Count)
            {
                statePath = paths[index];
                return true;
            }

            statePath = null;
            return false;
        }
    }
}
