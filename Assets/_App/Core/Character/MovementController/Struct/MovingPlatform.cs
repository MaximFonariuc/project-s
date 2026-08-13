using UnityEngine;

namespace _App
{
    public struct MovingPlatform
    {
        public Rigidbody LastPlatform;
        public Rigidbody Platform;
        public Vector3 Position;
        public Vector3 LocalPosition;
        public Vector3 DeltaPosition;
        public Quaternion Rotation;
        public Quaternion LocalRotation;
        public Quaternion DeltaRotation;
        public Vector3 PlatformVelocity;
    }
}