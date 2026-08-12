using UnityEngine;

namespace _App
{
    public class FlyAbility : IAbility
    {
        private readonly Character _character;
        private readonly bool _canEverFly;

        public FlyAbility(Character character, bool canEverFly)
        {
            _character = character;
            _canEverFly = canEverFly;
        }

        private bool IsFlyAllowed()
        {
            return _canEverFly && _character.IsFalling();
        }

        protected virtual bool CanFly()
        {
            bool isFlyAllowed = IsFlyAllowed();
            
            if (isFlyAllowed)
            {
                Vector3 worldUp = -_character.GetGravityDirection();
                float verticalSpeed = Vector3.Dot(_character.GetVelocity(), worldUp);

                isFlyAllowed = verticalSpeed < 0.0f;
            }

            return isFlyAllowed;
        }
        
        private void OnCollided(ref CollisionResult collisionResult)
        {
            if (_character.IsFlying() && collisionResult.IsWalkable)
                _character.SetMovementMode(EMovementMode.Falling);
        }
        
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            bool isFlying = _character.IsFlying();
            bool wantsToFly = _character.JumpInputPressed;

            if (!isFlying && wantsToFly && CanFly())
                _character.SetMovementMode(EMovementMode.Flying);
        }

        public void OnEnable()
        {
            _character.Collided += OnCollided;
            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }
        
        public void OnDisable()
        {
            _character.Collided -= OnCollided;
            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }
    }
}
