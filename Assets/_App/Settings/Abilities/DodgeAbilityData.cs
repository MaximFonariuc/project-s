using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeAbilityData", menuName = "Abilities/Dodge")]
    public class DodgeAbilityData : AbilityData
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;

        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new DodgeAbility(character, _canEverDodge, _dodgeImpulse, _dodgeDuration);
            return ability;
        }
    }
}
