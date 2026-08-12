using UnityEngine;

namespace _App
{
    public class GlideAbility : IAbility
    {
        private readonly bool _canEverGlide;
        private readonly float _maxFallSpeedGliding;
        private readonly Character _character;

        private bool _glideInputPressed;
        private bool _isGliding;

        public GlideAbility(Character character,bool canEverGlide, float maxFallSpeedGliding)
        {
            _character = character;
            _canEverGlide = canEverGlide;
            _maxFallSpeedGliding = maxFallSpeedGliding;
        }
        
        public virtual bool IsGliding()
        {
            return _isGliding;
        }

        public virtual void Glide()
        {
            _glideInputPressed = true;
        }

        public virtual void StopGliding()
        {
            _glideInputPressed = false;
        }
        
        protected virtual bool IsGlideAllowed()
        {
            return _canEverGlide && _character.IsFalling();
        }

        protected virtual bool CanGlide()
        {
            bool isGlideAllowed = IsGlideAllowed();
            
            if (isGlideAllowed)
            {
                Vector3 worldUp = -_character.GetGravityDirection();
                float verticalSpeed = Vector3.Dot(_character.GetVelocity(), worldUp);

                isGlideAllowed = verticalSpeed < 0.0f;
            }

            return isGlideAllowed;
        }
        
        protected virtual void CheckGlideInput()
        {
            if (!_isGliding && _glideInputPressed && CanGlide())
            {
                _isGliding = true;
                _character.MaxFallSpeed = _maxFallSpeedGliding;
            }
            else if (_isGliding && (!_glideInputPressed || !CanGlide()))
            {
                _isGliding = false;
                _character.MaxFallSpeed = 40.0f;
            }
        }
        
        private void OnBeforeCharacterSimulationUpdated(float deltaTime)
        {
            CheckGlideInput();
        }
        
        public void OnEnable()
        {
            _character.BeforeSimulationUpdated += OnBeforeCharacterSimulationUpdated;
        }

        public void OnDisable()
        {
            _character.BeforeSimulationUpdated -= OnBeforeCharacterSimulationUpdated;
        }
    }
}