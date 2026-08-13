using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerGlideAction
    {
        private readonly Character _character;
        private readonly bool _canEverGlide;
        private readonly float _maxFallSpeed;

        private float _previousMaxFallSpeed;
        private bool _inputHeld;
        private bool _isGliding;
        private bool _enabled;

        public PlayerGlideAction(Character character, GlideSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _canEverGlide = settings.IsEnabled;
            _maxFallSpeed = settings.MaxFallSpeed;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
            _inputHeld = false;
            EndGlide();
            _enabled = false;
        }

        public void Start()
        {
            if (_enabled)
            {
                _inputHeld = true;
            }
        }

        public void Stop()
        {
            _inputHeld = false;
        }

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!_isGliding && _inputHeld && CanGlide())
            {
                BeginGlide();
            }
            else if (_isGliding && (!_inputHeld || !CanGlide()))
            {
                EndGlide();
            }
        }

        private bool CanGlide()
        {
            if (!_enabled || !_canEverGlide || !_character.IsFalling())
            {
                return false;
            }

            Vector3 worldUp = -_character.GetGravityDirection();
            return Vector3.Dot(_character.GetVelocity(), worldUp) < 0.0f;
        }

        private void BeginGlide()
        {
            _previousMaxFallSpeed = _character.MaxFallSpeed;
            _character.MaxFallSpeed = _maxFallSpeed;
            _isGliding = true;
        }

        private void EndGlide()
        {
            if (!_isGliding)
            {
                return;
            }

            _character.MaxFallSpeed = _previousMaxFallSpeed;
            _isGliding = false;
        }
    }
}
