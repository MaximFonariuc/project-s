using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "SprintAbilityData", menuName = "Abilities/Sprint")]
    public class SprintAbilityData : AbilityData
    {
        [SerializeField] private float _maxSprintSpeed = 10.0f;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new SprintAbility(character, _maxSprintSpeed);
            return ability;
        }
    }
}