using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "GlidingAbilityData", menuName = "Abilities/Glide")]
    public class GlidingAbilityData : AbilityData
    {
        [SerializeField] private bool _canEverGlide = true;
        [SerializeField] private float _maxFallSpeedGliding = 1.0f;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new GlideAbility(character, _canEverGlide, _maxFallSpeedGliding);
            return ability;
        }
    }
}