using System;
using UnityEngine;

namespace _App
{
    [Serializable]
    public struct Advanced
    {
        public float MinMoveDistance;
        public float MinMoveDistanceSqr => MinMoveDistance * MinMoveDistance;

        public int MaxMovementIterations;

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
            MaxDepenetrationIterations = 1;

            EnablePhysicsInteraction = false;
            AllowPushCharacters = false;
            ImpartPlatformMovement = false;
            ImpartPlatformRotation = false;
            ImpartPlatformVelocity = false;
        }

        public void OnValidate()
        {
            MinMoveDistance = Mathf.Max(MinMoveDistance, 0.0f);

            MaxMovementIterations = Mathf.Max(MaxMovementIterations, 1);
            MaxDepenetrationIterations = Mathf.Max(MaxDepenetrationIterations, 1);
        }
    }
}