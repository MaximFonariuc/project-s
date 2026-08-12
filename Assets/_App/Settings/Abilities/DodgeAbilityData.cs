using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeAbilityData", menuName = "Abilities/Dodge")]
    public class DodgeAbilityData : AbilityData
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new DodgeAbility(character, animationController, _canEverDodge, _dodgeImpulse);
            return ability;
        }
    }
}