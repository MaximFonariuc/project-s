using ModularAnimation;

namespace _App
{
    internal static class PlayerAnimationSlots
    {
        public static readonly AnimationSlotId DodgeForward =
            new AnimationSlotId("Locomotion.Dodge.Forward");
        public static readonly AnimationSlotId DodgeBackward =
            new AnimationSlotId("Locomotion.Dodge.Backward");
        public static readonly AnimationSlotId Grounded =
            new AnimationSlotId("Locomotion.Grounded");
        public static readonly AnimationSlotId JumpWalk =
            new AnimationSlotId("Locomotion.Jump.Walk");
        public static readonly AnimationSlotId JumpRun =
            new AnimationSlotId("Locomotion.Jump.Run");
        public static readonly AnimationSlotId JumpSprint =
            new AnimationSlotId("Locomotion.Jump.Sprint");
        public static readonly AnimationSlotId Fall =
            new AnimationSlotId("Locomotion.Fall");
        public static readonly AnimationSlotId LandSoft =
            new AnimationSlotId("Locomotion.Land.Soft");
        public static readonly AnimationSlotId LandMedium =
            new AnimationSlotId("Locomotion.Land.Medium");
        public static readonly AnimationSlotId LandHard =
            new AnimationSlotId("Locomotion.Land.Hard");
        public static readonly AnimationSlotId LadderEnterTopFront =
            new AnimationSlotId("Traversal.Ladder.EnterTopFront");
        public static readonly AnimationSlotId LadderEnterTopBack =
            new AnimationSlotId("Traversal.Ladder.EnterTopBack");
        public static readonly AnimationSlotId LadderEnterBottom =
            new AnimationSlotId("Traversal.Ladder.EnterBottom");
        public static readonly AnimationSlotId LadderExitTop =
            new AnimationSlotId("Traversal.Ladder.ExitTop");
        public static readonly AnimationSlotId LadderExitBottom =
            new AnimationSlotId("Traversal.Ladder.ExitBottom");
        public static readonly AnimationSlotId LadderAscend =
            new AnimationSlotId("Traversal.Ladder.Ascend");
        public static readonly AnimationSlotId LadderDescend =
            new AnimationSlotId("Traversal.Ladder.Descend");
        public static readonly AnimationSlotId LadderIdle =
            new AnimationSlotId("Traversal.Ladder.Idle");
        public static readonly AnimationSlotId HeavyDeath =
            Reaction(
                new CombatReactionId("hit.heavy"),
                CharacterReactionStateId.Dead);

        public static AnimationSlotId Reaction(CharacterReactionStateId state) =>
            new AnimationSlotId($"Reaction.{state}");

        public static AnimationSlotId Reaction(
            CombatReactionId reactionId,
            CharacterReactionStateId state) =>
            reactionId.IsValid
                ? new AnimationSlotId($"Reaction.{state}.{reactionId.Value}")
                : Reaction(state);
    }
}
