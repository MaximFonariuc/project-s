using _App;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class PlayerActionArbitrationTests
    {
        private static PlayerMovementIntent Guarding(bool allowGuard)
        {
            return new PlayerMovementIntent(0.5f, false, false, allowGuard, false);
        }

        private static bool CanStartDefense(
            bool externalActionLock = false,
            bool locomotionBlocksActions = false,
            bool jumpStarting = false,
            bool dodgeStarting = false,
            bool locomotionHasPriorityRequest = false,
            PlayerCombatPhase combatPhase = PlayerCombatPhase.Ready,
            bool combatAllowsGuard = false)
        {
            return PlayerActionArbitration.CanStartDefense(
                externalActionLock,
                locomotionBlocksActions,
                jumpStarting,
                dodgeStarting,
                locomotionHasPriorityRequest,
                combatPhase,
                combatAllowsGuard);
        }

        [Test]
        public void ReactionOrRecoil_CreatesAnExternalLock()
        {
            Assert.IsFalse(PlayerActionArbitration.HasExternalActionLock(false, false));
            Assert.IsTrue(PlayerActionArbitration.HasExternalActionLock(true, false));
            Assert.IsTrue(PlayerActionArbitration.HasExternalActionLock(false, true));
        }

        [Test]
        public void DefenseStartsWhenCombatIsReadyAndNothingBlocks()
        {
            Assert.IsTrue(CanStartDefense());
        }

        [Test]
        public void ExternalLockBlocksDefense()
        {
            Assert.IsFalse(CanStartDefense(externalActionLock: true));
        }

        [Test]
        public void LocomotionBlockersBlockDefense()
        {
            Assert.IsFalse(CanStartDefense(locomotionBlocksActions: true));
            Assert.IsFalse(CanStartDefense(jumpStarting: true));
            Assert.IsFalse(CanStartDefense(dodgeStarting: true));
            Assert.IsFalse(CanStartDefense(locomotionHasPriorityRequest: true));
        }

        [Test]
        public void DefenseIsRefusedMidActionUnlessTheActionAllowsGuard()
        {
            Assert.IsFalse(CanStartDefense(
                combatPhase: PlayerCombatPhase.Executing,
                combatAllowsGuard: false));
            Assert.IsTrue(CanStartDefense(
                combatPhase: PlayerCombatPhase.Executing,
                combatAllowsGuard: true));
        }

        [Test]
        public void AuthoredGuardCancelDoesNotOverrideAHardBlocker()
        {
            Assert.IsFalse(CanStartDefense(
                externalActionLock: true,
                combatPhase: PlayerCombatPhase.Executing,
                combatAllowsGuard: true));
            Assert.IsFalse(CanStartDefense(
                dodgeStarting: true,
                combatPhase: PlayerCombatPhase.Executing,
                combatAllowsGuard: true));
        }

        [Test]
        public void WeaponSwitchPhaseStillRequiresAnAuthoredGuardWindow()
        {
            Assert.IsFalse(CanStartDefense(combatPhase: PlayerCombatPhase.WeaponSwitch));
            Assert.IsTrue(CanStartDefense(
                combatPhase: PlayerCombatPhase.WeaponSwitch,
                combatAllowsGuard: true));
        }

        [Test]
        public void NonDefenseIntentIsLockedUnderAnExternalLock()
        {
            PlayerMovementIntent combat = Guarding(true);

            PlayerMovementIntent locked =
                PlayerActionArbitration.ResolveNonDefenseIntent(true, in combat);
            PlayerMovementIntent free =
                PlayerActionArbitration.ResolveNonDefenseIntent(false, in combat);

            Assert.AreEqual(0.0f, locked.TranslationScale);
            Assert.IsFalse(locked.AllowGuard);
            Assert.AreEqual(combat.TranslationScale, free.TranslationScale);
            Assert.IsTrue(free.AllowGuard);
        }

        [Test]
        public void ExternalLockWinsOverDefenseAndCombatIntents()
        {
            PlayerMovementIntent defense = new PlayerMovementIntent(0.35f, false, false, true, false);
            PlayerMovementIntent combat = PlayerMovementIntent.Unrestricted;

            PlayerMovementIntent result = PlayerActionArbitration.ResolveLocomotionIntent(
                true,
                true,
                in defense,
                in combat);

            Assert.AreEqual(0.0f, result.TranslationScale);
            Assert.IsFalse(result.AllowJump);
            Assert.IsFalse(result.AllowDodge);
        }

        [Test]
        public void DefenseIntentWinsOverCombatWhileDefenseBlocks()
        {
            PlayerMovementIntent defense = new PlayerMovementIntent(0.35f, false, false, true, false);
            PlayerMovementIntent combat = PlayerMovementIntent.Unrestricted;

            PlayerMovementIntent result = PlayerActionArbitration.ResolveLocomotionIntent(
                false,
                true,
                in defense,
                in combat);

            Assert.AreEqual(0.35f, result.TranslationScale, 1e-5f);
        }

        [Test]
        public void CombatIntentAppliesWhenDefenseDoesNotBlock()
        {
            PlayerMovementIntent defense = new PlayerMovementIntent(0.35f, false, false, true, false);
            PlayerMovementIntent combat = PlayerMovementIntent.Unrestricted;

            PlayerMovementIntent result = PlayerActionArbitration.ResolveLocomotionIntent(
                false,
                false,
                in defense,
                in combat);

            Assert.AreEqual(1.0f, result.TranslationScale, 1e-5f);
            Assert.IsTrue(result.AllowJump);
        }
    }
}
