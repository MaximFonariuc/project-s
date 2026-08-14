using System;

namespace _App
{
    internal sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly PlayerCharacterAnimationPresenter _presenter;
        private readonly PlayerLocomotionAnimationResolver _resolver;

        public PlayerLocomotionAnimationAdapter(
            PlayerAnimationCoordinator animation,
            PlayerCharacterAnimationPresenter presenter,
            PlayerLocomotionAnimationResolver resolver)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public void Present(in PlayerLocomotionSnapshot snapshot, float deltaTime)
        {
            if (!snapshot.IsValid)
            {
                Reset();
                return;
            }

            _presenter.Present(snapshot, deltaTime);
            if (_resolver.TryResolve(snapshot, deltaTime, out PlayerLocomotionAnimationStateId state))
            {
                _animation.CrossFade(PlayerAnimationSource.Locomotion, ResolveSlot(state));
            }
        }

        public void Reset()
        {
            _animation.ReleaseAll(PlayerAnimationSource.Locomotion);
            _resolver.Reset();
        }

        private static ModularAnimation.AnimationSlotId ResolveSlot(
            PlayerLocomotionAnimationStateId state)
        {
            return state switch
            {
                PlayerLocomotionAnimationStateId.Grounded => PlayerAnimationSlots.Grounded,
                PlayerLocomotionAnimationStateId.JumpWalk => PlayerAnimationSlots.JumpWalk,
                PlayerLocomotionAnimationStateId.JumpRun => PlayerAnimationSlots.JumpRun,
                PlayerLocomotionAnimationStateId.JumpSprint => PlayerAnimationSlots.JumpSprint,
                PlayerLocomotionAnimationStateId.Fall => PlayerAnimationSlots.Fall,
                PlayerLocomotionAnimationStateId.LandSoft => PlayerAnimationSlots.LandSoft,
                PlayerLocomotionAnimationStateId.LandMedium => PlayerAnimationSlots.LandMedium,
                PlayerLocomotionAnimationStateId.LandHard => PlayerAnimationSlots.LandHard,
                PlayerLocomotionAnimationStateId.DodgeForward => PlayerAnimationSlots.DodgeForward,
                PlayerLocomotionAnimationStateId.DodgeBackward => PlayerAnimationSlots.DodgeBackward,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }
}
