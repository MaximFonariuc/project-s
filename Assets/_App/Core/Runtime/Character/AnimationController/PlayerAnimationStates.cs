using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal static class PlayerAnimationStates
    {
        public static readonly AnimationLayerId BaseLayer = AnimationLayerId.Base;
        public static readonly AnimationLayerId WeaponLayer = new AnimationLayerId(1);

        public static readonly AnimationStateId RollFront = State("RollFront");
        public static readonly AnimationStateId RollBack = State("RollBack");
        public static readonly AnimationStateId Skill = State("Skill");

        public static readonly AnimationStateId LadderEnterTopFront = State("Ladder_Enter_Top_Front");
        public static readonly AnimationStateId LadderEnterTopBack = State("Ladder_Enter_Top_Back");
        public static readonly AnimationStateId LadderEnterBottom = State("Ladder_Enter_Bottom");
        public static readonly AnimationStateId LadderExitTop = State("Ladder_Exit_Top");
        public static readonly AnimationStateId LadderExitBottom = State("Ladder_Exit_Bottom");
        public static readonly AnimationStateId LadderAscend = State("Ladder_Ascend");
        public static readonly AnimationStateId LadderDescend = State("Ladder_Descend");
        public static readonly AnimationStateId LadderIdle = State("Ladder_Idle");

        public static AnimationStateId State(string name) =>
            new AnimationStateId(Animator.StringToHash(name));
    }
}
