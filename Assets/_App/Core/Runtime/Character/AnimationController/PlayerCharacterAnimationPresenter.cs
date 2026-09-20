using System;
using ModularAnimation;

namespace _App
{
    public sealed class PlayerCharacterAnimationPresenter
    {
        private readonly ICharacterAnimationDriver _driver;

        public PlayerCharacterAnimationPresenter(ICharacterAnimationDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public void Present(in PlayerLocomotionSnapshot snapshot, float deltaTime)
        {
            bool actionOwnsVelocity = ActionOwnsVelocity(snapshot.State);
            float forwardSpeed = actionOwnsVelocity
                ? snapshot.RequestedSpeed
                : snapshot.ForwardSpeed;
            float lateralSpeed = actionOwnsVelocity ? 0.0f : snapshot.LateralSpeed;
            var frame = new CharacterLocomotionFrame(
                forwardSpeed,
                lateralSpeed,
                snapshot.VerticalSpeed,
                snapshot.NormalizedSpeed,
                snapshot.IsGrounded,
                snapshot.IsJumpStarting,
                ResolveGait(snapshot.SpeedMode),
                ResolveMode(snapshot.Mode),
                snapshot.IsLandingRecovering);

            _driver.ApplyLocomotion(frame, deltaTime);
        }

        private static bool ActionOwnsVelocity(PlayerLocomotionStateId state)
        {
            return state == PlayerLocomotionStateId.Dodge ||
                   state == PlayerLocomotionStateId.AirDash;
        }

        private static CharacterGait ResolveGait(PlayerSpeedMode speedMode)
        {
            return speedMode switch
            {
                PlayerSpeedMode.Walk => CharacterGait.Walk,
                PlayerSpeedMode.Sprint => CharacterGait.Sprint,
                _ => CharacterGait.Run
            };
        }

        private static CharacterLocomotionMode ResolveMode(PlayerLocomotionMode mode)
        {
            return mode switch
            {
                PlayerLocomotionMode.Airborne => CharacterLocomotionMode.Airborne,
                PlayerLocomotionMode.Swimming => CharacterLocomotionMode.Swimming,
                PlayerLocomotionMode.Climbing => CharacterLocomotionMode.Climbing,
                PlayerLocomotionMode.Flying => CharacterLocomotionMode.Flying,
                _ => CharacterLocomotionMode.Grounded
            };
        }
    }
}
