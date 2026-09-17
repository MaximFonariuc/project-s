using _App;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class LocomotionRateTests
    {
        private const float WalkSpeed = 3.0f;
        private const float RunSpeed = 5.0f;
        private const float SprintSpeed = 10.0f;
        private const float Forward = 1.0f;
        private const float Backward = -1.0f;

        [Test]
        public void StandingStillKeepsNativePlaybackRate()
        {
            Assert.AreEqual(
                1.0f,
                XControllerAnimationContract.ResolveLocomotionRate(0.0f, 0.0f),
                1e-4f);
        }

        [Test]
        public void TierSpeedsPlayAtTheirAuthoredRate()
        {
            foreach (XControllerLocomotionTier tier in
                     XControllerAnimationContract.LocomotionTiers)
            {
                if (tier.ForwardSpeed <= 0.0f)
                {
                    continue;
                }

                Assert.AreEqual(
                    1.0f,
                    XControllerAnimationContract.ResolveLocomotionRate(
                        tier.ForwardSpeed,
                        Forward),
                    1e-3f,
                    $"Tier {tier.ForwardSpeed:F3} m/s must not be retimed.");
            }
        }

        [Test]
        public void WalkAndRunSpeedsStayInsideTheSupportedRateBand()
        {
            float[] speeds = { WalkSpeed, RunSpeed };
            for (int i = 0; i < speeds.Length; i++)
            {
                float rate = XControllerAnimationContract.ResolveLocomotionRate(
                    speeds[i],
                    Forward);
                Assert.Greater(
                    rate,
                    XControllerAnimationContract.MinimumLocomotionRate,
                    $"{speeds[i]:F1} m/s saturates the lower rate clamp.");
                Assert.Less(
                    rate,
                    XControllerAnimationContract.MaximumLocomotionRate,
                    $"{speeds[i]:F1} m/s saturates the upper rate clamp.");
            }
        }

        [Test]
        public void SprintSpeedSaturatesTheUpperClamp()
        {
            // The fastest authored cycle is the run clip, so sprint speed cannot
            // be matched without a dedicated faster animation.
            Assert.AreEqual(
                XControllerAnimationContract.MaximumLocomotionRate,
                XControllerAnimationContract.ResolveLocomotionRate(
                    SprintSpeed,
                    Forward),
                1e-3f);
        }

        [Test]
        public void ForwardRateMatchesTheTravelledDistance()
        {
            float rate = XControllerAnimationContract.ResolveLocomotionRate(
                RunSpeed,
                Forward);
            float authored = XControllerAnimationContract.ResolveAuthoredSpeed(
                RunSpeed,
                Forward);

            Assert.AreEqual(RunSpeed, authored * rate, 1e-3f);
        }

        [Test]
        public void BackwardRateUsesTheBackwardClipSpeed()
        {
            float authoredForward = XControllerAnimationContract.ResolveAuthoredSpeed(
                RunSpeed,
                Forward);
            float authoredBackward = XControllerAnimationContract.ResolveAuthoredSpeed(
                RunSpeed,
                Backward);

            Assert.Less(
                authoredBackward,
                authoredForward,
                "Backward clips travel slower than forward clips.");
            Assert.Greater(
                XControllerAnimationContract.ResolveLocomotionRate(
                    RunSpeed,
                    Backward),
                XControllerAnimationContract.ResolveLocomotionRate(
                    RunSpeed,
                    Forward),
                "Backpedalling at the same speed needs a faster cadence.");
        }

        [Test]
        public void SpeedBelowTheSlowestTierSlowsTheWalkCycleDown()
        {
            XControllerLocomotionTier slowest =
                XControllerAnimationContract.LocomotionTiers[1];
            float speed = slowest.ForwardSpeed * 0.5f;

            Assert.AreEqual(
                0.5f,
                XControllerAnimationContract.ResolveLocomotionRate(speed, Forward),
                1e-3f);
        }

        [Test]
        public void SpeedAboveTheFastestTierIsClampedNotExtrapolated()
        {
            int last = XControllerAnimationContract.LocomotionTiers.Count - 1;
            XControllerLocomotionTier fastest =
                XControllerAnimationContract.LocomotionTiers[last];

            Assert.AreEqual(
                fastest.ForwardSpeed,
                XControllerAnimationContract.ResolveAuthoredSpeed(
                    fastest.ForwardSpeed * 4.0f,
                    Forward),
                1e-3f);
        }

        [Test]
        public void TiersAreOrderedByAscendingForwardSpeed()
        {
            for (int i = 1;
                 i < XControllerAnimationContract.LocomotionTiers.Count;
                 i++)
            {
                Assert.Greater(
                    XControllerAnimationContract.LocomotionTiers[i].ForwardSpeed,
                    XControllerAnimationContract.LocomotionTiers[i - 1].ForwardSpeed,
                    "Blend tree thresholds must increase monotonically.");
            }
        }
    }
}