using UnityEngine;

namespace _App
{
    public class GravityDirectionAbility : IAbility
    {
        private readonly Character _character;

        public GravityDirectionAbility(Character character)
        {
            _character = character;
        }

        private void RotateCharacterToGravity(float deltaTime)
        {
            Vector3 worldUp = -1.0f * _character.GetGravityDirection();
            Vector3 characterUp = _character.GetUpVector();
            
            Quaternion characterRotation = _character.GetRotation();
            Quaternion targetRotation = Quaternion.FromToRotation(characterUp, worldUp) * characterRotation;
            
            characterRotation = Quaternion.RotateTowards(characterRotation, targetRotation, _character.RotationRate * deltaTime);

            _character.SetRotation(characterRotation);
        }
        
        private void OnAfterSimulationUpdated(float deltaTime)
        {
            RotateCharacterToGravity(deltaTime);
        }

        public void OnEnable()
        {
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void OnDisable()
        {
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
        }
    }
}