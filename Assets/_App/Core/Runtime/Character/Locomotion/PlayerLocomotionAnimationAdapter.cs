using System;

namespace _App
{
    internal sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly PlayerCharacterAnimationPresenter _presenter;
        private PlayerLocomotionStateId _previousState;
        private bool _hasPreviousState;

        public PlayerLocomotionAnimationAdapter(
            PlayerAnimationCoordinator animation,
            PlayerCharacterAnimationPresenter presenter)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
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
                if (_hasPreviousState &&
                    _previousState == PlayerLocomotionStateId.Dodge &&
                    snapshot.State != PlayerLocomotionStateId.Dodge)
                {
                    _animation.Release(
                        PlayerAnimationSource.Locomotion,
                        PlayerAnimationStates.BaseLayer);
                }

                EnterState(snapshot);
                _previousState = snapshot.State;
                _hasPreviousState = true;
            }

            _presenter.Present(snapshot, deltaTime);
        }

        public void Reset()
        {
            _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            _hasPreviousState = false;
        }

        private void EnterState(in PlayerLocomotionSnapshot snapshot)
        {
            if (snapshot.State != PlayerLocomotionStateId.Dodge)
            {
                return;
            }

            _animation.CrossFade(
                PlayerAnimationSource.Locomotion,
                snapshot.DodgeBackwards ? PlayerAnimationStates.RollBack : PlayerAnimationStates.RollFront,
                PlayerAnimationStates.BaseLayer,
                0.1f);
        }
    }
}
