using UnityEngine;

namespace ModularCharacterMovement
{
    public struct FindGroundResult
    {
        public bool HitGround;
        public bool IsWalkable;
        public bool IsWalkableGround => HitGround && IsWalkable;

        public Vector3 Position;
        public Vector3 Point => HitResult.point;
        public Vector3 Normal => HitResult.normal;
        public Vector3 SurfaceNormal;
        public Collider Collider;
        public Rigidbody Rigidbody => Collider ? Collider.attachedRigidbody : null;
        public Transform Transform
        {
            get
            {
                if (Collider == null)
                    return null;

                Rigidbody attachedRigidbody = Collider.attachedRigidbody;
                return attachedRigidbody ? attachedRigidbody.transform : Collider.transform;
            }
        }

        public float GroundDistance;
        public bool IsRaycastResult;
        public float RaycastDistance;
        public RaycastHit HitResult;
        public float GetDistanceToGround()
        {
            return IsRaycastResult ? RaycastDistance : GroundDistance;
        }
        
        public void SetFromSweepResult(bool hitGround, bool isWalkable, Vector3 position, float sweepDistance, ref RaycastHit inHit, Vector3 surfaceNormal)
        {
            HitGround = hitGround;
            IsWalkable = isWalkable;
            
            Position = position;

            Collider = inHit.collider;

            GroundDistance = sweepDistance;

            IsRaycastResult = false;
            RaycastDistance = 0.0f;

            HitResult = inHit;

            SurfaceNormal = surfaceNormal;
        }

        public void SetFromSweepResult(bool hitGround, bool isWalkable, Vector3 position, Vector3 point, Vector3 normal, Vector3 surfaceNormal, Collider collider, float sweepDistance)
        {
            HitGround = hitGround;
            IsWalkable = isWalkable;
            
            Position = position;

            Collider = collider;

            GroundDistance = sweepDistance;

            IsRaycastResult = false;
            RaycastDistance = 0.0f;

            HitResult = new RaycastHit
            {
                point = point,
                normal = normal,

                distance = sweepDistance
            };

            SurfaceNormal = surfaceNormal;
        }
        
        public void SetFromRaycastResult(bool hitGround, bool isWalkable, Vector3 position, float sweepDistance, float castDistance, ref RaycastHit inHit)
        {
            HitGround = hitGround;
            IsWalkable = isWalkable;

            Position = position;

            Collider = inHit.collider;

            GroundDistance = sweepDistance;

            IsRaycastResult = true;
            RaycastDistance = castDistance;

            float oldDistance = HitResult.distance;

            HitResult = inHit;
            HitResult.distance = oldDistance;

            SurfaceNormal = HitResult.normal;
        }
    }
}