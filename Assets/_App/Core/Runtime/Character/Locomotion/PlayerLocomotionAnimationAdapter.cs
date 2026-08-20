using System;

namespace _App
{
    internal sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly PlayerCharacterAnimationPresenter _presenter;

        private bool _wasDodging;

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

            _presenter.Present(snapshot, deltaTime);

            bool isDodging = snapshot.State == PlayerLocomotionStateId.Dodge;
            if (isDodging && !_wasDodging)
            {
                _animation.CrossFade(
                    PlayerAnimationSource.Locomotion,
                    snapshot.DodgeBackwards
                        ? PlayerAnimationSlots.DodgeBackward
                        : PlayerAnimationSlots.DodgeForward);
            }
            else if (!isDodging && _wasDodging)
            {
                _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            }

            _wasDodging = isDodging;
        }

        public void Reset()
        {
            _animation.Cancel(PlayerAnimationSource.Locomotion);
            _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            _wasDodging = false;
        }
    }
}
