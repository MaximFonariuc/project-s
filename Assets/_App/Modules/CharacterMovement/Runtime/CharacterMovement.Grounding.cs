using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed partial class CharacterMovement
    {
        private bool CanPerchOn(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;

            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanPerchOn(collisionBehaviour))
                    return true;

                if (CanNotPerchOn(collisionBehaviour))
                    return false;
            }

            return true;
        }

        private float GetPerchRadiusThreshold()
        {
            return Mathf.Max(0.0f, _radius - PerchOffset);
        }


        private float GetValidPerchRadius(Collider otherCollider)
        {
            if (!CanPerchOn(otherCollider))
                return 0.0011f;

            return Mathf.Clamp(_perchOffset, 0.0011f, _radius);
        }


        private bool ShouldComputePerchResult(Vector3 characterPosition, ref RaycastHit inHit)
        {
            if (GetPerchRadiusThreshold() <= SWEEP_EDGE_REJECT_DISTANCE)
	        {
		        return false;
	        }

            float distFromCenterSq = (inHit.point - characterPosition).ProjectedOnPlane(_characterUp).sqrMagnitude;
            float standOnEdgeRadius = GetValidPerchRadius(inHit.collider);

            if (distFromCenterSq <= standOnEdgeRadius.Square())
            {
                return false;
            }

            return true;
        }


        private bool CapsuleCast(Vector3 point1, Vector3 point2, float castRadius, Vector3 castDirection,
            float castDistance, int castLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            return _queries.CapsuleCast(point1, point2, castRadius, castDirection, castDistance, castLayerMask,
                TriggerInteraction, ColliderFilter, out hitResult, out startPenetrating);
        }


        private bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 castDirection,
            float castDistance, int castLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            return _queries.BoxCast(center, halfExtents, orientation, castDirection, castDistance, castLayerMask,
                TriggerInteraction, ColliderFilter, out hitResult, out startPenetrating);
        }

        private bool GroundSweepTest(Vector3 characterPosition, float capsuleRadius, float capsuleHalfHeight,
            float sweepDistance, out RaycastHit hitResult, out bool startPenetrating)
        {
            bool foundBlockingHit;

            if (!UseFlatBaseForGroundChecks)
            {
                Vector3 characterCenter = characterPosition + _capsule.TransformedCenter;

                Vector3 point1 = characterCenter - _characterUp * (capsuleHalfHeight - capsuleRadius);
                Vector3 point2 = characterCenter + _characterUp * (capsuleHalfHeight - capsuleRadius);

                Vector3 sweepDirection = -1.0f * _characterUp;

                foundBlockingHit = CapsuleCast(point1, point2, capsuleRadius, sweepDirection, sweepDistance,
                    _collisionLayers, out hitResult, out startPenetrating);
            }
            else
            {
                Vector3 center = characterPosition + _capsule.TransformedCenter;
                Vector3 halfExtents = new Vector3(capsuleRadius * 0.707f, capsuleHalfHeight, capsuleRadius * 0.707f);

                Quaternion sweepOrientation = Rotation * Quaternion.Euler(0f, -Rotation.eulerAngles.y, 0f);
                Vector3 sweepDirection = -1.0f * _characterUp;

                LayerMask sweepLayerMask = _collisionLayers;

                foundBlockingHit = BoxCast(center, halfExtents, sweepOrientation * Quaternion.Euler(0.0f, 45.0f, 0.0f),
                    sweepDirection, sweepDistance, sweepLayerMask, out hitResult, out startPenetrating);

                if (!foundBlockingHit && !startPenetrating)
                {
                    foundBlockingHit = BoxCast(center, halfExtents, sweepOrientation, sweepDirection, sweepDistance,
                        sweepLayerMask, out hitResult, out startPenetrating);
                }
            }

            return foundBlockingHit;
        }

        public void ComputeGroundDistance(Vector3 characterPosition, float sweepRadius, float sweepDistance,
            float castDistance, out FindGroundResult outGroundResult)
        {
            outGroundResult = default;
            if (sweepDistance < castDistance)
                return;

            float characterRadius = _radius;
            float characterHeight = _height;
            float characterHalfHeight = characterHeight * 0.5f;

            bool foundGround = default;
            bool startPenetrating = default;

            if (sweepDistance > 0.0f && sweepRadius > 0.0f)
            {
                const float shrinkScale = 0.9f;
                float shrinkHeight = (characterHalfHeight - characterRadius) * (1.0f - shrinkScale);

                float capsuleRadius = sweepRadius;
                float capsuleHalfHeight = characterHalfHeight - shrinkHeight;

                float actualSweepDistance = sweepDistance + shrinkHeight;

                foundGround = GroundSweepTest(characterPosition, capsuleRadius, capsuleHalfHeight, actualSweepDistance,
                    out RaycastHit hitResult, out startPenetrating);

                if (foundGround || startPenetrating)
                {
                    if (startPenetrating || !IsWithinEdgeTolerance(characterPosition, hitResult.point, capsuleRadius))
                    {
                        const float shrinkScaleOverlap = 0.1f;
                        shrinkHeight = (characterHalfHeight - characterRadius) * (1.0f - shrinkScaleOverlap);

                        capsuleRadius = Mathf.Max(0.0011f, capsuleRadius - SWEEP_EDGE_REJECT_DISTANCE - KINDA_SMALL_NUMBER);
                        capsuleHalfHeight = Mathf.Max(capsuleRadius, characterHalfHeight - shrinkHeight);

                        actualSweepDistance = sweepDistance + shrinkHeight;

                        foundGround = GroundSweepTest(characterPosition, capsuleRadius, capsuleHalfHeight,
                            actualSweepDistance, out hitResult, out startPenetrating);
                    }

                    if (foundGround && !startPenetrating)
                    {
                        float maxPenetrationAdjust = Mathf.Max(MAX_GROUND_DISTANCE, characterRadius);
                        float sweepResult = Mathf.Max(-maxPenetrationAdjust, hitResult.distance - shrinkHeight);

                        Vector3 sweepDirection = -1.0f * _characterUp;
                        Vector3 hitPosition = characterPosition + sweepDirection * sweepResult;

                        Vector3 surfaceNormal = hitResult.normal;

                        bool isWalkable = false;
                        bool hitGround = sweepResult <= sweepDistance &&
                                         ComputeHitLocation(hitResult.normal) == EHitLocation.Below;

                        if (hitGround)
                        {
                            if (UseFlatBaseForGroundChecks)
                                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
                            else
                            {
                                surfaceNormal = FindGeomOpposingNormal(sweepDirection * sweepDistance, ref hitResult);

                                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
                            }
                        }

                        outGroundResult.SetFromSweepResult(hitGround, isWalkable, hitPosition, sweepResult,
                            ref hitResult, surfaceNormal);

                        if (outGroundResult.IsWalkableGround)
                            return;
                    }
                }
            }

            if (!foundGround && !startPenetrating)
                return;

            if (castDistance > 0.0f)
            {
                Vector3 rayOrigin = characterPosition + _capsule.TransformedCenter;
                Vector3 rayDirection = -1.0f * _characterUp;

                float shrinkHeight = characterHalfHeight;
                float rayLength = castDistance + shrinkHeight;

                foundGround = Raycast(rayOrigin, rayDirection, rayLength, _collisionLayers, out RaycastHit hitResult);

                if (foundGround && hitResult.distance > 0.0f)
                {
                    float maxPenetrationAdjust = Mathf.Max(MAX_GROUND_DISTANCE, characterRadius);
                    float castResult = Mathf.Max(-maxPenetrationAdjust, hitResult.distance - shrinkHeight);

                    if (castResult <= castDistance && IsWalkable(hitResult.collider, hitResult.normal))
                    {
                        outGroundResult.SetFromRaycastResult(true, true, outGroundResult.Position,
                            outGroundResult.GroundDistance, castResult, ref hitResult);

                        return;
                    }
                }
            }

            outGroundResult.IsWalkable = false;
        }


        private bool ComputePerchResult(Vector3 characterPosition, float testRadius, float inMaxGroundDistance,
            ref RaycastHit inHit, out FindGroundResult perchGroundResult)
        {
            perchGroundResult = default;

            if (inMaxGroundDistance <= 0.0f)
                return false;

            float inHitAboveBase = Mathf.Max(0.0f, Vector3.Dot(inHit.point - characterPosition, _characterUp));
            float perchCastDist = Mathf.Max(0.0f, inMaxGroundDistance - inHitAboveBase);
            float perchSweepDist = Mathf.Max(0.0f, inMaxGroundDistance);

            float actualSweepDist = perchSweepDist + _radius;
            ComputeGroundDistance(characterPosition, testRadius, actualSweepDist, perchCastDist, out perchGroundResult);

            if (!perchGroundResult.IsWalkable)
                return false;
            if (inHitAboveBase + perchGroundResult.GroundDistance > inMaxGroundDistance)
            {
                perchGroundResult.IsWalkable = false;
                return false;
            }

            return true;
        }


        public void FindGround(Vector3 characterPosition, out FindGroundResult outGroundResult)
        {
            if (!_detectCollisions)
            {
                outGroundResult = default;
                return;
            }

            float heightCheckAdjust = IsGrounded ? MAX_GROUND_DISTANCE + KINDA_SMALL_NUMBER : -MAX_GROUND_DISTANCE;
            float sweepDistance = Mathf.Max(MAX_GROUND_DISTANCE, StepOffset + heightCheckAdjust);

            ComputeGroundDistance(characterPosition, _radius, sweepDistance, sweepDistance, out outGroundResult);

            if (outGroundResult.HitGround && !outGroundResult.IsRaycastResult)
            {
                Vector3 positionOnGround = outGroundResult.Position;

                if (ShouldComputePerchResult(positionOnGround, ref outGroundResult.HitResult))
                {
                    float maxPerchGroundDistance = sweepDistance;
                    if (IsGrounded)
                        maxPerchGroundDistance += PerchAdditionalHeight;

                    float validPerchRadius = GetValidPerchRadius(outGroundResult.Collider);

                    if (ComputePerchResult(positionOnGround, validPerchRadius, maxPerchGroundDistance,
                        ref outGroundResult.HitResult, out FindGroundResult perchGroundResult))
                    {

                        float moveUpDist = AVG_GROUND_DISTANCE - outGroundResult.GroundDistance;
                        if (moveUpDist + perchGroundResult.GroundDistance >= maxPerchGroundDistance)
                        {
                            outGroundResult.GroundDistance = AVG_GROUND_DISTANCE;
                        }

                        if (!outGroundResult.IsWalkableGround)
                        {
                            float groundDistance = outGroundResult.GroundDistance;
                            float raycastDistance = Mathf.Max(MIN_GROUND_DISTANCE, groundDistance);

                            outGroundResult.SetFromRaycastResult(true, true, outGroundResult.Position, groundDistance,
                                raycastDistance, ref perchGroundResult.HitResult);
                        }
                    }
                    else
                    {
                        outGroundResult.IsWalkable = false;
                    }
                }
            }
        }


        private void AdjustGroundHeight()
        {
            if (!_grounding.CurrentGround.IsWalkableGround || !IsConstrainedToGround)
                return;

            float lastGroundDistance = _grounding.CurrentGround.GroundDistance;

            if (_grounding.CurrentGround.IsRaycastResult)
            {
                if (lastGroundDistance < MIN_GROUND_DISTANCE &&
                    _grounding.CurrentGround.RaycastDistance >= MIN_GROUND_DISTANCE)
                {
                    return;
                }

                lastGroundDistance = _grounding.CurrentGround.RaycastDistance;
            }

            if (lastGroundDistance < MIN_GROUND_DISTANCE || lastGroundDistance > MAX_GROUND_DISTANCE)
            {
                float initialY = Vector3.Dot(UpdatedPosition, _characterUp);
                float moveDistance = AVG_GROUND_DISTANCE - lastGroundDistance;

                Vector3 displacement = _characterUp * moveDistance;

                Vector3 sweepOrigin = UpdatedPosition;
                Vector3 sweepDirection = displacement.normalized;

                float sweepRadius = _radius;
                float sweepDistance = displacement.magnitude;

                int sweepLayerMask = _collisionLayers;

                bool hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                    out RaycastHit hitResult, out bool startPenetrating, out _, out _, true);

                if (!hit && !startPenetrating)
                {
                    UpdatedPosition += displacement;
                    _grounding.CurrentGround.GroundDistance += moveDistance;
                }
                else if (moveDistance > 0.0f)
                {
                    UpdatedPosition += sweepDirection * hitResult.distance;

                    float currentY = Vector3.Dot(UpdatedPosition, _characterUp);
                    _grounding.CurrentGround.GroundDistance += currentY - initialY;
                }
                else
                {
                    UpdatedPosition += sweepDirection * hitResult.distance;

                    float currentY = Vector3.Dot(UpdatedPosition, _characterUp);
                    _grounding.CurrentGround.GroundDistance += currentY - initialY;
                }
            }

        }


        private bool CanStepUp(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;

            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanStepOn(collisionBehaviour))
                    return true;

                if (CanNotStepOn(collisionBehaviour))
                    return false;
            }

            return true;
        }


        private bool StepUp(ref CollisionResult inCollision, out CollisionResult stepResult)
        {
            stepResult = default;

            if (inCollision.HitLocation == EHitLocation.Above)
                return false;

            float characterInitialGroundPositionY = Vector3.Dot(inCollision.Position, _characterUp);
            float groundPointY = characterInitialGroundPositionY;

            float actualGroundDistance = Mathf.Max(0.0f, _grounding.CurrentGround.GetDistanceToGround());
            characterInitialGroundPositionY -= actualGroundDistance;

            float stepTravelUpHeight = Mathf.Max(0.0f, StepOffset - actualGroundDistance);
            float stepTravelDownHeight = StepOffset + MAX_GROUND_DISTANCE * 2.0f;

            bool hitVerticalFace =
                !IsWithinEdgeTolerance(inCollision.Position, inCollision.Point, _radius + CONTACT_OFFSET);

            if (!_grounding.CurrentGround.IsRaycastResult && !hitVerticalFace)
                groundPointY = Vector3.Dot(GroundPoint, _characterUp);
            else
                groundPointY -= _grounding.CurrentGround.GroundDistance;

            float initialImpactY = Vector3.Dot(inCollision.Point, _characterUp);
            if (initialImpactY <= characterInitialGroundPositionY)
                return false;

            Vector3 sweepOrigin = inCollision.Position;
            Vector3 sweepDirection = _characterUp;

            float sweepRadius = _radius;
            float sweepDistance = stepTravelUpHeight;

            int sweepLayerMask = _collisionLayers;

            bool foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out RaycastHit hitResult, out bool startPenetrating);

            if (startPenetrating)
                return false;

            if (!foundBlockingHit)
                sweepOrigin += sweepDirection * sweepDistance;
            else
                sweepOrigin += sweepDirection * hitResult.distance;

            Vector3 displacement = inCollision.RemainingDisplacement;
            Vector3 displacement2D = ConstrainVectorToPlane(Vector3.ProjectOnPlane(displacement, _characterUp));

            sweepDistance = displacement.magnitude;
            sweepDirection = displacement2D.normalized;

            foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out hitResult, out startPenetrating);

            if (startPenetrating)
                return false;

            if (!foundBlockingHit)
                sweepOrigin += sweepDirection * sweepDistance;
            else
            {
                return false;
            }

            sweepDirection = -_characterUp;
            sweepDistance = stepTravelDownHeight;

            foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out hitResult, out startPenetrating);

            if (!foundBlockingHit || startPenetrating)
                return false;

            float deltaY = Vector3.Dot(hitResult.point, _characterUp) - groundPointY;
            if (deltaY > StepOffset)
                return false;

            Vector3 positionOnStep = sweepOrigin + sweepDirection * hitResult.distance;

            if (OverlapTest(positionOnStep, UpdatedRotation, _radius, _height, _collisionLayers, _queries.Overlaps,
                    TriggerInteraction) > 0)
                return false;

            Vector3 surfaceNormal = FindGeomOpposingNormal(sweepDirection * sweepDistance, ref hitResult);

            bool isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
            if (!isWalkable)
            {
                bool normalTowardsMe = Vector3.Dot(displacement, surfaceNormal) < 0.0f;
                if (normalTowardsMe)
                    return false;

                if (Vector3.Dot(positionOnStep, _characterUp) > Vector3.Dot(inCollision.Position, _characterUp))
                    return false;
            }

            if (!IsWithinEdgeTolerance(positionOnStep, hitResult.point, _radius + CONTACT_OFFSET))
                return false;

            if (deltaY > 0.0f && !CanStepUp(hitResult.collider))
                return false;

            stepResult = new CollisionResult
            {
                Position = positionOnStep
            };

            return true;
        }


        public void PauseGroundConstraint(float unconstrainedTime = 0.1f)
        {
            _grounding.Pause(unconstrainedTime);
        }


        private void UpdateCurrentGround(ref FindGroundResult inGroundResult)
        {
            _grounding.UpdateCurrent(ref inGroundResult);
        }

    }
}
