using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerDodgeAction
    {
        private readonly Character _character;
        private readonly ICharacterMotor _motor;
        private readonly IDamageImmunityTarget _damageImmunity;
        private readonly DodgeSettings _settings;
        private readonly bool _canEverDodge;
        private readonly bool _hasInvulnerability;
        private readonly float _impulse;
        private readonly float _duration;
        private readonly float _invulnerabilityStart;
        private readonly float _invulnerabilityEnd;

        private IDisposable _damageImmunityLease;
        private Vector3 _dodgeDirection;
        private float _continuationSpeed;
        private float _elapsed;
        private bool _requestedBackwards;
        private bool _requestPending;
        private bool _exitPending;
        private bool _enabled;

        public bool CanRequest => _enabled && !_requestPending && CanEnterDodge;
        public bool IsStarting => _requestPending;

        public PlayerDodgeAction(
            Character character,
            ICharacterMotor motor,
            IDamageImmunityTarget damageImmunity,
            DodgeSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _damageImmunity = damageImmunity ??
                throw new ArgumentNullException(nameof(damageImmunity));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings;
            _canEverDodge = settings.IsEnabled;
            _impulse = settings.Impulse;
            _duration = settings.Duration;
            _hasInvulnerability = settings.HasInvulnerability;
            _invulnerabilityStart = settings.InvulnerabilityStart;
            _invulnerabilityEnd = settings.InvulnerabilityEnd;
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
            Interrupt();
            _enabled = false;
        }

        public void Interrupt()
        {
            _requestPending = false;
            _exitPending = false;
            ReleaseDamageImmunity();

            if (_character.IsDodging())
            {
                ExitDodge();
                return;
            }

            _elapsed = 0.0f;
            _requestedBackwards = false;
        }

        public void SetContinuationSpeed(float speed)
        {
            _continuationSpeed = Mathf.Max(0.0f, speed);
        }

        public bool TryRequest(Vector3 worldDirection, bool backwards)
        {
            if (!CanRequest)
            {
                return false;
            }

            Vector3 clampedDirection = Vector3.ClampMagnitude(worldDirection, 1.0f);
            _motor.SetMovementDirection(clampedDirection);

            if (!backwards)
            {
                _motor.SnapFacingDirection(clampedDirection);
            }

            _requestedBackwards = backwards;
            _requestPending = true;
            return true;
        }

        private bool CanEnterDodge =>
            _canEverDodge &&
            !_character.IsCrouched() &&
            !_character.IsDodging() &&
            _character.IsWalking();

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!_character.IsDodging())
            {
                ReleaseDamageImmunity();
            }

            if (_exitPending && _character.IsDodging())
            {
                ExitDodge();
                return;
            }

            if (!_requestPending)
            {
                return;
            }

            _requestPending = false;
            if (CanEnterDodge)
            {
                EnterDodge();
            }
        }

        private void EnterDodge()
        {
            _elapsed = 0.0f;
            ReleaseDamageImmunity();
            _character.SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Dodging);

            Vector3 dodgeDirection = _character.GetMovementDirection();
            if (dodgeDirection.IsZero())
            {
                dodgeDirection = _character.GetForwardVector();
            }

            _dodgeDirection = Vector3.ProjectOnPlane(
                dodgeDirection,
                _character.GetUpVector()).normalized;
            _character.SetVelocity(_dodgeDirection * ResolveSpeed(0.0f));
            UpdateDamageImmunity();
        }

        private float ResolveSpeed(float normalizedTime)
        {
            return Mathf.Max(
                _impulse * _settings.SampleNormalizedSpeed(normalizedTime),
                _continuationSpeed);
        }

        private void OnCustomMovementModeUpdated(float deltaTime)
        {
            if (!_character.IsDodging())
            {
                return;
            }

            _character.SetMovementDirection(Vector3.zero);
            _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
            _character.SetVelocity(_dodgeDirection * ResolveSpeed(_elapsed / _duration));
            UpdateDamageImmunity();

            if (_elapsed >= _duration)
            {
                _exitPending = true;
            }
        }

        private void ExitDodge()
        {
            ReleaseDamageImmunity();
            Vector3 up = _character.GetUpVector();
            Vector3 velocity = _character.GetVelocity();
            Vector3 verticalVelocity = Vector3.Project(velocity, up);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
            _character.SetVelocity(
                verticalVelocity +
                Vector3.ClampMagnitude(planarVelocity, _continuationSpeed));
            _elapsed = 0.0f;
            _dodgeDirection = Vector3.zero;
            _continuationSpeed = 0.0f;
            _requestedBackwards = false;
            _requestPending = false;
            _exitPending = false;
            _character.SetMovementMode(
                _character.IsGrounded() ? EMovementMode.Walking : EMovementMode.Falling);
        }

        private void UpdateDamageImmunity()
        {
            float normalizedTime = Mathf.Clamp01(_elapsed / _duration);
            bool shouldBeImmune = _hasInvulnerability &&
                                  normalizedTime >= _invulnerabilityStart &&
                                  normalizedTime < _invulnerabilityEnd;
            if (shouldBeImmune)
            {
                _damageImmunityLease ??= _damageImmunity.AcquireDamageImmunity();
                return;
            }

            ReleaseDamageImmunity();
        }

        private void ReleaseDamageImmunity()
        {
            _damageImmunityLease?.Dispose();
            _damageImmunityLease = null;
        }

    }
}
