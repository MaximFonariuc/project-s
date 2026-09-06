using _App;
using ModularAnimation;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class CombatPresentationResolverTests
    {
        private static bool Resolve(
            PlayerCombatPresentationKind kind,
            int comboIndex,
            string animationId = null)
        {
            PlayerCombatAnimationId id = animationId == null
                ? default
                : new PlayerCombatAnimationId(animationId);
            return PlayerCombatPresentationResolver.TryResolve(
                kind,
                comboIndex,
                id,
                out AnimationActionId _,
                out int _);
        }

        [Test]
        public void GroundComboIndexesWithinCapacityResolve()
        {
            for (int i = 0; i < PlayerAnimatorActions.LightAttackVariantCount; i++)
            {
                Assert.IsTrue(
                    Resolve(PlayerCombatPresentationKind.ComboStep, i),
                    $"Ground combo index {i} should resolve.");
            }
        }

        [Test]
        public void GroundComboIndexBeyondCapacityIsRefused()
        {
            Assert.IsFalse(Resolve(
                PlayerCombatPresentationKind.ComboStep,
                PlayerAnimatorActions.LightAttackVariantCount));
        }

        [Test]
        public void AirComboIndexesWithinCapacityResolve()
        {
            for (int i = 0; i < PlayerAnimatorActions.AirAttackVariantCount; i++)
            {
                Assert.IsTrue(
                    Resolve(PlayerCombatPresentationKind.AirComboStep, i),
                    $"Air combo index {i} should resolve.");
            }
        }

        [Test]
        public void AirComboIndexBeyondCapacityIsRefused()
        {
            Assert.IsFalse(Resolve(
                PlayerCombatPresentationKind.AirComboStep,
                PlayerAnimatorActions.AirAttackVariantCount));
        }

        [Test]
        public void NegativeComboIndexIsRefused()
        {
            Assert.IsFalse(Resolve(PlayerCombatPresentationKind.ComboStep, -1));
            Assert.IsFalse(Resolve(PlayerCombatPresentationKind.AirComboStep, -1));
        }

        [Test]
        public void EveryKnownNamedAnimationResolves()
        {
            string[] known =
            {
                "attack.heavy",
                "skill.special",
                "attack.launcher",
                "movement.air-chase"
            };

            foreach (string id in known)
            {
                Assert.IsTrue(
                    Resolve(PlayerCombatPresentationKind.NamedAnimation, 0, id),
                    $"Named animation '{id}' should resolve.");
            }
        }

        [Test]
        public void UnknownNamedAnimationIsRefused()
        {
            Assert.IsFalse(Resolve(
                PlayerCombatPresentationKind.NamedAnimation,
                0,
                "attack.typo"));
        }

        [Test]
        public void ComboStepIgnoresTheAnimationId()
        {
            Assert.IsTrue(Resolve(PlayerCombatPresentationKind.ComboStep, 0, "attack.typo"));
        }

        [Test]
        public void GroundAndAirCombosUseDifferentActions()
        {
            PlayerCombatPresentationResolver.TryResolve(
                PlayerCombatPresentationKind.ComboStep, 0, default,
                out AnimationActionId ground, out int groundVariant);
            PlayerCombatPresentationResolver.TryResolve(
                PlayerCombatPresentationKind.AirComboStep, 0, default,
                out AnimationActionId air, out int airVariant);

            Assert.AreNotEqual(ground, air);
            Assert.AreEqual(0, groundVariant);
            Assert.AreEqual(0, airVariant);
        }
    }
}
