using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "WalkAbilityData", menuName = "Abilities/Walk")]
    public class WalkAbilityData : AbilityData
    {
        [SerializeField] private float _maxWalkSpeed = 3.0f;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new WalkAbility(_maxWalkSpeed);
            return ability;
        }
    }
}
