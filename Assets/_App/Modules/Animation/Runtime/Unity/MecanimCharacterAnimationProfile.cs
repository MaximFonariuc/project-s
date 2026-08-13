using UnityEngine;

namespace ModularAnimation.Unity
{
    [CreateAssetMenu(
        fileName = "MecanimCharacterAnimationProfile",
        menuName = "Modular Animation/Mecanim Character Profile")]
    public sealed class MecanimCharacterAnimationProfile : ScriptableObject
    {
        [SerializeField] private string _movementType = "MovementType";
        [SerializeField] private string _movementDirection = "MovementDirection";
        [SerializeField] private string _onGround = "OnGround";
        [SerializeField] private string _fallingDuration = "FallingDuration";
        [SerializeField] private string _speedMultiplier = "SpeedMultiplier";

        public MecanimCharacterAnimationBindings CreateBindings()
        {
            return new MecanimCharacterAnimationBindings(
                _movementType,
                _movementDirection,
                _onGround,
                _fallingDuration,
                _speedMultiplier);
        }
    }
}
