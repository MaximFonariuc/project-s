using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerAirDashAction
    {
        private readonly Character _character;
        private readonly ICharacterMotor _motor;
        private readonly IDamageImmunityTarget _damageImmunity;
        private readonly AirDashSettings _settings;
        private readonly bool _canEverAirDash;
        private readonly bool _hasInvulnerability;
        private readonly float _impulse;
        private readonly float _duration;
        private readonly float _invulnerabilityStart;
        private readonly float _invulnerabilityEnd;
        private readonly int _chargesPerAirtime;

        private IDisposable _damageImmunityLease;
        private Vector3 _dashDirection;
        private float _continuationSpeed;
        private float _elapsed;
        private int _chargesLeft;
        private bool _requestPending;
        private bool _exitPending;
        private bool _enabled;

        public bool CanRequest => _enabled && !_requestPending && CanEnterAirDash;
        public bool IsStarting => _requestPending;

        public PlayerAirDashAction(
            Character character,
            ICharacterMotor motor,
            IDamageImmunityTarget damageImmunity,
            AirDashSettings settings)
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
            _canEverAirDash = settings.IsEnabled;
            _impulse = settings.Impulse;
            _duration = settings.Duration;
            _chargesPerAirtime = settings.ChargesPerAirtime;
            _hasInvulnerability = settings.HasInvulnerability;
            _invulnerabilityStart = settings.InvulnerabilityStart;
            _invulnerabilityEnd = settings.InvulnerabilityEnd;
            _chargesLeft = _chargesPerAirtime;
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

            if (_character.IsAirDashing())
            {
                ExitAirDash();
                return;
            }

            _elapsed = 0.0f;
        }

        public void SetContinuationSpeed(float speed)
        {
            _continuationSpeed = Mathf.Max(0.0f, speed);
        }

        public void RestoreCharges()
        {
            _chargesLeft = _chargesPerAirtime;
        }

        public bool TryRequest(Vector3 worldDirection, PlayerDodgeDirection direction)
        {
            if (!CanRequest)
            {
                return false;
            }

            Vector3 clampedDirection = Vector3.ClampMagnitude(worldDirection, 1.0f);
            _character.SetMovementDirection(clampedDirection);

            if (direction == PlayerDodgeDirection.Forward &&
                !clampedDirection.IsZero())
            {
                _motor.SnapFacingDirection(clampedDirection);
            }

            _requestPending = true;
            return true;
        }

        private bool CanEnterAirDash =>
            _canEverAirDash &&
            _chargesLeft > 0 &&
            !_character.IsAirDashing() &&
            _character.IsFalling();

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (!_character.IsAirDashing())
            {
                ReleaseDamageImmunity();
            }

            if (_exitPending && _character.IsAirDashing())
            {
                ExitAirDash();
                return;
            }

            if (!_requestPending)
            {
                return;
            }

            _requestPending = false;
            if (CanEnterAirDash)
            {
                EnterAirDash();
            }
        }

        private void EnterAirDash()
        {
            _elapsed = 0.0f;
            _chargesLeft--;
            ReleaseDamageImmunity();
            _character.SetMovementMode(
                EMovementMode.Custom,
                (int)ECustomMovementMode.AirDashing);

            Vector3 dashDirection = _character.GetMovementDirection();
            if (dashDirection.IsZero())
            {
                dashDirection = _character.GetForwardVector();
            }

            _dashDirection = Vector3.ProjectOnPlane(
                dashDirection,
                _character.GetUpVector()).normalized;
            _character.SetVelocity(_dashDirection * ResolveSpeed(0.0f));
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
            if (!_character.IsAirDashing())
            {
                return;
            }

            _character.SetMovementDirection(Vector3.zero);
            _elapsed = Mathf.Min(_elapsed + deltaTime, _duration);
            _character.SetVelocity(_dashDirection * ResolveSpeed(_elapsed / _duration));
            UpdateDamageImmunity();

            if (_elapsed >= _duration)
            {
                _exitPending = true;
            }
        }

        private void ExitAirDash()
        {
            ReleaseDamageImmunity();

            Vector3 up = _character.GetUpVector();
            Vector3 planarVelocity = Vector3.ProjectOnPlane(_character.GetVelocity(), up);
            _character.SetVelocity(
                Vector3.ClampMagnitude(planarVelocity, ResolveSpeed(1.0f)));
            _elapsed = 0.0f;
            _dashDirection = Vector3.zero;
            _continuationSpeed = 0.0f;
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