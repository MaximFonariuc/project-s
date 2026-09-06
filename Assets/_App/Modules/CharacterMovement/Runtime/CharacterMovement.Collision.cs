using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed partial class CharacterMovement
    {
        private void ResetCollisionFlags()
        {
            _collisions.Clear();
        }


        private EHitLocation ComputeHitLocation(Vector3 inNormal)
        {
            float verticalComponent = inNormal.Dot(_characterUp);

            if (verticalComponent > HEMISPHERE_LIMIT)
                return EHitLocation.Below;

            return verticalComponent < -HEMISPHERE_LIMIT ? EHitLocation.Above : EHitLocation.Sides;
        }

        private bool IsWalkable(Collider inCollider, Vector3 inNormal)
        {
            if (ComputeHitLocation(inNormal) != EHitLocation.Below)
                return false;

            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(inCollider);

                if (IsWalkable(collisionBehaviour))
                    return Vector3.Dot(inNormal, _characterUp) > MAX_WALKABLE_SLOPE_LIMIT;

                if (IsNotWalkable(collisionBehaviour))
                    return Vector3.Dot(inNormal, _characterUp) > MIN_WALKABLE_SLOPE_LIMIT;
            }

            float actualSlopeLimit = _minSlopeLimit;

            if (_slopeLimitOverride && inCollider.TryGetComponent(out SlopeLimitBehaviour slopeLimitOverrideComponent))
            {
                switch (slopeLimitOverrideComponent.WalkableSlopeBehaviour)
                {
                    case ESlopeBehaviourType.Walkable:
                        actualSlopeLimit = MAX_WALKABLE_SLOPE_LIMIT;
                        break;

                    case ESlopeBehaviourType.NotWalkable:
                        actualSlopeLimit = MIN_WALKABLE_SLOPE_LIMIT;
                        break;

                    case ESlopeBehaviourType.Override:
                        actualSlopeLimit = slopeLimitOverrideComponent.SlopeLimitCos;
                        break;

                    case ESlopeBehaviourType.Default:
                        break;
                }
            }

            return Vector3.Dot(inNormal, _characterUp) > actualSlopeLimit;
        }


        private Vector3 ComputeBlockingNormal(Vector3 inNormal, bool isWalkable)
        {
            if ((IsGrounded || _grounding.HasLanded) && !isWalkable)
            {
                Vector3 actualGroundNormal = _grounding.HasLanded
                    ? _grounding.FoundGround.Normal
                    : _grounding.CurrentGround.Normal;

                Vector3 forward = actualGroundNormal.PerpendicularTo(inNormal);
                Vector3 blockingNormal = forward.PerpendicularTo(_characterUp);

                if (Vector3.Dot(blockingNormal, inNormal) < 0.0f)
                    blockingNormal = -blockingNormal;

                if (!blockingNormal.IsZero())
                    inNormal = blockingNormal;

                return inNormal;
            }

            return inNormal;
        }


        private bool ShouldFilter(Collider otherCollider)
        {
            return _collisionFilter.ShouldFilter(otherCollider, ColliderFilter);
        }


        public void CapsuleIgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            if (otherCollider == null)
                return;

            _collisionFilter.SetIgnoredByPhysics(otherCollider, ignore);
        }


        public void IgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            if (otherCollider == null)
                return;

            _collisionFilter.SetIgnored(otherCollider, ignore);
        }


        public void IgnoreCollision(Rigidbody otherRigidbody, bool ignore = true)
        {
            if (otherRigidbody == null)
                return;

            _collisionFilter.SetIgnored(otherRigidbody, ignore);
        }


        private void ClearCollisionResults()
        {
            _collisions.Clear();
        }


        private void AddCollisionResult(ref CollisionResult collisionResult)
        {
            _collisions.Add(ref collisionResult, _platforms.CurrentPlatform);
        }


        public int GetCollisionCount()
        {
            return _collisions.Count;
        }


        public CollisionResult GetCollisionResult(int index)
        {
            return _collisions.Get(index);
        }


        private bool ComputeInflatedMTD(Vector3 characterPosition, Quaternion characterRotation, float mtdInflation,
            Collider hitCollider, Transform hitTransform, out Vector3 mtdDirection, out float mtdDistance)
        {
            mtdDirection = Vector3.zero;
            mtdDistance = 0.0f;

            _capsuleCollider.radius = _radius + mtdInflation * 1.0f;
            _capsuleCollider.height = _height + mtdInflation * 2.0f;

            bool mtdResult = Physics.ComputePenetration(_capsuleCollider, characterPosition, characterRotation,
                hitCollider, hitTransform.position, hitTransform.rotation, out Vector3 recoverDirection, out float recoverDistance);

            if (mtdResult)
            {
                if (IsFinite(recoverDirection))
                {
                    mtdDirection = recoverDirection;
                    mtdDistance = Mathf.Max(Mathf.Abs(recoverDistance) - mtdInflation, 0.0f) + KINDA_SMALL_NUMBER;
                }
                else
                {
                    Debug.LogWarning("Warning: ComputeInflatedMTD_Internal: MTD returned NaN " + recoverDirection.ToString("F4"));
                }
            }

            _capsuleCollider.radius = _radius;
            _capsuleCollider.height = _height;

            return mtdResult;
        }


        private bool ComputeMTD(Vector3 characterPosition, Quaternion characterRotation, Collider hitCollider, Transform hitTransform, out Vector3 mtdDirection, out float mtdDistance)
        {
            const float smallMtdInflation = 0.0025f;
            const float largeMtdInflation = 0.0175f;

            if (ComputeInflatedMTD(characterPosition, characterRotation, smallMtdInflation, hitCollider, hitTransform, out mtdDirection, out mtdDistance) ||
                ComputeInflatedMTD(characterPosition, characterRotation, largeMtdInflation, hitCollider, hitTransform, out mtdDirection, out mtdDistance))
            {
                return true;
            }

            return false;
        }


        private void ResolveOverlaps(EDepenetrationBehaviour depenetrationBehaviour = EDepenetrationBehaviour.IgnoreNone)
        {
            if (!DetectCollisions)
                return;

            bool ignoreStatic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreStatic) != 0;
            bool ignoreDynamic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreDynamic) != 0;
            bool ignoreKinematic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreKinematic) != 0;

            for (int i = 0; i < _advanced.MaxDepenetrationIterations; i++)
            {
                Vector3 top = UpdatedPosition + _capsule.TransformedTopCenter;
                Vector3 bottom = UpdatedPosition + _capsule.TransformedBottomCenter;

                int overlapCount = _queries.OverlapCapsule(bottom, top, _radius, _collisionLayers, _queries.Overlaps,
                    TriggerInteraction, ColliderFilter);
                if (overlapCount == 0)
                    break;

                for (int j = 0; j < overlapCount; j++)
                {
                    Collider overlappedCollider = _queries.Overlaps[j];

                    if (ShouldFilter(overlappedCollider))
                        continue;

                    Rigidbody attachedRigidbody = overlappedCollider.attachedRigidbody;

                    if (ignoreStatic && attachedRigidbody == null)
                        continue;

                    if (attachedRigidbody)
                    {
                        bool isKinematic = attachedRigidbody.isKinematic;

                        if (ignoreKinematic && isKinematic)
                            continue;

                        if (ignoreDynamic && !isKinematic)
                            continue;
                    }

                    if (ComputeMTD(UpdatedPosition, UpdatedRotation, overlappedCollider, overlappedCollider.transform, out Vector3 recoverDirection, out float recoverDistance))
                    {
                        recoverDirection = ConstrainDirectionToPlane(recoverDirection);

                        EHitLocation hitLocation = ComputeHitLocation(recoverDirection);

                        bool isWalkable = IsWalkable(overlappedCollider, recoverDirection);

                        Vector3 impactNormal = ComputeBlockingNormal(recoverDirection, isWalkable);

                        UpdatedPosition += impactNormal * (recoverDistance + PENETRATION_OFFSET);

                        Vector3 point;

                        if (hitLocation == EHitLocation.Above)
                            point = UpdatedPosition + _capsule.TransformedTopCenter - recoverDirection * _radius;
                        else if (hitLocation == EHitLocation.Below)
                            point = UpdatedPosition + _capsule.TransformedBottomCenter - recoverDirection * _radius;
                        else
                            point = UpdatedPosition + _capsule.TransformedCenter - recoverDirection * _radius;

                        CollisionResult collisionResult = new CollisionResult
                        {
                            StartPenetrating = true,

                            HitLocation = hitLocation,
                            IsWalkable = isWalkable,

                            Position = UpdatedPosition,

                            Velocity = _velocity,
                            OtherVelocity = GetRigidbodyVelocity(attachedRigidbody, point),

                            Point = point,
                            Normal = impactNormal,

                            SurfaceNormal = impactNormal,

                            Collider = overlappedCollider
                        };

                        AddCollisionResult(ref collisionResult);
                    }
                }
            }
        }


        public int OverlapTest(Vector3 characterPosition, Quaternion characterRotation, float testRadius,
            float testHeight, int layerMask, Collider[] results, QueryTriggerInteraction queryTriggerInteraction)
        {
            CharacterCapsuleShape.Calculate(testRadius, testHeight, out Vector3 _, out Vector3 bottomCenter,
                out Vector3 topCenter);

            Vector3 top = characterPosition + characterRotation * topCenter;
            Vector3 bottom = characterPosition + characterRotation * bottomCenter;

            return _queries.OverlapCapsule(bottom, top, testRadius, layerMask, results, queryTriggerInteraction,
                ColliderFilter);
        }


        public Collider[] OverlapTest(Vector3 characterPosition, Quaternion characterRotation, float testRadius,
            float testHeight, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out int overlapCount)
        {
            overlapCount = OverlapTest(characterPosition, characterRotation, testRadius, testHeight, layerMask,
                _queries.Overlaps, queryTriggerInteraction);

            return _queries.Overlaps;
        }


        public Collider[] OverlapTest(int layerMask, QueryTriggerInteraction queryTriggerInteraction,
            out int overlapCount)
        {
            overlapCount =
                OverlapTest(Position, Rotation, Radius, Height, layerMask, _queries.Overlaps, queryTriggerInteraction);

            return _queries.Overlaps;
        }


        public bool CheckCapsule()
        {
            IgnoreCollision(_platforms.CurrentPlatform);

            int overlapCount =
                OverlapTest(Position, Rotation, Radius, Height, CollisionLayers, _queries.Overlaps, TriggerInteraction);

            IgnoreCollision(_platforms.CurrentPlatform, false);

            return overlapCount > 0;
        }


        public bool CheckHeight(float testHeight)
        {
            testHeight = Mathf.Max(testHeight, _radius * 2.0f);
            float heightDelta = testHeight - _height;
            if (heightDelta <= KINDA_SMALL_NUMBER)
                return false;

            IgnoreCollision(_platforms.CurrentPlatform);

            CharacterCapsuleShape.Calculate(
                _radius,
                _height,
                out Vector3 _,
                out Vector3 _,
                out Vector3 topCenter);

            Vector3 worldUp = Rotation * Vector3.up;
            Vector3 worldTopCenter = Position + Rotation * topCenter;
            float sweepRadius = Mathf.Max(0.0f, _radius - SMALL_CONTACT_OFFSET);

            bool blocked = _queries.CapsuleCast(
                worldTopCenter,
                worldTopCenter,
                sweepRadius,
                worldUp,
                heightDelta,
                CollisionLayers,
                TriggerInteraction,
                ColliderFilter,
                out RaycastHit _,
                out bool startPenetrating);

            IgnoreCollision(_platforms.CurrentPlatform, false);

            return blocked || startPenetrating;
        }

        public bool IsWithinEdgeTolerance(Vector3 characterPosition, Vector3 inPoint, float testRadius)
        {
            float distFromCenterSq = (inPoint - characterPosition).ProjectedOnPlane(_characterUp).sqrMagnitude;

            float reducedRadius = Mathf.Max(SWEEP_EDGE_REJECT_DISTANCE + KINDA_SMALL_NUMBER,
                testRadius - SWEEP_EDGE_REJECT_DISTANCE);

            return distFromCenterSq < reducedRadius * reducedRadius;
        }


        private bool ShouldCheckForValidLandingSpot(ref CollisionResult inCollision)
        {
            if (inCollision.HitLocation == EHitLocation.Below && inCollision.Normal != inCollision.SurfaceNormal)
            {
                if (IsWithinEdgeTolerance(UpdatedPosition, inCollision.Point, _radius))
                    return true;
            }

            return false;
        }


        private bool IsValidLandingSpot(Vector3 characterPosition, ref CollisionResult inCollision)
        {
            if (!inCollision.IsWalkable)
                return false;

            if (inCollision.HitLocation != EHitLocation.Below)
                return false;

            if (!IsWithinEdgeTolerance(characterPosition, inCollision.Point, _radius))
            {
                inCollision.IsWalkable = false;

                return false;
            }

            FindGround(characterPosition, out FindGroundResult groundResult);
            {
                inCollision.IsWalkable = groundResult.IsWalkableGround;

                if (inCollision.IsWalkable)
                {
                    _grounding.FoundGround = groundResult;

                    return true;
                }
            }

            return false;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask, out RaycastHit hitResult,
            float thickness = 0.0f)
        {
            return _queries.Raycast(origin, direction, distance, layerMask, TriggerInteraction, ColliderFilter,
                out hitResult, thickness);
        }

        private bool CapsuleCast(Vector3 characterPosition, float castRadius, Vector3 castDirection, float castDistance,
            int layerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = false;

            Vector3 top = characterPosition + _capsule.TransformedTopCenter;
            Vector3 bottom = characterPosition + _capsule.TransformedBottomCenter;

            return _queries.CapsuleCast(bottom, top, castRadius, castDirection, castDistance, layerMask,
                TriggerInteraction, ColliderFilter, out hitResult, out startPenetrating);
        }

        private bool CapsuleCastEx(Vector3 characterPosition, float castRadius, Vector3 castDirection, float castDistance, int layerMask,
            out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, bool ignoreNonBlockingOverlaps = false)
        {
            hitResult = default;

            startPenetrating = default;
            recoverDirection = default;
            recoverDistance = default;

            Vector3 top = characterPosition + _capsule.TransformedTopCenter;
            Vector3 bottom = characterPosition + _capsule.TransformedBottomCenter;

            int rawHitCount = _queries.CapsuleCastRaw(bottom, top, castRadius, castDirection, castDistance, layerMask,
                TriggerInteraction);

            if (rawHitCount == 0)
                return false;

            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _queries.Hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                bool isOverlapping = hit.distance <= 0.0f;
                if (isOverlapping)
                {
                    if (ComputeMTD(characterPosition, UpdatedRotation, hit.collider, hit.collider.transform, out Vector3 mtdDirection, out float mtdDistance))
                    {
                        mtdDirection = ConstrainDirectionToPlane(mtdDirection);

                        EHitLocation hitLocation = ComputeHitLocation(mtdDirection);

                        Vector3 point;
                        if (hitLocation == EHitLocation.Above)
                            point = characterPosition + _capsule.TransformedTopCenter - mtdDirection * _radius;
                        else if (hitLocation == EHitLocation.Below)
                            point = characterPosition + _capsule.TransformedBottomCenter - mtdDirection * _radius;
                        else
                            point = characterPosition + _capsule.TransformedCenter - mtdDirection * _radius;

                        Vector3 impactNormal = ComputeBlockingNormal(mtdDirection, IsWalkable(hit.collider, mtdDirection));

                        hit.point = point;
                        hit.normal = impactNormal;
                        hit.distance = -mtdDistance;
                    }
                }
            }

            if (rawHitCount > 2)
            {
                _queries.SortHitsByDistance(rawHitCount);
            }

            float mostOpposingDot = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _queries.Hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                bool isOverlapping = hit.distance <= 0.0f && !hit.point.IsZero();
                if (isOverlapping)
                {
                    float movementDotNormal = Vector3.Dot(castDirection, hit.normal);

                    if (ignoreNonBlockingOverlaps)
                    {
                        bool isMovingOut = movementDotNormal > 0.0f;
                        if (isMovingOut)
                            continue;
                    }

                    if (movementDotNormal < mostOpposingDot)
                    {
                        mostOpposingDot = movementDotNormal;
                        hitIndex = i;
                    }
                }
                else if (hitIndex == -1)
                {
                    hitIndex = i;
                    break;
                }
            }

            if (hitIndex >= 0)
            {
                hitResult = _queries.Hits[hitIndex];

                if (hitResult.distance <= 0.0f)
                {
                    startPenetrating = true;
                    recoverDirection = hitResult.normal;
                    recoverDistance = Mathf.Abs(hitResult.distance);
                }

                return true;
            }

            return false;
        }


        private bool SweepTest(Vector3 sweepOrigin, float sweepRadius, Vector3 sweepDirection, float sweepDistance,
            int sweepLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;

            bool innerCapsuleHit =
                CapsuleCast(sweepOrigin, sweepRadius, sweepDirection, sweepDistance + sweepRadius, sweepLayerMask,
                    out RaycastHit innerCapsuleHitResult, out startPenetrating) && innerCapsuleHitResult.distance <= sweepDistance;

            float outerCapsuleRadius = sweepRadius + CONTACT_OFFSET;

            bool outerCapsuleHit =
                CapsuleCast(sweepOrigin, outerCapsuleRadius, sweepDirection, sweepDistance + outerCapsuleRadius,
                    sweepLayerMask, out RaycastHit outerCapsuleHitResult, out _) && outerCapsuleHitResult.distance <= sweepDistance;

            bool foundBlockingHit = innerCapsuleHit || outerCapsuleHit;
            if (!foundBlockingHit)
                return false;

            if (!outerCapsuleHit)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else if (innerCapsuleHit && innerCapsuleHitResult.distance < outerCapsuleHitResult.distance)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else
            {
                hitResult = outerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);
            }

            return true;
        }


        private bool SweepTestEx(Vector3 sweepOrigin, float sweepRadius, Vector3 sweepDirection, float sweepDistance, int sweepLayerMask,
            out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, bool ignoreBlockingOverlaps = false)
        {
            hitResult = default;

            bool innerCapsuleHit =
                CapsuleCastEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance + sweepRadius, sweepLayerMask,
                out RaycastHit innerCapsuleHitResult, out startPenetrating, out recoverDirection, out recoverDistance, ignoreBlockingOverlaps) && innerCapsuleHitResult.distance <= sweepDistance;

            if (innerCapsuleHit && startPenetrating)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);

                return true;
            }

            float outerCapsuleRadius = sweepRadius + CONTACT_OFFSET;

            bool outerCapsuleHit =
                CapsuleCast(sweepOrigin, outerCapsuleRadius, sweepDirection, sweepDistance + outerCapsuleRadius, sweepLayerMask,
                out RaycastHit outerCapsuleHitResult, out _) && outerCapsuleHitResult.distance <= sweepDistance;

            bool foundBlockingHit = innerCapsuleHit || outerCapsuleHit;
            if (!foundBlockingHit)
                return false;

            if (!outerCapsuleHit)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else if (innerCapsuleHit && innerCapsuleHitResult.distance < outerCapsuleHitResult.distance)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else
            {
                hitResult = outerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);
            }

            return true;
        }


        private bool ResolvePenetration(Vector3 displacement, Vector3 proposedAdjustment)
        {
            Vector3 adjustment = ConstrainVectorToPlane(proposedAdjustment);
            if (adjustment.IsZero())
                return false;

            const float overlapInflation = 0.001f;

            if (!(OverlapTest(UpdatedPosition + adjustment, UpdatedRotation, _radius + overlapInflation, _height,
                    _collisionLayers, _queries.Overlaps, TriggerInteraction) > 0))
            {
                UpdatedPosition += adjustment;

                return true;
            }

            Vector3 lastPosition = UpdatedPosition;

            bool hit = CapsuleCastEx(UpdatedPosition, _radius, adjustment.normalized, adjustment.magnitude, _collisionLayers,
                out RaycastHit sweepHitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, true);

            if (!hit)
                UpdatedPosition += adjustment;
            else
                UpdatedPosition += adjustment.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);


            bool moved = UpdatedPosition != lastPosition;
            if (!moved && startPenetrating)
            {
                Vector3 secondMtd = recoverDirection * (recoverDistance + CONTACT_OFFSET + PENETRATION_OFFSET);
                Vector3 combinedMtd = adjustment + secondMtd;

                if (secondMtd != adjustment && !combinedMtd.IsZero())
                {
                    lastPosition = UpdatedPosition;

                    hit = CapsuleCastEx(UpdatedPosition, _radius, combinedMtd.normalized, combinedMtd.magnitude,
                        _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                    if (!hit)
                        UpdatedPosition += combinedMtd;
                    else
                        UpdatedPosition += combinedMtd.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                    moved = UpdatedPosition != lastPosition;
                }
            }

            if (!moved)
            {
                Vector3 moveDelta = ConstrainVectorToPlane(displacement);
                if (!moveDelta.IsZero())
                {
                    lastPosition = UpdatedPosition;

                    Vector3 newAdjustment = adjustment + moveDelta;
                    hit = CapsuleCastEx(UpdatedPosition, _radius, newAdjustment.normalized, newAdjustment.magnitude,
                        _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                    if (!hit)
                        UpdatedPosition += newAdjustment;
                    else
                        UpdatedPosition += newAdjustment.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                    moved = UpdatedPosition != lastPosition;

                    if (!moved && Vector3.Dot(moveDelta, adjustment) > 0.0f)
                    {
                        lastPosition = UpdatedPosition;

                        hit = CapsuleCastEx(UpdatedPosition, _radius, moveDelta.normalized, moveDelta.magnitude,
                            _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                        if (!hit)
                            UpdatedPosition += moveDelta;
                        else
                            UpdatedPosition += moveDelta.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                        moved = UpdatedPosition != lastPosition;
                    }
                }
            }

            return moved;
        }


        private bool MovementSweepTest(Vector3 characterPosition, Vector3 inVelocity, Vector3 displacement,
            out CollisionResult collisionResult)
        {
            collisionResult = default;

            Vector3 sweepOrigin = characterPosition;
            Vector3 sweepDirection = displacement.normalized;

            float sweepRadius = _radius;
            float sweepDistance = displacement.magnitude;

            int sweepLayerMask = _collisionLayers;

            bool hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance);

            if (startPenetrating)
            {
                Vector3 requestedAdjustment = recoverDirection * (recoverDistance + CONTACT_OFFSET + PENETRATION_OFFSET);

                if (ResolvePenetration(displacement, requestedAdjustment))
                {
                    sweepOrigin = UpdatedPosition;
                    hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                        out hitResult, out startPenetrating, out _, out _);
                }
            }

            if (!hit)
                return false;

            EHitLocation hitLocation = ComputeHitLocation(hitResult.normal);

            Vector3 displacementToHit = sweepDirection * hitResult.distance;
            Vector3 remainingDisplacement = displacement - displacementToHit;

            Vector3 hitPosition = sweepOrigin + displacementToHit;

            Vector3 surfaceNormal = hitResult.normal;

            bool isWalkable = false;
            bool hitGround = hitLocation == EHitLocation.Below;

            if (hitGround)
            {
                surfaceNormal = FindGeomOpposingNormal(displacement, ref hitResult);

                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
            }

            collisionResult = new CollisionResult
            {
                StartPenetrating = startPenetrating,

                HitLocation = hitLocation,
                IsWalkable = isWalkable,

                Position = hitPosition,

                Velocity = inVelocity,
                OtherVelocity = GetRigidbodyVelocity(hitResult.rigidbody, hitResult.point),

                Point = hitResult.point,
                Normal = hitResult.normal,

                SurfaceNormal = surfaceNormal,

                DisplacementToHit = displacementToHit,
                RemainingDisplacement = remainingDisplacement,

                Collider = hitResult.collider,

                HitResult = hitResult
            };

            return true;
        }


        public bool MovementSweepTest(Vector3 characterPosition, Vector3 sweepDirection, float sweepDistance,
            out CollisionResult collisionResult)
        {
            return MovementSweepTest(characterPosition, Velocity, sweepDirection * sweepDistance, out collisionResult);
        }


        private Vector3 HandleSlopeBoosting(Vector3 slideResult, Vector3 displacement, Vector3 inNormal)
        {
            Vector3 result = slideResult;

            float yResult = Vector3.Dot(result, _characterUp);
            if (yResult > 0.0f)
            {
                float yLimit = Vector3.Dot(displacement, _characterUp);
                if (yResult - yLimit > KINDA_SMALL_NUMBER)
                {
                    if (yLimit > 0.0f)
                    {
                        float upPercent = yLimit / yResult;
                        result *= upPercent;
                    }
                    else
                    {
                        result = Vector3.zero;
                    }

                    Vector3 lateralRemainder = (slideResult - result).ProjectedOnPlane(_characterUp);
                    Vector3 lateralNormal = inNormal.ProjectedOnPlane(_characterUp).normalized;
                    Vector3 adjust = lateralRemainder.ProjectedOnPlane(lateralNormal);

                    result += adjust;
                }
            }

            return result;
        }


        private Vector3 ComputeSlideVector(Vector3 displacement, Vector3 inNormal, bool isWalkable)
        {
            if (IsGrounded)
            {
                if (isWalkable)
                    displacement = displacement.TangentTo(inNormal, _characterUp);
                else
                {
                    Vector3 right = inNormal.PerpendicularTo(GroundNormal);
                    Vector3 up = right.PerpendicularTo(inNormal);

                    displacement = displacement.ProjectedOnPlane(inNormal);
                    displacement = displacement.TangentTo(up, _characterUp);
                }
            }
            else
            {
                if (isWalkable)
                {
                    if (_grounding.ConstrainToGround)
                        displacement = displacement.ProjectedOnPlane(_characterUp);

                    displacement = displacement.ProjectedOnPlane(inNormal);
                }
                else
                {
                    Vector3 slideResult = displacement.ProjectedOnPlane(inNormal);

                    if (_grounding.ConstrainToGround)
                        slideResult = HandleSlopeBoosting(slideResult, displacement, inNormal);

                    displacement = slideResult;
                }
            }

            return ConstrainVectorToPlane(displacement);
        }


        private int SlideAlongSurface(int iteration, Vector3 inputDisplacement, ref Vector3 inVelocity,
            ref Vector3 displacement, ref CollisionResult inHit, ref Vector3 prevNormal)
        {
            if (UseFlatTop && inHit.HitLocation == EHitLocation.Above)
            {
                Vector3 surfaceNormal = FindBoxOpposingNormal(displacement, inHit.Normal, inHit.Transform);

                if (inHit.Normal != surfaceNormal)
                {
                    inHit.Normal = surfaceNormal;
                    inHit.SurfaceNormal = surfaceNormal;
                }
            }

            inHit.Normal = ComputeBlockingNormal(inHit.Normal, inHit.IsWalkable);

            if (inHit.IsWalkable && IsConstrainedToGround)
            {
                inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, true);
                displacement = ComputeSlideVector(displacement, inHit.Normal, true);
            }
            else
            {
                if (iteration == 0)
                {
                    inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, inHit.IsWalkable);
                    displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);

                    iteration++;
                }
                else if (iteration == 1)
                {
                    Vector3 crease = prevNormal.PerpendicularTo(inHit.Normal);

                    Vector3 oVel = inputDisplacement.ProjectedOnPlane(crease);

                    Vector3 nVel = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                            nVel = nVel.ProjectedOnPlane(crease);

                    if (oVel.Dot(nVel) <= 0.0f || prevNormal.Dot(inHit.Normal) < 0.0f)
                    {
                        inVelocity = ConstrainVectorToPlane(inVelocity.ProjectedOn(crease));
                        displacement = ConstrainVectorToPlane(displacement.ProjectedOn(crease));

                        ++iteration;
                    }
                    else
                    {
                        inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, inHit.IsWalkable);
                        displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    }
                }
                else
                {
                    inVelocity = Vector3.zero;
                    displacement = Vector3.zero;
                }

                prevNormal = inHit.Normal;
            }

            return iteration;
        }

        private void PerformMovement(float deltaTime)
        {
            EDepenetrationBehaviour depenetrationFlags = !EnablePhysicsInteraction
                ? EDepenetrationBehaviour.IgnoreDynamic
                : EDepenetrationBehaviour.IgnoreNone;

            ResolveOverlaps(depenetrationFlags);

            if (IsGrounded)
                _velocity = _velocity.ProjectedOnPlane(_characterUp);

            Vector3 displacement = _velocity * deltaTime;

            if (IsGrounded)
            {
                displacement = displacement.TangentTo(GroundNormal, _characterUp);
                displacement = ConstrainVectorToPlane(displacement);
            }

            Vector3 inputDisplacement = displacement;

            int iteration = 0;
            Vector3 prevNormal = default;

            for (int i = 0; i < _collisions.Count; i++)
            {
                ref CollisionResult collisionResult = ref _collisions[i];

                bool opposesMovement = displacement.Dot(collisionResult.Normal) < 0.0f;
                if (!opposesMovement)
                    continue;

                if (IsConstrainedToGround && !IsOnWalkableGround)
                {
                    if (IsValidLandingSpot(UpdatedPosition, ref collisionResult))
                    {
                        _grounding.RecordLanding(collisionResult.Velocity);
                    }
                    else
                    {
                        if (collisionResult.HitLocation == EHitLocation.Below)
                        {
                            FindGround(UpdatedPosition, out FindGroundResult groundResult);

                            collisionResult.IsWalkable = groundResult.IsWalkableGround;
                            if (collisionResult.IsWalkable)
                            {
                                _grounding.FoundGround = groundResult;
                                _grounding.RecordLanding(collisionResult.Velocity);
                            }
                        }
                    }

                    if (!_grounding.HasLanded && collisionResult.HitLocation == EHitLocation.Below)
                    {
                        _grounding.FoundGround.SetFromSweepResult(true, false, UpdatedPosition, collisionResult.Point,
                            collisionResult.Normal, collisionResult.SurfaceNormal, collisionResult.Collider,
                            collisionResult.HitResult.distance);
                    }
                }

                iteration = SlideAlongSurface(iteration, inputDisplacement, ref _velocity, ref displacement,
                    ref collisionResult, ref prevNormal);
            }

            int maxSlideCount = _advanced.MaxMovementIterations;
            bool canApplyRemainingDisplacement = !DetectCollisions;
            while (DetectCollisions && maxSlideCount-- > 0 && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
            {
                bool collided = MovementSweepTest(UpdatedPosition, _velocity, displacement,
                    out CollisionResult collisionResult);

                if (!collided)
                {
                    canApplyRemainingDisplacement = true;
                    break;
                }

                UpdatedPosition += collisionResult.DisplacementToHit;

                displacement = collisionResult.RemainingDisplacement;

                if (IsGrounded && !collisionResult.IsWalkable)
                {
                    if (CanStepUp(collisionResult.Collider) &&
                        StepUp(ref collisionResult, out CollisionResult stepResult))
                    {
                        UpdatedPosition = stepResult.Position;

                        displacement = Vector3.zero;
                        break;
                    }
                }

                if (IsConstrainedToGround && !IsOnWalkableGround)
                {
                    if (IsValidLandingSpot(UpdatedPosition, ref collisionResult))
                    {
                        _grounding.RecordLanding(collisionResult.Velocity);
                    }
                    else
                    {

                        if (ShouldCheckForValidLandingSpot(ref collisionResult))
                        {
                            FindGround(UpdatedPosition, out FindGroundResult groundResult);

                            collisionResult.IsWalkable = groundResult.IsWalkableGround;
                            if (collisionResult.IsWalkable)
                            {
                                _grounding.FoundGround = groundResult;
                                _grounding.RecordLanding(collisionResult.Velocity);
                            }
                        }
                    }

                    if (!_grounding.HasLanded && collisionResult.HitLocation == EHitLocation.Below)
                    {
                        float sweepDistance = collisionResult.HitResult.distance;
                        Vector3 surfaceNormal = collisionResult.SurfaceNormal;

                        _grounding.FoundGround.SetFromSweepResult(true, false, UpdatedPosition, sweepDistance,
                            ref collisionResult.HitResult, surfaceNormal);
                    }
                }

                iteration = SlideAlongSurface(iteration, inputDisplacement, ref _velocity, ref displacement,
                    ref collisionResult, ref prevNormal);

                AddCollisionResult(ref collisionResult);
            }

            if (canApplyRemainingDisplacement && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
                UpdatedPosition += displacement;

            if (IsGrounded || _grounding.HasLanded)
            {
                _velocity = _velocity.ProjectedOnPlane(_characterUp).normalized * _velocity.magnitude;
                _velocity = ConstrainVectorToPlane(_velocity);
            }
        }

    }
}
