using System;
using UnityEngine;

namespace _App
{
    internal sealed class LandingForcePolicy
    {
        private readonly Character _character;
        private readonly float _scale;

        public LandingForcePolicy(Character character, LandingForceSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _scale = settings.Scale;
        }

        public void Enable()
        {
            _character.Landed += OnLanded;
        }

        public void Disable()
        {
            _character.Landed -= OnLanded;
        }

        private void OnLanded(Vector3 landingVelocity)
        {
            Rigidbody ground = _character.CharacterMovement.GroundRigidbody;
            if (ground == null)
            {
                return;
            }

            Vector3 force = _character.GetGravityVector() *
                            (_character.Mass * landingVelocity.magnitude * _scale);

            ground.AddForceAtPosition(force, _character.Position);
        }
    }
}
