using System;
using ModularAnimation;

namespace _App
{
    public sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly ICharacterAnimationDriver _driver;
        private readonly PlayerCharacterAnimationPresenter _presenter;

        public PlayerLocomotionAnimationAdapter(
            ICharacterAnimationDriver driver,
            PlayerCharacterAnimationPresenter presenter)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public void EnterState(PlayerLocomotionStateId stateId, bool dodgeBackwards)
        {
            if (stateId != PlayerLocomotionStateId.Dodge)
            {
                return;
            }

            _driver.CrossFade(
                dodgeBackwards ? PlayerAnimationStates.RollBack : PlayerAnimationStates.RollFront,
                PlayerAnimationStates.BaseLayer,
                0.1f);
        }

        public void SetSpeedMode(PlayerSpeedMode speedMode)
        {
            _presenter.SetGait(speedMode switch
            {
                PlayerSpeedMode.Walk => CharacterGait.Walk,
                PlayerSpeedMode.Sprint => CharacterGait.Sprint,
                _ => CharacterGait.Run
            });
        }

        public void Tick(float deltaTime)
        {
            _presenter.Tick(deltaTime);
        }
    }
}
