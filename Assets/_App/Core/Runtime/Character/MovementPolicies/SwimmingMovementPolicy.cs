using System;
using UnityEngine;

namespace _App
{
    internal sealed class SwimmingMovementPolicy
    {
        private readonly Character _character;
        private readonly bool _isEnabled;

        public SwimmingMovementPolicy(
            Character character,
            SwimmingSettings settings)
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
            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }

        public void Disable()
        {
            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!_isEnabled || !_character.IsSwimming())
            {
                return;
            }

            if (_character.JumpInputPressed)
            {
                MoveVertically(1.0f);
            }
            else if (_character.CrouchInputPressed)
            {
                MoveVertically(-1.0f);
            }
        }

        private void MoveVertically(float direction)
        {
            float depth = _character.CalcImmersionDepth();
            if (depth > 0.65f)
            {
                Vector3 movement = _character.GetMovementDirection();
                movement += _character.GetUpVector() * direction;
                _character.SetMovementDirection(movement);
                return;
            }

            if (direction > 0.0f)
            {
                _character.SetMovementMode(EMovementMode.Falling);
                _character.LaunchCharacter(_character.GetUpVector() * 9.0f, true);
            }
        }
    }

}
