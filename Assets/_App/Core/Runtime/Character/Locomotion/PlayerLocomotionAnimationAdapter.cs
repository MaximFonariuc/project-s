using System;
using ModularAnimation;

namespace _App
{
    public sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly ICharacterAnimationDriver _driver;
        private readonly PlayerCharacterAnimationPresenter _presenter;
        private PlayerLocomotionStateId _previousState;
        private bool _hasPreviousState;

        public PlayerLocomotionAnimationAdapter(
            ICharacterAnimationDriver driver,
            PlayerCharacterAnimationPresenter presenter)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public void Present(in PlayerLocomotionSnapshot snapshot, float deltaTime)
        {
            if (!snapshot.IsValid)
            {
                Reset();
                return;
            }

            if (!_hasPreviousState || _previousState != snapshot.State)
            {
                EnterState(snapshot);
                _previousState = snapshot.State;
                _hasPreviousState = true;
            }

            _presenter.Present(snapshot, deltaTime);
        }

        public void Reset()
        {
            _hasPreviousState = false;
        }

        private void EnterState(in PlayerLocomotionSnapshot snapshot)
        {
            if (snapshot.State != PlayerLocomotionStateId.Dodge)
            {
                return;
            }

            _driver.CrossFade(
                snapshot.DodgeBackwards ? PlayerAnimationStates.RollBack : PlayerAnimationStates.RollFront,
                PlayerAnimationStates.BaseLayer,
                0.1f);
        }
    }
}
