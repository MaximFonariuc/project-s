using UnityEngine;

namespace _App
{
    
    [CreateAssetMenu(fileName = "LandingForceAbilityData", menuName = "Abilities/LandingForce")]
    public class LandingForceAbilityData : AbilityData
    {
        [SerializeField] private float _landingForceScale = 1.0f;
        
        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new LandingForceAbility(character, _landingForceScale);
            return ability;
        }
    }
}