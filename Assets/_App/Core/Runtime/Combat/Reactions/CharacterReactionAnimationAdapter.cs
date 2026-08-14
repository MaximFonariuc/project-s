using System;
using System.Collections.Generic;
using ModularAnimation;

namespace _App
{
    internal sealed class CharacterReactionAnimationAdapter : ICharacterReactionPresentation
    {
        private readonly ICharacterAnimationDriver _animation;
        private readonly Dictionary<CharacterReactionAnimationKey, CharacterReactionAnimationBinding>
            _bindings;

        public CharacterReactionAnimationAdapter(
            ICharacterAnimationDriver animation,
            CharacterReactionProfile profile)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _bindings = profile != null
                ? profile.CreateAnimationMap()
                : new Dictionary<CharacterReactionAnimationKey, CharacterReactionAnimationBinding>();
        }

        public void Present(in CharacterReactionSnapshot snapshot)
        {
            if (!snapshot.IsValid || snapshot.State == CharacterReactionStateId.Stable ||
                !TryGetBinding(snapshot.Hit.ReactionId, snapshot.State, out CharacterReactionAnimationBinding binding))
            {
                return;
            }

            float expectedDuration = binding.ExpectedDuration > 0.0f
                ? binding.ExpectedDuration
                : snapshot.RemainingDuration;
            _animation.CrossFade(
                binding.State,
                binding.Layer,
                binding.TransitionDuration,
                expectedDuration);
        }

        public void Reset()
        {
            // Locomotion presentation remains responsible for the stable pose.
        }

        private bool TryGetBinding(
            CombatReactionId reactionId,
            CharacterReactionStateId state,
            out CharacterReactionAnimationBinding binding)
        {
            if (reactionId.IsValid &&
                _bindings.TryGetValue(
                    new CharacterReactionAnimationKey(reactionId, state),
                    out binding))
            {
                return true;
            }

            return _bindings.TryGetValue(
                new CharacterReactionAnimationKey(CombatReactionId.None, state),
                out binding);
        }
    }
}
