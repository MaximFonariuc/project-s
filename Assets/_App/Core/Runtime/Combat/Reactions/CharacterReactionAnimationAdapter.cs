using System;
using ModularAnimation;
using ModularAnimation.Unity;

namespace _App
{
    internal sealed class CharacterReactionAnimationAdapter : ICharacterReactionPresentation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly AnimationsData _animationsData;
        private AnimationActionLease _activeAction;

        public CharacterReactionAnimationAdapter(
            PlayerAnimationCoordinator animation,
            AnimationsData animationsData)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _animationsData = animationsData != null
                ? animationsData
                : throw new ArgumentNullException(nameof(animationsData));
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

            if (snapshot.Hit.ReactionId.IsValid)
            {
                AnimationSlotId specificSlot =
                    PlayerAnimationSlots.Reaction(snapshot.Hit.ReactionId, snapshot.State);
                if (TryPlayAction(source, specificSlot))
                {
                    return;
                }
            }

            AnimationSlotId stateSlot = PlayerAnimationSlots.Reaction(snapshot.State);
            if (TryPlayAction(source, stateSlot))
            {
                return;
            }

            _activeAction.Release();
            _activeAction = default;
            _animation.CrossFade(source, stateSlot);
        }

        public void Reset()
        {
            _activeAction.Release();
            _activeAction = default;
            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
            _animation.ReleaseAll(PlayerAnimationSource.Death);
        }

        private bool TryPlayAction(
            PlayerAnimationSource source,
            AnimationSlotId slot)
        {
            if (!_animationsData.TryGetSharedAction(
                    slot,
                    out CharacterAnimationActionPresentation presentation))
            {
                return false;
            }

            AnimationClipActionRequest request = presentation.CreateRequest();
            if (!_animation.TryPlayAction(
                    source,
                    in request,
                    presentation.BlendOutSeconds,
                    out AnimationActionLease action))
            {
                return false;
            }

            _activeAction = action;
            return true;
        }
    }
}
