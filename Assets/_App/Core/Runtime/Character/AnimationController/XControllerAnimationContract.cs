using System;
using System.Collections.Generic;
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
        public const string HitStunStatePath = "Base Layer.Reaction.HitStun";
        public const string KnockdownStatePath = "Base Layer.Reaction.Knockdown";
        public const string AirborneReactionStatePath =
            "Base Layer.Reaction.Airborne";
        public const string DeadStatePath = "Base Layer.Reaction.Dead";
        public const string WeaponWaitingStatePath =
            "SwitchWeapon Layer.Waiting";
        public const string WeaponSheatheStatePath =
            "SwitchWeapon Layer.TwoHanded_Sheathe";
        public const string WeaponUnsheatheStatePath =
            "SwitchWeapon Layer.TwoHanded_Unsheathe";

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
                    Attack4StatePath,
                    HitStunStatePath,
                    KnockdownStatePath,
                    AirborneReactionStatePath,
                    DeadStatePath
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
                    DeadStatePath
                });

        private static readonly IReadOnlyList<string> SwitchWeaponStates =
            Array.AsReadOnly(
                new[]
                {
                    WeaponWaitingStatePath,
                    WeaponSheatheStatePath,
                    WeaponUnsheatheStatePath
                });

        public static IReadOnlyList<XControllerParameterRequirement>
            RequiredParameters => Parameters;

        public static IReadOnlyList<string> RequiredBaseLayerStatePaths =>
            BaseLayerStates;

        public static IReadOnlyList<string> LightAttackStatePaths =>
            LightAttackStates;

        public static IReadOnlyList<string> ReactionStatePaths =>
            ReactionStates;

        public static IReadOnlyList<string> RequiredSwitchWeaponStatePaths =>
            SwitchWeaponStates;
    }
}
