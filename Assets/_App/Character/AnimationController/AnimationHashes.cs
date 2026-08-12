using UnityEngine;

namespace _App
{
	public static class AnimationHashes
	{
		public static readonly int Action = Animator.StringToHash("Action");
		public static readonly int SpeedMultiplier = Animator.StringToHash("SpeedMultiplier"); 

		// Movement
		public static readonly int MovementType = Animator.StringToHash("MovementType");
		public static readonly int MovementDirection = Animator.StringToHash("MovementDirection");

		public static readonly int OnGround = Animator.StringToHash("OnGround");
		public static readonly int IsCrouch = Animator.StringToHash("Crouch");

		// Jump
		public static readonly int FallingDuration = Animator.StringToHash("FallingDuration");
		public static readonly int DoubleJumpTrigger = Animator.StringToHash("DoubleJumpTrigger");

		// Combat
		public static readonly int IsRelax = Animator.StringToHash("Relax");
		public static readonly int Skill = Animator.StringToHash("Skill");
		public static readonly int AttackTrigger = Animator.StringToHash("AttackTrigger");
		public static readonly int ChangeCombatStateTrigger = Animator.StringToHash("ChangeCombatStateTrigger");

		public static readonly int RollFront = Animator.StringToHash("RollFront");
		public static readonly int RollBack = Animator.StringToHash("RollBack");

		// LadderClimbing
		public static readonly int ClimbLadderTrigger = Animator.StringToHash("ClimbLadderTrigger");
		public static readonly int ClimbDirection = Animator.StringToHash("ClimbDirection");

		public static readonly int LadderEnterTopFront = Animator.StringToHash("Ladder_Enter_Top_Front");
		public static readonly int LadderEnterTopBack = Animator.StringToHash("Ladder_Enter_Top_Back");
		public static readonly int LadderEnterBottom = Animator.StringToHash("Ladder_Enter_Bottom");

		public static readonly int LadderExitTop = Animator.StringToHash("Ladder_Exit_Top");
		public static readonly int LadderExitBottom = Animator.StringToHash("Ladder_Exit_Bottom");

		public static readonly int LadderAscend = Animator.StringToHash("Ladder_Ascend");
		public static readonly int LadderDescend = Animator.StringToHash("Ladder_Descend");

		public static readonly int LadderIdle = Animator.StringToHash("Ladder_Idle");

		// Swim
		public static readonly int IsSwimming = Animator.StringToHash("Swimming");
		public static readonly int SwimTrigger = Animator.StringToHash("SwimTrigger");
		public static readonly int SwimDirection = Animator.StringToHash("SwimDirection");

		// HeadLook
		public static readonly int HeadLookX = Animator.StringToHash("HeadLookX");
		public static readonly int HeadLookY = Animator.StringToHash("HeadLookY");
		
		//Damage
		public static readonly int HitFront = Animator.StringToHash("HitFront");

	}
}
