using System;
using ModularAnimation;

namespace _App
{
    internal sealed class CharacterReactionAnimationAdapter : ICharacterReactionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;

        public CharacterReactionAnimationAdapter(PlayerAnimationCoordinator animation)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Present(in CharacterReactionSnapshot snapshot)
        {
            if (!snapshot.IsValid || snapshot.State == CharacterReactionStateId.Stable)
            {
                return;
            }

            PlayerAnimationSource source = snapshot.State == CharacterReactionStateId.Dead
                ? PlayerAnimationSource.Death
                : PlayerAnimationSource.Reaction;
            AnimationSlotId slot = snapshot.State == CharacterReactionStateId.HitStun
                ? PlayerAnimationSlots.Reaction(
                    snapshot.Hit.ReactionId,
                    snapshot.State,
                    snapshot.Direction)
                : PlayerAnimationSlots.Reaction(snapshot.Hit.ReactionId, snapshot.State);

            _animation.CrossFade(source, slot, snapshot.RemainingDuration);
        }

        public void Reset()
        {
            _animation.Cancel(PlayerAnimationSource.Reaction);
            _animation.Cancel(PlayerAnimationSource.Death);
            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
            _animation.ReleaseAll(PlayerAnimationSource.Death);
        }
    }
}
