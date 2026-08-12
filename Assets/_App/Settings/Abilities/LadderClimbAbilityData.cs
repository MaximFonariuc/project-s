using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "LadderClimbAbilityData", menuName = "Abilities/Ladder Climb")]
    public class LadderClimbAbilityData : AbilityData
    {
        [SerializeField] private LayerMask _ladderMask = 1;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new LadderClimbAbility(character, animationController, _ladderMask);
            return ability;
        }
    }
}