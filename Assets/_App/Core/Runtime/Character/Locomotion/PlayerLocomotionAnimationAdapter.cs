using System;

namespace _App
{
    public sealed class PlayerLocomotionAnimationAdapter : IPlayerLocomotionPresentation
    {
        private readonly AnimationController _animation;

        public PlayerLocomotionAnimationAdapter(AnimationController animation)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void EnterState(PlayerLocomotionStateId stateId, bool dodgeBackwards)
        {
            if (stateId != PlayerLocomotionStateId.Dodge)
            {
                return;
            }

            _animation.CrossFade(
                dodgeBackwards ? AnimationHashes.RollBack : AnimationHashes.RollFront,
                0.1f,
                AnimationController.BASE_LAYER_INDEX);
        }

        public void SetSpeedMode(PlayerSpeedMode speedMode)
        {
            _animation.SetMovementMode(speedMode switch
            {
                PlayerSpeedMode.Walk => EAnimationMoveMode.Walking,
                PlayerSpeedMode.Sprint => EAnimationMoveMode.Sprinting,
                _ => EAnimationMoveMode.Running
            });
        }

        public void Tick()
        {
            _animation.Update();
        }
    }
}
