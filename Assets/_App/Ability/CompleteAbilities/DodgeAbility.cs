using UnityEngine;

namespace _App
{
    public class DodgeAbility : IAbility
    {
        private readonly Character _character;
        private readonly AnimationController _animationController;
        private readonly bool _canEverDodge;
        private readonly float _dodgeImpulse;

        private float _dodgingTime;
        private bool _dodgeInputPressed;

        private int _currentDodgeAnimHash;

        public bool IsDodging => _character.MovementMode == EMovementMode.Custom && _character.CustomMovement == (int)ECustomMovementMode.Dodging;

        public DodgeAbility(Character character, 
            AnimationController animationController, 
            bool canEverDodge, 
            float dodgeImpulse)
        {
            _character = character;
            _animationController = animationController;
            _canEverDodge = canEverDodge;
            _dodgeImpulse = dodgeImpulse;
        }

        public bool CanDoDodge() => !IsDodging;

        public void Dodge(int dodgeAnimationHash)
        {
            if (!CanDoDodge())
                return;

            _dodgeInputPressed = true;

            _currentDodgeAnimHash = dodgeAnimationHash;
        }

        public bool IsDodgeAllowed()
        {
            if (_character.IsCrouched())
                return false;

            return _canEverDodge && (_character.IsWalking() || !_character.IsFalling());
        }

        private void DoDodge()
        {
            _character.SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Dodging);
            
            Vector3 dodgeDirection = _character.GetMovementDirection();
            if (dodgeDirection.IsZero())
            {
                dodgeDirection = _character.GetForwardVector();
            }

            Vector3 dodgeDirection2D = dodgeDirection.OnlyXZ().normalized;

            _character.SetVelocity(dodgeDirection2D * _dodgeImpulse);

            _animationController?.CrossFade(_currentDodgeAnimHash, 0.1f, AnimationController.BASE_LAYER_INDEX);
        }
        
        private void ResetDodgeState()
        {
            _dodgingTime = 0f;
            _dodgeInputPressed = false;
            _currentDodgeAnimHash = 0;
            _character.SetMovementMode(EMovementMode.Falling);
        }

        private void DodgingMovementMode(float deltaTime)
        {
            _character.SetMovementDirection(Vector3.zero);
            _dodgingTime += deltaTime;

            float dodgeAnimLength = _animationController.GetAnimationClipLength(_currentDodgeAnimHash);

            if (_dodgingTime >= dodgeAnimLength * 0.5f)
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
