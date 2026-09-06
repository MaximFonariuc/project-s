using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed partial class CharacterMovement
    {
        private int SlideAlongSurface(int iteration, Vector3 inputDisplacement, ref Vector3 displacement,
            ref CollisionResult inHit, ref Vector3 prevNormal)
        {
            inHit.Normal = ComputeBlockingNormal(inHit.Normal, inHit.IsWalkable);

            if (inHit.IsWalkable && IsConstrainedToGround)
                displacement = ComputeSlideVector(displacement, inHit.Normal, true);
            else
            {
                if (iteration == 0)
                {
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
                        displacement = ConstrainVectorToPlane(displacement.ProjectedOn(crease));
                        ++iteration;
                    }
                    else
                    {
                        displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    }
                }
                else
                {
                    displacement = Vector3.zero;
                }

                prevNormal = inHit.Normal;
            }

            return iteration;
        }


        private void MoveAndSlide(Vector3 displacement)
        {
            Vector3 inputDisplacement = displacement;

            int iteration = default;
            Vector3 prevNormal = default;

            int maxSlideCount = _advanced.MaxMovementIterations;
            bool canApplyRemainingDisplacement = false;
            while (maxSlideCount-- > 0 && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
            {
                bool collided = MovementSweepTest(UpdatedPosition, default, displacement, out CollisionResult collisionResult);
                if (!collided)
                {
                    canApplyRemainingDisplacement = true;
                    break;
                }


                UpdatedPosition += collisionResult.DisplacementToHit;

                displacement = collisionResult.RemainingDisplacement;

                iteration = SlideAlongSurface(iteration, inputDisplacement, ref displacement, ref collisionResult, ref prevNormal);

                AddCollisionResult(ref collisionResult);
            }

            if (canApplyRemainingDisplacement && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
                UpdatedPosition += displacement;
        }


        private bool CanRideOn(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;

            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanRideOn(collisionBehaviour) && otherCollider.attachedRigidbody)
                    return true;

                if (CanNotRideOn(collisionBehaviour) && otherCollider.attachedRigidbody)
                    return false;
            }

            return otherCollider.attachedRigidbody;
        }


        private void IgnoreCurrentPlatform(bool ignore)
        {
            IgnoreCollision(_platforms.CurrentPlatform, ignore);
        }


        public void AttachTo(Rigidbody parent)
        {
            _platforms.AttachTo(parent);
        }


        private void UpdateCurrentPlatform()
        {
            _platforms.RefreshAttachment(UpdatedPosition, UpdatedRotation, IsGrounded, GroundCollider, CanRideOn);
        }


        private void UpdatePlatformMovement(float deltaTime)
        {
            Vector3 platformVelocity = _platforms.CalculateVelocity(deltaTime, UpdatedRotation, _characterUp,
                ImpartPlatformRotation, out Quaternion platformRotation, out Vector3 previousPlatformVelocity);

            UpdatedRotation = platformRotation;

            if (ImpartPlatformMovement && platformVelocity.sqrMagnitude > 0.0f)
            {
                if (FastPlatformMove)
                    UpdatedPosition += platformVelocity * deltaTime;
                else
                {
                    IgnoreCurrentPlatform(true);

                    MoveAndSlide(platformVelocity * deltaTime);

                    IgnoreCurrentPlatform(false);
                }
            }

            _platforms.ApplyVelocityTransition(ref _velocity, previousPlatformVelocity, ImpartPlatformVelocity);
        }


        private void ComputeDynamicCollisionResponse(ref CollisionResult inCollisionResult,
            out Vector3 characterImpulse, out Vector3 otherImpulse)
        {
            characterImpulse = default;
            otherImpulse = default;

            float massRatio = 0.0f;

            Rigidbody otherRigidbody = inCollisionResult.Rigidbody;
            if (!otherRigidbody.isKinematic || otherRigidbody.TryGetComponent(out CharacterMovement _))
            {
                float mass = Rigidbody.mass;
                massRatio = mass / (mass + inCollisionResult.Rigidbody.mass);
            }

            Vector3 normal = inCollisionResult.Normal;

            float velocityDotNormal = Vector3.Dot(inCollisionResult.Velocity, normal);
            float otherVelocityDotNormal = Vector3.Dot(inCollisionResult.OtherVelocity, normal);

            if (velocityDotNormal < 0.0f)
                characterImpulse += velocityDotNormal * normal;

            if (otherVelocityDotNormal > velocityDotNormal)
            {
                Vector3 relVel = (otherVelocityDotNormal - velocityDotNormal) * normal;

                characterImpulse += relVel * (1.0f - massRatio);
                otherImpulse -= relVel * massRatio;
            }
        }


        private void ResolveDynamicCollisions()
        {
            if (!EnablePhysicsInteraction)
                return;

            for (int i = 0; i < _collisions.Count; i++)
            {
                ref CollisionResult collisionResult = ref _collisions[i];
                if (collisionResult.IsWalkable)
                    continue;

                Rigidbody otherRigidbody = collisionResult.Rigidbody;
                if (otherRigidbody == null)
                    continue;

                ComputeDynamicCollisionResponse(ref collisionResult, out Vector3 characterImpulse, out Vector3 otherImpulse);

                CollisionResponse?.Invoke(ref collisionResult, ref characterImpulse, ref otherImpulse);

                if (otherRigidbody.TryGetComponent(out CharacterMovement otherCharacter))
                {
                    if (PhysicsInteractionAffectsCharacters)
                    {
                        Velocity += characterImpulse;
                        otherCharacter.Velocity += otherImpulse * PushForceScale;
                    }
                }
                else
                {
                    _velocity += characterImpulse;

                    if (!otherRigidbody.isKinematic)
                    {
                        otherRigidbody.AddForceAtPosition(otherImpulse * PushForceScale, collisionResult.Point,
                            ForceMode.VelocityChange);
                    }
                }
            }

            if (IsGrounded)
                _velocity = _velocity.ProjectedOnPlane(_characterUp).normalized * _velocity.magnitude;

            _velocity = ConstrainVectorToPlane(_velocity);
        }

    }
}
