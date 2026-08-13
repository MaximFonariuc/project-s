using System;
using UnityEngine;

namespace _App
{
    internal sealed class GravityAlignmentPolicy
    {
        private readonly Character _character;

        public GravityAlignmentPolicy(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
        }

        public void Enable()
        {
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void Disable()
        {
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
        }

        private void OnAfterSimulationUpdated(float deltaTime)
        {
            Vector3 worldUp = -_character.GetGravityDirection();
            Quaternion rotation = _character.GetRotation();
            Quaternion target = Quaternion.FromToRotation(
                _character.GetUpVector(),
                worldUp) * rotation;

            rotation = Quaternion.RotateTowards(
                rotation,
                target,
                _character.RotationRate * deltaTime);

            _character.SetRotation(rotation);
        }
    }
}
