using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "OrientToGroundAbilityData", menuName = "Abilities/OrientToGround")]
    public class OrientToGroundAbilityData : AbilityData
    {
        [SerializeField] private float _maxSlopeAngle = 30.0f;
        [SerializeField] private float _alignRate = 10.0f;
        [SerializeField] private float _rayOffset = 0.1f;
        [SerializeField] private LayerMask _groundMask = 1;

        [Space(15f)]
        [SerializeField] private bool _drawRays = true;
        
        public override IAbility CreateAbilityInstance(Character character, AnimationController animationController)
        {
            var ability = new OrientToGroundAbility(character, _maxSlopeAngle, _alignRate, _rayOffset, _groundMask, _drawRays);
            return ability;
        }
    }
}