using System;
using _App;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class GameplayTimeScaleControllerTests
    {
        private sealed class FakeTimeScaleBackend : ITimeScaleBackend
        {
            public float TimeScale { get; set; } = 1.0f;
        }

        private static GameplayTimeScaleController CreateEnabled(
            out FakeTimeScaleBackend backend)
        {
            backend = new FakeTimeScaleBackend();
            var controller = new GameplayTimeScaleController(backend);
            controller.Enable();
            return controller;
        }

        [Test]
        public void Request_AppliesRequestedScale()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);

            controller.Request(new GameplayTimeScaleRequest(0.1f, 0.25f));

            Assert.AreEqual(0.25f, backend.TimeScale, 1e-5f);
            Assert.IsTrue(controller.IsActive);
        }

        [Test]
        public void ConcurrentRequests_ApplyTheStrongestSlowdown()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);

            controller.Request(new GameplayTimeScaleRequest(0.1f, 0.50f));
            controller.Request(new GameplayTimeScaleRequest(0.1f, 0.10f));

            Assert.AreEqual(0.10f, backend.TimeScale, 1e-5f);
        }

        [Test]
        public void ExpiredRequest_RestoresBaselineScale()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);
            controller.Request(new GameplayTimeScaleRequest(0.10f, 0.20f));

            controller.Tick(0.20f);

            Assert.AreEqual(1.0f, backend.TimeScale, 1e-5f);
            Assert.IsFalse(controller.IsActive);
        }

        [Test]
        public void LongerRequest_SurvivesShorterOne()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);
            controller.Request(new GameplayTimeScaleRequest(0.05f, 0.10f));
            controller.Request(new GameplayTimeScaleRequest(0.50f, 0.40f));

            controller.Tick(0.10f);

            Assert.AreEqual(0.40f, backend.TimeScale, 1e-5f);
            Assert.IsTrue(controller.IsActive);
        }

        [Test]
        public void Disable_RestoresBaselineCapturedOnEnable()
        {
            var backend = new FakeTimeScaleBackend { TimeScale = 0.5f };
            var controller = new GameplayTimeScaleController(backend);
            controller.Enable();
            controller.Request(new GameplayTimeScaleRequest(1.0f, 0.20f));

            controller.Disable();

            Assert.AreEqual(0.5f, backend.TimeScale, 1e-5f);
        }

        [Test]
        public void BaselineIsMultiplied_NotOverwritten()
        {
            var backend = new FakeTimeScaleBackend { TimeScale = 0.5f };
            var controller = new GameplayTimeScaleController(backend);
            controller.Enable();

            controller.Request(new GameplayTimeScaleRequest(1.0f, 0.20f));

            Assert.AreEqual(0.1f, backend.TimeScale, 1e-5f);
        }

        [Test]
        public void RequestBeforeEnable_IsRejected()
        {
            var controller = new GameplayTimeScaleController(new FakeTimeScaleBackend());

            Assert.Throws<InvalidOperationException>(
                () => controller.Request(new GameplayTimeScaleRequest(0.1f, 0.5f)));
        }

        [Test]
        public void NonFiniteDeltaTime_IsRejected()
        {
            GameplayTimeScaleController controller = CreateEnabled(out _);
            controller.Request(new GameplayTimeScaleRequest(0.1f, 0.5f));

            Assert.Throws<ArgumentOutOfRangeException>(() => controller.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => controller.Tick(float.PositiveInfinity));
        }

        [Test]
        public void NonFiniteRequestValues_AreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameplayTimeScaleRequest(float.NaN, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameplayTimeScaleRequest(0.1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameplayTimeScaleRequest(0.1f, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameplayTimeScaleRequest(0.0f, 0.5f));
        }

        [Test]
        public void SaturatedCapacity_KeepsStrongestSlowdown()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);
            for (int i = 0; i < 40; i++)
            {
                controller.Request(new GameplayTimeScaleRequest(1.0f, 0.9f));
            }

            controller.Request(new GameplayTimeScaleRequest(1.0f, 0.05f));

            Assert.AreEqual(0.05f, backend.TimeScale, 1e-5f);
        }

        [Test]
        public void Dispose_RestoresBaseline()
        {
            GameplayTimeScaleController controller = CreateEnabled(out var backend);
            controller.Request(new GameplayTimeScaleRequest(1.0f, 0.10f));

            controller.Dispose();

            Assert.AreEqual(1.0f, backend.TimeScale, 1e-5f);
        }
    }
}
