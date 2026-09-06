using System;
using UnityEngine;

namespace ModularCharacterMovement
{
    [Serializable]
    public struct Advanced
    {
        private const int MinimumMovementIterations = 3;
        private const int MinimumDepenetrationIterations = 2;

        public float MinMoveDistance;
        public float MinMoveDistanceSqr => MinMoveDistance * MinMoveDistance;

        [Min(MinimumMovementIterations)]
        public int MaxMovementIterations;

        [Min(MinimumDepenetrationIterations)]
        public int MaxDepenetrationIterations;

        public bool UseFastGeomNormalPath;

        [Space(15f)] public bool EnablePhysicsInteraction;

        public bool AllowPushCharacters;

        public bool ImpartPlatformMovement;

        public bool ImpartPlatformRotation;

        public bool ImpartPlatformVelocity;

        public void Reset()
        {
            MinMoveDistance = 0.0f;

            MaxMovementIterations = 5;
            MaxDepenetrationIterations = 4;

            EnablePhysicsInteraction = false;
            AllowPushCharacters = false;
            ImpartPlatformMovement = false;
            ImpartPlatformRotation = false;
            ImpartPlatformVelocity = false;
        }

        public void Sanitize()
        {
            MinMoveDistance = Mathf.Max(MinMoveDistance, 0.0f);

            MaxMovementIterations = Mathf.Max(MaxMovementIterations, MinimumMovementIterations);
            MaxDepenetrationIterations = Mathf.Max(
                MaxDepenetrationIterations,
                MinimumDepenetrationIterations);
        }
    }
}
