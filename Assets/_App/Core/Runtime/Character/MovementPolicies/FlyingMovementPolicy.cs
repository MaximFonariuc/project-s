using System;
using UnityEngine;

namespace _App
{
    internal sealed class FlyingMovementPolicy
    {
        private readonly Character _character;
        private readonly bool _isEnabled;

        public FlyingMovementPolicy(Character character, FlyingSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _isEnabled = settings.IsEnabled;
        }

        public void Enable()
        {
            _character.Collided += OnCollided;
            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }

        public void Disable()
        {
            _character.Collided -= OnCollided;
            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (_character.IsFlying() || !_character.JumpInputPressed || !CanFly())
            {
                return;
            }

            _character.SetMovementMode(EMovementMode.Flying);
        }

        private bool CanFly()
        {
            if (!_isEnabled || !_character.IsFalling())
            {
                return false;
            }

            Vector3 worldUp = -_character.GetGravityDirection();
            return Vector3.Dot(_character.GetVelocity(), worldUp) < 0.0f;
        }

        private void OnCollided(ref CollisionResult collisionResult)
        {
            if (_character.IsFlying() && collisionResult.IsWalkable)
            {
                _character.SetMovementMode(EMovementMode.Falling);
            }
        }
    }
}
