using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "FlyingAbilityData", menuName = "Abilities/Fly")]
    public class FlyingAbilityData : AbilityData
    {
        [SerializeField] private bool _canEverFly = true;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new FlyAbility(character, _canEverFly);
            return ability;
        }
    }
}