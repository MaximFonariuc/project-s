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
            var frame = new CharacterLocomotionFrame(
                snapshot.ForwardSpeed,
                snapshot.LateralSpeed,
                snapshot.VerticalSpeed,
                snapshot.NormalizedSpeed,
                snapshot.IsGrounded,
                ResolveGait(snapshot.SpeedMode),
                ResolveMode(snapshot.Mode));

            _driver.ApplyLocomotion(frame, deltaTime);
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
