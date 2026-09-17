using System;
using _App;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class CombatChargeTrackerTests
    {
        private const float HoldSeconds = 0.3f;

        private sealed class StubClock : ICombatClock
        {
            public float ScaledTime { get; set; }
            public float UnscaledTime { get; set; }
        }

        private StubClock _clock;
        private CombatChargeTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _clock = new StubClock();
            _tracker = new CombatChargeTracker(_clock, HoldSeconds);
        }

        [Test]
        public void MissingClockIsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CombatChargeTracker(null, HoldSeconds));
        }

        [Test]
        public void NonPositiveThresholdIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CombatChargeTracker(_clock, 0.0f));
        }

        [Test]
        public void ReleaseBeforeThresholdDoesNotCharge()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = HoldSeconds - 0.01f;

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out _));
        }

        [Test]
        public void ReleaseAtThresholdCharges()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = HoldSeconds;

            Assert.IsTrue(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out PlayerCombatCommandId charged));
            Assert.AreEqual(PlayerCombatCommandIds.ChargedHeavyAttack, charged);
        }

        [Test]
        public void ChargeIsMeasuredFromTheLatestPress()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = 5.0f;
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = 5.0f + HoldSeconds - 0.01f;

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out _));
        }

        [Test]
        public void ReleasingADifferentCommandIsIgnored()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = HoldSeconds * 2.0f;

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.LightAttack,
                out _));
        }

        [Test]
        public void CommandsWithoutAChargedVariantNeverCharge()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.LightAttack);
            _clock.UnscaledTime = HoldSeconds * 2.0f;

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.LightAttack,
                out _));
        }

        [Test]
        public void ASecondReleaseDoesNotChargeAgain()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = HoldSeconds;
            _tracker.TryResolveRelease(PlayerCombatCommandIds.HeavyAttack, out _);

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out _));
        }

        [Test]
        public void ClearDropsAPendingHold()
        {
            _tracker.NotePressed(PlayerCombatCommandIds.HeavyAttack);
            _clock.UnscaledTime = HoldSeconds * 2.0f;
            _tracker.Clear();

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out _));
        }

        [Test]
        public void ReleaseWithoutPressIsIgnored()
        {
            _clock.UnscaledTime = HoldSeconds * 2.0f;

            Assert.IsFalse(_tracker.TryResolveRelease(
                PlayerCombatCommandIds.HeavyAttack,
                out _));
        }
    }
}