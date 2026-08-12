using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "GravityDirectionAbilityData", menuName = "Abilities/GravityDirection")]
    public class GravityDirectionAbilityData : AbilityData
    {
        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new GravityDirectionAbility(character);
            return ability;
        }
    }
}