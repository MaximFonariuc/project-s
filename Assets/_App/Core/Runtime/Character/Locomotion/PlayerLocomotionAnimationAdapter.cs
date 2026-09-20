using System;
using ModularAnimation;

namespace _App
{
    internal sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly PlayerCharacterAnimationPresenter _presenter;
        private readonly float _dodgeDuration;
        private readonly float _airDashDuration;

        private bool _wasDodging;
        private bool _wasAirDashing;

        public PlayerLocomotionAnimationAdapter(
            PlayerAnimationCoordinator animation,
            PlayerCharacterAnimationPresenter presenter,
            PlayerLocomotionSettings settings)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _dodgeDuration = settings?.DodgeDuration ??
                throw new ArgumentNullException(nameof(settings));
            _airDashDuration = settings.AirDashDuration;
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
                    ResolveDodgeSlot(snapshot.DodgeDirection),
                    _dodgeDuration);
            }
            else if (!isDodging && _wasDodging)
            {
                _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            }

            _wasDodging = isDodging;

            bool isAirDashing = snapshot.State == PlayerLocomotionStateId.AirDash;
            if (isAirDashing && !_wasAirDashing && _airDashDuration > 0.0f)
            {
                _animation.CrossFade(
                    PlayerAnimationSource.Locomotion,
                    ResolveAirDashSlot(snapshot.DodgeDirection),
                    _airDashDuration);
            }
            else if (!isAirDashing && _wasAirDashing)
            {
                _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            }

            _wasAirDashing = isAirDashing;
        }

        private static AnimationSlotId ResolveDodgeSlot(PlayerDodgeDirection direction)
        {
            return direction == PlayerDodgeDirection.Backward
                ? PlayerAnimationSlots.DodgeBackward
                : PlayerAnimationSlots.DodgeForward;
        }

        private static AnimationSlotId ResolveAirDashSlot(PlayerDodgeDirection direction)
        {
            return direction switch
            {
                PlayerDodgeDirection.Backward => PlayerAnimationSlots.AirDashBackward,
                PlayerDodgeDirection.Left => PlayerAnimationSlots.AirDashLeft,
                PlayerDodgeDirection.Right => PlayerAnimationSlots.AirDashRight,
                _ => PlayerAnimationSlots.AirDashForward
            };
        }

        public void Reset()
        {
            _animation.Cancel(PlayerAnimationSource.Locomotion);
            _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            _wasDodging = false;
            _wasAirDashing = false;
        }
    }
}
