using UnityEngine;

namespace _App
{
    public class LandingForceAbility : IAbility
    {
        private readonly Character _character;
        private readonly float _landingForceScale;

        public LandingForceAbility(Character character,float landingForceScale)
        {
            _character = character;
            _landingForceScale = landingForceScale;
        }

        public void OnEnable()
        {
            _character.Landed += OnLanded;
        }

        public void OnDisable()
        {
            _character.Landed -= OnLanded;
        }

        private void OnLanded(Vector3 landingVelocity)
        {
            Rigidbody groundRigidbody = _character.CharacterMovement.GroundRigidbody;
            if (!groundRigidbody)
                return;
            
            Vector3 force = _character.GetGravityVector() *
                            (_character.Mass * landingVelocity.magnitude * _landingForceScale);
                
            groundRigidbody.AddForceAtPosition(force, _character.Position);
        }

        
    }
}