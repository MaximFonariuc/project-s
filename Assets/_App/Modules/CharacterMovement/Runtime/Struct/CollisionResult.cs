using UnityEngine;

namespace ModularCharacterMovement
{
    public struct CollisionResult
    {
        public bool StartPenetrating;
        public EHitLocation HitLocation;
        public bool IsWalkable;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 OtherVelocity;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 SurfaceNormal;
        public Vector3 DisplacementToHit;
        public Vector3 RemainingDisplacement;
        public Collider Collider;
        public Rigidbody Rigidbody => Collider ? Collider.attachedRigidbody : null;
        public Transform Transform
        {
            get
            {
                if (Collider == null)
                    return null;

                Rigidbody rb = Collider.attachedRigidbody;
                return rb ? rb.transform : Collider.transform;
            }
        }
        public RaycastHit HitResult;
    }
}