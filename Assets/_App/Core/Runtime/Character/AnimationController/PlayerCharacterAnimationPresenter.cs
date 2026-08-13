using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCharacterAnimationPresenter
    {
        private readonly Character _character;
        private readonly ICharacterAnimationDriver _driver;

        private CharacterGait _gait = CharacterGait.Run;

        public CharacterGait Gait => _gait;

        public PlayerCharacterAnimationPresenter(
            Character character,
            ICharacterAnimationDriver driver)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public void SetGait(CharacterGait gait)
        {
            _gait = gait;
        }

        public void Tick(float deltaTime)
        {
            Vector3 up = _character.GetUpVector();
            Vector3 velocity = _character.GetVelocity();
            Vector3 localVelocity = _character.transform.InverseTransformDirection(velocity);
            float maxSpeed = Mathf.Max(_character.GetMaxSpeed(), 0.0001f);

            var frame = new CharacterLocomotionFrame(
                localVelocity.z,
                localVelocity.x,
                Vector3.Dot(velocity, up),
                Mathf.Clamp01(Vector3.ProjectOnPlane(velocity, up).magnitude / maxSpeed),
                _character.IsGrounded(),
                _gait,
                ResolveMode());

            _driver.ApplyLocomotion(frame, deltaTime);
        }

        private CharacterLocomotionMode ResolveMode()
        {
            if (_character.IsClimbing())
            {
                return CharacterLocomotionMode.Climbing;
            }

            if (_character.IsSwimming())
            {
                return CharacterLocomotionMode.Swimming;
            }

            if (_character.IsFlying())
            {
                return CharacterLocomotionMode.Flying;
            }

            return _character.IsFalling()
                ? CharacterLocomotionMode.Airborne
                : CharacterLocomotionMode.Grounded;
        }
    }
}
