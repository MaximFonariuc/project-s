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

            AnimationPlayback playback = AnimationPlayback.Invalid;
            if (snapshot.Hit.ReactionId.IsValid)
            {
                playback = _animation.CrossFade(
                    PlayerAnimationSource.Reaction,
                    PlayerAnimationSlots.Reaction(snapshot.Hit.ReactionId, snapshot.State));
            }

            if (!playback.IsValid)
            {
                _animation.CrossFade(
                    PlayerAnimationSource.Reaction,
                    PlayerAnimationSlots.Reaction(snapshot.State));
            }
        }

        public void Reset()
        {
            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
        }
    }
}
