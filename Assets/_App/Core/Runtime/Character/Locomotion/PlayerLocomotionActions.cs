using System;
using UnityEngine;

namespace _App
{
    public interface IPlayerLocomotionActions
    {
        void StartGliding();
        void StopGliding();
        bool TryDodge(Vector3 worldDirection, bool backwards);
    }

    internal sealed class PlayerLocomotionActionAdapter : IPlayerLocomotionActions
    {
        private const float DirectionThresholdSqr = 0.0001f;

        private readonly ICharacterMotor _motor;
        private readonly DodgeAbility _dodge;
        private readonly GlideAbility _glide;

        public PlayerLocomotionActionAdapter(
            ICharacterMotor motor,
            CharacterAbilityRuntime abilities)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));

            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            _dodge = abilities.Get<DodgeAbility>();
            _glide = abilities.Get<GlideAbility>();
        }

        public void StartGliding()
        {
            _glide?.Glide();
        }

        public void StopGliding()
        {
            _glide?.StopGliding();
        }

        public bool TryDodge(Vector3 worldDirection, bool backwards)
        {
            if (_dodge == null || !_dodge.IsDodgeAllowed())
            {
                return false;
            }

            Vector3 clampedDirection = Vector3.ClampMagnitude(worldDirection, 1.0f);
            _motor.SetMovementDirection(clampedDirection);

            if (!backwards && clampedDirection.sqrMagnitude > DirectionThresholdSqr)
            {
                _motor.SnapFacingDirection(clampedDirection);
            }

            _dodge.Dodge();
            return true;
        }
    }
}
