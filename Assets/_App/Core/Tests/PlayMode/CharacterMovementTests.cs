using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectS.Core.PlayModeTests
{
    public sealed class CharacterMovementTests
    {
        private CharacterMovementFixture _world;

        [SetUp]
        public void SetUp()
        {
            _world = new CharacterMovementFixture();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void OpenGroundMovementTravelsTheRequestedDistance()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            _world.Advance(new Vector3(2.0f, 0.0f, 0.0f), steps: 50);

            Vector3 position = _world.Movement.GetPosition();
            Assert.AreEqual(2.0f, position.x, 0.05f);
            Assert.AreEqual(0.0f, position.z, 0.01f);
        }

        [Test]
        public void CharacterRestingOnGroundIsGrounded()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            _world.Advance(Vector3.zero, steps: 5);

            Assert.IsTrue(_world.Movement.IsGrounded);
            Assert.IsTrue(_world.Movement.IsOnWalkableGround);
        }

        [Test]
        public void WallStopsHorizontalPenetration()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(3.0f, 1.0f, 0.0f), new Vector3(1.0f, 4.0f, 8.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            _world.Advance(new Vector3(4.0f, 0.0f, 0.0f), steps: 100);

            float wallFace = 3.0f - 0.5f;
            Assert.Less(
                _world.Movement.GetPosition().x,
                wallFace - CharacterMovementFixture.Radius + 0.05f,
                "Character pushed through the wall.");
        }

        [Test]
        public void DiagonalApproachSlidesAlongTheWall()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(3.0f, 1.0f, 0.0f), new Vector3(1.0f, 4.0f, 12.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            _world.Advance(new Vector3(3.0f, 0.0f, 3.0f), steps: 100);

            Vector3 position = _world.Movement.GetPosition();
            Assert.Greater(position.z, 1.0f, "Character did not slide along the wall.");
        }

        [Test]
        public void StepBelowStepOffsetIsClimbed()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(3.0f, 0.15f, 0.0f), new Vector3(2.0f, 0.3f, 6.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();
            _world.Movement.StepOffset = 0.5f;

            _world.Advance(new Vector3(2.0f, 0.0f, 0.0f), steps: 75);

            Vector3 position = _world.Movement.GetPosition();
            Assert.Greater(position.y, 0.25f, "Character failed to step up.");
            Assert.Greater(position.x, 2.5f, "Character did not reach the step top.");
            Assert.Less(position.x, 4.0f, "Character already walked off the step.");
        }

        [Test]
        public void StepAboveStepOffsetBlocksMovement()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(3.0f, 0.75f, 0.0f), new Vector3(2.0f, 1.5f, 6.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();
            _world.Movement.StepOffset = 0.3f;

            _world.Advance(new Vector3(2.0f, 0.0f, 0.0f), steps: 120);

            Vector3 position = _world.Movement.GetPosition();
            Assert.Less(position.y, 0.3f, "Character climbed a step above the offset.");
            Assert.Less(position.x, 2.1f, "Character passed through the step.");
        }

        [Test]
        public void OverlappingSpawnIsPushedOut()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(0.2f, 1.0f, 0.0f), new Vector3(1.0f, 2.0f, 4.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            _world.Advance(Vector3.zero, steps: 20);

            Vector3 position = _world.Movement.GetPosition();
            Assert.IsFalse(_world.Movement.CheckCapsule(), "Character stayed overlapped.");
            Assert.Less(position.x, -0.1f, "Depenetration pushed the wrong way.");
        }

        [Test]
        public void NonFiniteVelocityIsRejectedAndStateStaysValid()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();
            Vector3 before = _world.Movement.GetPosition();

            LogAssert.Expect(LogType.Error, new Regex("rejected a non-finite move"));
            _world.Movement.Move(new Vector3(float.NaN, 0.0f, 0.0f), 0.02f);

            Vector3 after = _world.Movement.GetPosition();
            Assert.AreEqual(before.x, after.x, 1e-4f);
            Assert.AreEqual(before.y, after.y, 1e-4f);
            Assert.AreEqual(before.z, after.z, 1e-4f);
            Assert.IsTrue(CharacterMovementIsFinite(after));
        }

        [Test]
        public void NonFiniteDeltaTimeIsRejected()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();
            Vector3 before = _world.Movement.GetPosition();

            LogAssert.Expect(LogType.Error, new Regex("rejected a non-finite move"));
            _world.Movement.Move(new Vector3(1.0f, 0.0f, 0.0f), float.NaN);

            Assert.AreEqual(before.x, _world.Movement.GetPosition().x, 1e-4f);
        }

        [Test]
        public void MovementSurvivesRepeatedRejections()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();

            LogAssert.Expect(LogType.Error, new Regex("rejected a non-finite move"));
            _world.Movement.Move(new Vector3(float.NaN, 0.0f, 0.0f), 0.02f);
            _world.Movement.Move(new Vector3(float.PositiveInfinity, 0.0f, 0.0f), 0.02f);

            _world.Advance(new Vector3(2.0f, 0.0f, 0.0f), steps: 25);

            Assert.AreEqual(1.0f, _world.Movement.GetPosition().x, 0.1f);
        }


        [Test]
        public void SteppingOffTheFarEdgeReturnsToGround()
        {
            _world.SpawnGround(new Vector3(0.0f, -0.5f, 0.0f));
            _world.SpawnBox(new Vector3(3.0f, 0.15f, 0.0f), new Vector3(2.0f, 0.3f, 6.0f));
            _world.SpawnCharacter(new Vector3(0.0f, 0.1f, 0.0f));
            _world.Settle();
            _world.Movement.StepOffset = 0.5f;

            _world.Advance(new Vector3(2.0f, 0.0f, 0.0f), steps: 120);

            Vector3 position = _world.Movement.GetPosition();
            Assert.Greater(position.x, 4.0f, "Character did not clear the step.");
            Assert.Less(position.y, 0.1f, "Character stayed floating after the drop.");
            Assert.IsTrue(_world.Movement.IsGrounded);
        }

        private static bool CharacterMovementIsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) && !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }
    }
}
