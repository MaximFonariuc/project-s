using System;
using UnityEngine;

namespace _App
{
    public enum PlayerDefenseStateId
    {
        Inactive = 0,
        Guarding = 1,
        BlockRecovery = 2,
        ParryRecovery = 3
    }

    public readonly struct PlayerDefenseSnapshot
    {
        public static PlayerDefenseSnapshot Inactive => default;

        public bool IsValid { get; }
        public PlayerDefenseStateId State { get; }
        public bool IsGuarding { get; }
        public bool IsParryWindowOpen { get; }
        public bool IsImpactRecovering { get; }
        public CombatHitOutcome ImpactOutcome { get; }

        internal PlayerDefenseSnapshot(
            PlayerDefenseStateId state,
            bool isGuarding,
            bool isParryWindowOpen,
            bool isImpactRecovering,
            CombatHitOutcome impactOutcome)
        {
            IsValid = true;
            State = state;
            IsGuarding = isGuarding;
            IsParryWindowOpen = isParryWindowOpen;
            IsImpactRecovering = isImpactRecovering;
            ImpactOutcome = impactOutcome;
        }
    }

    public readonly struct PlayerDefenseResult
    {
        public CombatHitOutcome Outcome { get; }
        public CombatHitResult Hit { get; }

        internal PlayerDefenseResult(
            CombatHitOutcome outcome,
            in CombatHitResult hit)
        {
            Outcome = outcome;
            Hit = hit;
        }
    }

    internal interface IPlayerDefenseActor
    {
        Transform Root { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }
        Vector3 Up { get; }
        bool CanDefend { get; }
    }

    internal interface IPlayerDefenseState
    {
        event Action GuardStarted;
        event Action GuardEnded;
        event Action<PlayerDefenseResult> Defended;

        PlayerDefenseSnapshot Snapshot { get; }
        bool IsGuarding { get; }
        bool BlocksActions { get; }
        PlayerMovementIntent MovementIntent { get; }
    }
}
