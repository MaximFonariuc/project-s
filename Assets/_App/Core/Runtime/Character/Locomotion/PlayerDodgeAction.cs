using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerDodgeAction
    {
        private readonly Character _character;
        private readonly ICharacterMotor _motor;
        private readonly bool _canEverDodge;
        private readonly float _impulse;
        private readonly float _duration;

        private float _elapsed;
        private bool _requestPending;
        private bool _enabled;

        public PlayerDodgeAction(
            Character character,
            ICharacterMotor motor,
            DodgeSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _canEverDodge = settings.IsEnabled;
            _impulse = settings.Impulse;
            _duration = settings.Duration;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
            _character.CustomMovementModeUpdated += OnCustomMovementModeUpdated;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
            _character.CustomMovementModeUpdated -= OnCustomMovementModeUpdated;
            _requestPending = false;

            if (_character.IsDodging())
            {
                ExitDodge();
            }
            else
            {
                _elapsed = 0.0f;
            }

            _enabled = false;
        }

        public bool TryRequest(Vector3 worldDirection, bool backwards)
        {
            if (!_enabled || _requestPending || !IsAllowed())
            {
                return false;
            }

            Vector3 clampedDirection = Vector3.ClampMagnitude(worldDirection, 1.0f);
            _motor.SetMovementDirection(clampedDirection);

            if (!backwards)
            {
                _motor.SnapFacingDirection(clampedDirection);
            }

            _requestPending = true;
            return true;
        }

        private bool IsAllowed()
        {
            return _canEverDodge &&
                   !_character.IsCrouched() &&
                   !_character.IsDodging() &&
                   _character.IsWalking();
        }

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!_requestPending)
            {
                return;
            }

            _requestPending = false;
            if (IsAllowed())
            {
                EnterDodge();
            }
        }

        private void EnterDodge()
        {
            _elapsed = 0.0f;
            _character.SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Dodging);

            Vector3 dodgeDirection = _character.GetMovementDirection();
            if (dodgeDirection.IsZero())
            {
                dodgeDirection = _character.GetForwardVector();
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(
                dodgeDirection,
                _character.GetUpVector()).normalized;

            _character.SetVelocity(planarDirection * _impulse);
        }

        private void OnCustomMovementModeUpdated(float deltaTime)
        {
            if (!_character.IsDodging())
            {
                return;
            }

            _character.SetMovementDirection(Vector3.zero);
            _elapsed += deltaTime;

            if (_elapsed >= _duration)
            {
                ExitDodge();
            }
        }

        private void ExitDodge()
        {
            _elapsed = 0.0f;
            _requestPending = false;
            _character.SetMovementMode(
                _character.IsGrounded() ? EMovementMode.Walking : EMovementMode.Falling);
        }
    }
}
