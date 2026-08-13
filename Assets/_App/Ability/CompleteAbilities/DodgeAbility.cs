using UnityEngine;

namespace _App
{
    public class DodgeAbility : IAbility
    {
        private readonly Character _character;
        private readonly bool _canEverDodge;
        private readonly float _dodgeImpulse;
        private readonly float _dodgeDuration;

        private float _dodgingTime;
        private bool _dodgeInputPressed;

        public bool IsDodging => _character.MovementMode == EMovementMode.Custom && _character.CustomMovement == (int)ECustomMovementMode.Dodging;

        public DodgeAbility(
            Character character,
            bool canEverDodge, 
            float dodgeImpulse,
            float dodgeDuration)
        {
            _character = character;
            _canEverDodge = canEverDodge;
            _dodgeImpulse = dodgeImpulse;
            _dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
        }

        public bool CanDoDodge() => !IsDodging;

        public void Dodge()
        {
            if (!CanDoDodge())
                return;

            _dodgeInputPressed = true;
        }

        public bool IsDodgeAllowed()
        {
            if (_character.IsCrouched())
                return false;

            return _canEverDodge && _character.IsWalking();
        }

        private void DoDodge()
        {
            _character.SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Dodging);
            
            Vector3 dodgeDirection = _character.GetMovementDirection();
            if (dodgeDirection.IsZero())
            {
                dodgeDirection = _character.GetForwardVector();
            }

            Vector3 dodgeDirection2D = Vector3.ProjectOnPlane(dodgeDirection, _character.GetUpVector()).normalized;

            _character.SetVelocity(dodgeDirection2D * _dodgeImpulse);
        }
        
        private void ResetDodgeState()
        {
            _dodgingTime = 0f;
            _dodgeInputPressed = false;
            _character.SetMovementMode(
                _character.IsGrounded() ? EMovementMode.Walking : EMovementMode.Falling);
        }

        private void DodgingMovementMode(float deltaTime)
        {
            _character.SetMovementDirection(Vector3.zero);
            _dodgingTime += deltaTime;

            if (_dodgingTime >= _dodgeDuration)
            {
                ResetDodgeState();
            }
        }

        private void OnBeforeCharacterSimulationUpdated(float deltaTime)
        {
            if (!IsDodging && _dodgeInputPressed && IsDodgeAllowed())
            {
                DoDodge();
            }
        }

        private void OnMovementModeChanged(float deltaTime)
        {
            if (_character.CustomMovement == (int)ECustomMovementMode.Dodging)
            {
                DodgingMovementMode(deltaTime);
            }
        }

        public void OnEnable()
        {
            _character.BeforeSimulationUpdated += OnBeforeCharacterSimulationUpdated;
            _character.CustomMovementModeUpdated += OnMovementModeChanged;
        }

        public void OnDisable()
        {
            _character.BeforeSimulationUpdated -= OnBeforeCharacterSimulationUpdated;
            _character.CustomMovementModeUpdated -= OnMovementModeChanged;
        }
    }
}
