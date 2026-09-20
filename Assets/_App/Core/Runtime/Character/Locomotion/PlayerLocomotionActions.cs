using System;
using UnityEngine;

namespace _App
{
    public interface IPlayerLocomotionActions
    {
        bool CanDodge { get; }
        bool IsDodgeStarting { get; }
        bool CanAirDash { get; }
        bool IsAirDashStarting { get; }

        void Enable();
        void Disable();
        void Interrupt();
        void StartGliding();
        void StopGliding();
        void SetDodgeContinuationSpeed(float speed);
        bool TryDodge(Vector3 worldDirection, bool backwards);
        bool TryAirDash(Vector3 worldDirection, bool backwards);
        void RestoreAirDashCharges();
    }

    internal sealed class PlayerLocomotionActions : IPlayerLocomotionActions
    {
        private readonly PlayerDodgeAction _dodge;
        private readonly PlayerAirDashAction _airDash;
        private readonly PlayerGlideAction _glide;
        private bool _enabled;

        public bool CanDodge => _dodge?.CanRequest == true;
        public bool IsDodgeStarting => _dodge?.IsStarting == true;
        public bool CanAirDash => _airDash?.CanRequest == true;
        public bool IsAirDashStarting => _airDash?.IsStarting == true;

        public PlayerLocomotionActions(
            Character character,
            ICharacterMotor motor,
            IDamageImmunityTarget damageImmunity,
            CharacterFeatureSet features)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            if (damageImmunity == null)
            {
                throw new ArgumentNullException(nameof(damageImmunity));
            }

            DodgeSettings dodgeSettings = features.GetEnabled<DodgeSettings>();
            if (dodgeSettings != null)
            {
                _dodge = new PlayerDodgeAction(
                    character,
                    motor,
                    damageImmunity,
                    dodgeSettings);
            }

            AirDashSettings airDashSettings = features.GetEnabled<AirDashSettings>();
            if (airDashSettings != null)
            {
                _airDash = new PlayerAirDashAction(
                    character,
                    damageImmunity,
                    airDashSettings);
            }

            GlideSettings glideSettings = features.GetEnabled<GlideSettings>();
            if (glideSettings != null)
            {
                _glide = new PlayerGlideAction(character, glideSettings);
            }
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _dodge?.Enable();
            _airDash?.Enable();
            _glide?.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            Interrupt();
            _glide?.Disable();
            _airDash?.Disable();
            _dodge?.Disable();
            _enabled = false;
        }

        public void Interrupt()
        {
            _glide?.Interrupt();
            _airDash?.Interrupt();
            _dodge?.Interrupt();
        }

        public void StartGliding()
        {
            _glide?.Start();
        }

        public void StopGliding()
        {
            _glide?.Stop();
        }

        public void SetDodgeContinuationSpeed(float speed)
        {
            _dodge?.SetContinuationSpeed(speed);
        }

        public bool TryDodge(Vector3 worldDirection, bool backwards)
        {
            return _dodge?.TryRequest(worldDirection, backwards) == true;
        }

        public bool TryAirDash(Vector3 worldDirection, bool backwards)
        {
            return _airDash?.TryRequest(worldDirection, backwards) == true;
        }

        public void RestoreAirDashCharges()
        {
            _airDash?.RestoreCharges();
        }
    }
}
