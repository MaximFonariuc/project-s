using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "SwimmingAbilityData", menuName = "Abilities/Swim")]
    public class SwimmingAbilityData : AbilityData
    {
        [SerializeField] private bool _canEverSwim = true;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new SwimAbility(character, _canEverSwim, animationController);
            return ability;
        }
    }
}