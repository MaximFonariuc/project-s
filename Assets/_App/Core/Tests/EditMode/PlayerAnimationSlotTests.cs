using System;
using _App;
using ModularAnimation;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class PlayerAnimationSlotTests
    {
        [Test]
        public void EveryLadderSlotResolvesToItsOwnVariant()
        {
            AnimationSlotId[] ordered =
            {
                PlayerAnimationSlots.LadderEnterTopFront,
                PlayerAnimationSlots.LadderEnterTopBack,
                PlayerAnimationSlots.LadderEnterBottom,
                PlayerAnimationSlots.LadderIdle,
                PlayerAnimationSlots.LadderAscend,
                PlayerAnimationSlots.LadderDescend,
                PlayerAnimationSlots.LadderExitTop,
                PlayerAnimationSlots.LadderExitBottom
            };

            for (int i = 0; i < ordered.Length; i++)
            {
                Assert.IsTrue(
                    PlayerAnimatorActions.TryCreate(ordered[i], out AnimationActionRequest request),
                    $"Ladder slot '{ordered[i]}' is not mapped.");
                Assert.AreEqual(PlayerAnimatorActions.LadderTraversal, request.Action);
                Assert.AreEqual(i, request.Variant);
            }
        }

        [Test]
        public void LadderSlotsRequireACallerSuppliedDuration()
        {
            Assert.IsTrue(PlayerAnimatorActions.TryCreate(
                PlayerAnimationSlots.LadderIdle,
                out AnimationActionRequest request));

            Assert.AreEqual(0.0f, request.ExpectedDuration);
        }

        [Test]
        public void DodgeSlotsMapToDistinctVariants()
        {
            Assert.IsTrue(PlayerAnimatorActions.TryCreate(
                PlayerAnimationSlots.DodgeForward, out AnimationActionRequest forward));
            Assert.IsTrue(PlayerAnimatorActions.TryCreate(
                PlayerAnimationSlots.DodgeBackward, out AnimationActionRequest backward));

            Assert.AreEqual(PlayerAnimatorActions.Dodge, forward.Action);
            Assert.AreEqual(PlayerAnimatorActions.Dodge, backward.Action);
            Assert.AreNotEqual(forward.Variant, backward.Variant);
        }

        [Test]
        public void DirectionalHitStunTakesPrecedenceOverThePlainSlot()
        {
            AnimationSlotId plain = PlayerAnimationSlots.Reaction(
                CharacterReactionKind.Light,
                CharacterReactionStateId.HitStun);
            AnimationSlotId back = PlayerAnimationSlots.Reaction(
                CharacterReactionKind.Light,
                CharacterReactionStateId.HitStun,
                CharacterReactionDirection.Back);

            Assert.IsTrue(PlayerAnimatorActions.TryCreate(plain, out AnimationActionRequest plainRequest));
            Assert.IsTrue(PlayerAnimatorActions.TryCreate(back, out AnimationActionRequest backRequest));

            Assert.AreEqual(0, plainRequest.Variant);
            Assert.AreEqual(6, backRequest.Variant);
        }

        [Test]
        public void FrontDirectionFallsBackToThePlainHitStunSlot()
        {
            AnimationSlotId front = PlayerAnimationSlots.Reaction(
                CharacterReactionKind.Light,
                CharacterReactionStateId.HitStun,
                CharacterReactionDirection.Front);

            Assert.IsTrue(PlayerAnimatorActions.TryCreate(front, out AnimationActionRequest request));
            Assert.AreEqual(0, request.Variant);
        }

        [Test]
        public void DeathIsTheOnlyReactionCarryingABakedDuration()
        {
            AnimationSlotId dead = PlayerAnimationSlots.Reaction(CharacterReactionStateId.Dead);
            AnimationSlotId hitStun = PlayerAnimationSlots.Reaction(CharacterReactionStateId.HitStun);

            Assert.IsTrue(PlayerAnimatorActions.TryCreate(dead, out AnimationActionRequest deadRequest));
            Assert.IsTrue(PlayerAnimatorActions.TryCreate(hitStun, out AnimationActionRequest stunRequest));

            Assert.Greater(deadRequest.ExpectedDuration, 0.0f);
            Assert.AreEqual(0.0f, stunRequest.ExpectedDuration);
        }

        [Test]
        public void UnknownSlotIsRefused()
        {
            Assert.IsFalse(PlayerAnimatorActions.TryCreate(
                new AnimationSlotId("Nonsense.Slot"), out _));
            Assert.IsFalse(PlayerAnimatorActions.TryCreate(default, out _));
        }

        [Test]
        public void EveryReachableReactionSlotIsMapped()
        {
            foreach (CharacterReactionStateId state in
                     Enum.GetValues(typeof(CharacterReactionStateId)))
            {
                if (state == CharacterReactionStateId.Stable)
                {
                    continue;
                }

                foreach (CharacterReactionKind kind in
                         Enum.GetValues(typeof(CharacterReactionKind)))
                {
                    AnimationSlotId slot = PlayerAnimationSlots.Reaction(kind, state);
                    Assert.IsTrue(
                        PlayerAnimatorActions.TryCreate(slot, out _),
                        $"Reaction slot '{slot}' is not mapped.");
                }
            }
        }
    }
}
