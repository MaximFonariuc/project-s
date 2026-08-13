using System;
using UnityEngine;

namespace _App
{
    public interface IPlayerLocomotionActions
    {
        void Enable();
        void Disable();
        void StartGliding();
        void StopGliding();
        bool TryDodge(Vector3 worldDirection, bool backwards);
    }

    internal sealed class PlayerLocomotionActions : IPlayerLocomotionActions
    {
        private readonly PlayerDodgeAction _dodge;
        private readonly PlayerGlideAction _glide;
        private bool _enabled;

        public PlayerLocomotionActions(
            Character character,
            ICharacterMotor motor,
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

            DodgeSettings dodgeSettings = features.GetEnabled<DodgeSettings>();
            if (dodgeSettings != null)
            {
                _dodge = new PlayerDodgeAction(character, motor, dodgeSettings);
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
            _glide?.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _glide?.Disable();
            _dodge?.Disable();
            _enabled = false;
        }

        public void StartGliding()
        {
            _glide?.Start();
        }

        public void StopGliding()
        {
            _glide?.Stop();
        }

        public bool TryDodge(Vector3 worldDirection, bool backwards)
        {
            return _dodge?.TryRequest(worldDirection, backwards) == true;
        }
    }
}
