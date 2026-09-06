using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed partial class CharacterMovement
    {
        private Vector3 FindOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            const float thickness = (CONTACT_OFFSET - SWEEP_EDGE_REJECT_DISTANCE) * 0.5f;

            Vector3 result = inHit.normal;

            Vector3 rayOrigin = inHit.point - sweepDirDenorm;

            float rayLength = sweepDirDenorm.magnitude * 2f;
            Vector3 rayDirection = sweepDirDenorm / sweepDirDenorm.magnitude;

            if (Raycast(rayOrigin, rayDirection, rayLength, _collisionLayers, out RaycastHit hitResult, thickness))
                result = hitResult.normal;

            return result;
        }


        private static Vector3 FindBoxOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            Transform localToWorld = inHit.transform;

            Vector3 localContactNormal = localToWorld.InverseTransformDirection(inHit.normal);
            Vector3 localTraceDirDenorm = localToWorld.InverseTransformDirection(sweepDirDenorm);

            Vector3 bestLocalNormal = localContactNormal;
            float bestOpposingDot = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (localContactNormal[i] > KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = 1.0f;
                    }
                }
                else if (localContactNormal[i] < -KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = -localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = -1.0f;
                    }
                }
            }

            return localToWorld.TransformDirection(bestLocalNormal);
        }


        private static Vector3 FindBoxOpposingNormal(Vector3 displacement, Vector3 hitNormal, Transform hitTransform)
        {
            Transform localToWorld = hitTransform;

            Vector3 localContactNormal = localToWorld.InverseTransformDirection(hitNormal);
            Vector3 localTraceDirDenorm = localToWorld.InverseTransformDirection(displacement);

            Vector3 bestLocalNormal = localContactNormal;
            float bestOpposingDot = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (localContactNormal[i] > KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = 1.0f;
                    }
                }
                else if (localContactNormal[i] < -KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = -localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = -1.0f;
                    }
                }
            }

            return localToWorld.TransformDirection(bestLocalNormal);
        }


        private static Vector3 FindTerrainOpposingNormal(ref RaycastHit inHit)
        {
            TerrainCollider terrainCollider = inHit.collider as TerrainCollider;

            if (terrainCollider != null)
            {
                Vector3 localPoint = terrainCollider.transform.InverseTransformPoint(inHit.point);

                TerrainData terrainData = terrainCollider.terrainData;

                Vector3 interpolatedNormal = terrainData.GetInterpolatedNormal(localPoint.x / terrainData.size.x,
                    localPoint.z / terrainData.size.z);

                return interpolatedNormal;
            }

            return inHit.normal;
        }

        private Vector3 FindGeomOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            if (inHit.collider is SphereCollider _ || inHit.collider is CapsuleCollider _)
            {
                return inHit.normal;
            }

            if (inHit.collider is BoxCollider _)
            {
                return FindBoxOpposingNormal(sweepDirDenorm, ref inHit);
            }

            if (inHit.collider is MeshCollider nonConvexMeshCollider && !nonConvexMeshCollider.convex)
            {
                Mesh sharedMesh = nonConvexMeshCollider.sharedMesh;
                if (sharedMesh && sharedMesh.isReadable && !_advanced.UseFastGeomNormalPath)
                    return MeshUtility.FindMeshOpposingNormal(sharedMesh, ref inHit);

                return FindOpposingNormal(sweepDirDenorm, ref inHit);
            }

            if (inHit.collider is MeshCollider convexMeshCollider && convexMeshCollider.convex)
            {
                return FindOpposingNormal(sweepDirDenorm, ref inHit);
            }


            if (inHit.collider is TerrainCollider && !_advanced.UseFastGeomNormalPath)
            {
                return FindTerrainOpposingNormal(ref inHit);
            }

            return inHit.normal;
        }


        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }


        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static Vector3 ApplyVelocityBraking(Vector3 currentVelocity, float friction, float deceleration, float deltaTime)
        {
            bool isZeroFriction = friction == 0.0f;
            bool isZeroBraking = deceleration == 0.0f;

            if (isZeroFriction && isZeroBraking)
                return currentVelocity;

            Vector3 oldVel = currentVelocity;
            Vector3 revAcceleration = isZeroBraking ? Vector3.zero : -deceleration * currentVelocity.normalized;

            currentVelocity += (-friction * currentVelocity + revAcceleration) * deltaTime;

            if (Vector3.Dot(currentVelocity, oldVel) <= 0.0f)
                return Vector3.zero;

            float sqrSpeed = currentVelocity.sqrMagnitude;
            if (sqrSpeed <= 0.00001f || !isZeroBraking && sqrSpeed <= 0.01f)
                return Vector3.zero;

            return currentVelocity;
        }


        private static float ComputeAnalogInputModifier(Vector3 desiredVelocity, float maxSpeed)
        {
            if (maxSpeed > 0.0f && desiredVelocity.sqrMagnitude > 0.0f)
                return Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);

            return 0.0f;
        }


        private static Vector3 CalcVelocity(Vector3 currentVelocity, Vector3 desiredVelocity, float maxSpeed,
            float acceleration, float deceleration, float friction, float brakingFriction, float deltaTime)
        {
            float desiredSpeed = desiredVelocity.magnitude;
            Vector3 desiredMoveDirection = desiredSpeed > 0.0f ? desiredVelocity / desiredSpeed : Vector3.zero;

            float analogInputModifier = ComputeAnalogInputModifier(desiredVelocity, maxSpeed);
            Vector3 requestedAcceleration = acceleration * analogInputModifier * desiredMoveDirection;

            float actualMaxSpeed = Mathf.Max(0.0f, maxSpeed * analogInputModifier);

            bool isZeroAcceleration = requestedAcceleration.IsZero();
            bool isVelocityOverMax = currentVelocity.IsExceeding(actualMaxSpeed);

            if (isZeroAcceleration || isVelocityOverMax)
            {
                Vector3 oldVelocity = currentVelocity;

                currentVelocity = ApplyVelocityBraking(currentVelocity, brakingFriction, deceleration, deltaTime);

                if (isVelocityOverMax && currentVelocity.sqrMagnitude < actualMaxSpeed.Square() &&
                    Vector3.Dot(requestedAcceleration, oldVelocity) > 0.0f)
                    currentVelocity = oldVelocity.normalized * actualMaxSpeed;
            }
            else
            {
                currentVelocity -= (currentVelocity - desiredMoveDirection * currentVelocity.magnitude) * Mathf.Min(friction * deltaTime, 1.0f);
            }

            if (!isZeroAcceleration)
            {
                float newMaxSpeed = currentVelocity.IsExceeding(actualMaxSpeed) ? currentVelocity.magnitude : actualMaxSpeed;

                currentVelocity += requestedAcceleration * deltaTime;
                currentVelocity = currentVelocity.ClampedTo(newMaxSpeed);
            }

            return currentVelocity;
        }


        private static Vector3 GetRigidbodyVelocity(Rigidbody rigidbody, Vector3 worldPoint)
        {
            if (rigidbody == null)
                return Vector3.zero;

            return rigidbody.TryGetComponent(out CharacterMovement controller)
                ? controller.Velocity
                : rigidbody.GetPointVelocity(worldPoint);
        }


        private static bool IsWalkable(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.Walkable) != 0;
        }


        private static bool IsNotWalkable(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.NotWalkable) != 0;
        }


        private static bool CanPerchOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanPerchOn) != 0;
        }


        private static bool CanNotPerchOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotPerchOn) != 0;
        }


        private static bool CanStepOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanStepOn) != 0;
        }


        private static bool CanNotStepOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotStepOn) != 0;
        }


        private static bool CanRideOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanRideOn) != 0;
        }


        private static bool CanNotRideOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotRideOn) != 0;
        }

    }
}
