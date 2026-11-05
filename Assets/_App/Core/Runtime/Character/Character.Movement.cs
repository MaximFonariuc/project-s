using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public partial class Character
    {
        public void SetMovementMode(EMovementMode newMovementMode, int newCustomMode = 0)
        {
            if (newMovementMode == _movementMode)
            {
                if (newMovementMode != EMovementMode.Custom || newCustomMode == _customMovementMode)
                    return;
            }

            EMovementMode prevMovementMode = _movementMode;
            int prevCustomMode = _customMovementMode;

            _movementMode = newMovementMode;
            _customMovementMode = newCustomMode;

            _footIK?.EnableIK(!(newMovementMode == EMovementMode.Custom && newCustomMode == (int)ECustomMovementMode.Climbing));

            OnMovementModeChanged(prevMovementMode, prevCustomMode);
        }

        protected virtual void OnMovementModeChanged(EMovementMode prevMovementMode, int prevCustomMode)
        {
            switch (MovementMode)
            {
                case EMovementMode.None:
                    CharacterMovement.Velocity = Vector3.zero;
                    CharacterMovement.ClearAccumulatedForces();
                    break;

                case EMovementMode.Walking:
                    ResetJumpState();
                    if (prevMovementMode == EMovementMode.Flying || prevMovementMode == EMovementMode.Swimming)
                    {
                        CharacterMovement.ConstrainToGround = true;
                    }
                    if (prevMovementMode == EMovementMode.Falling)
                    {
                        OnLanded(CharacterMovement.LandedVelocity);
                    }
                    break;

                case EMovementMode.Falling:
                    if (prevMovementMode == EMovementMode.Flying || prevMovementMode == EMovementMode.Swimming)
                    {
                        CharacterMovement.ConstrainToGround = true;
                    }
                    break;

                case EMovementMode.Flying:
                case EMovementMode.Swimming:
                    ResetJumpState();
                    CharacterMovement.ConstrainToGround = false;
                    break;
            }

            if (!IsFalling())
            {
                _fallingTime = 0.0f;
            }

            InvokeMovementModeChangedEvent(prevMovementMode, prevCustomMode);
        }

        public virtual bool CanAttack()
        {
            return (IsWalking() || IsFalling()) && !IsCrouched() && !IsDodging();
        }

        public virtual bool IsWalking()
        {
            return _movementMode == EMovementMode.Walking;
        }

        public virtual bool IsFalling()
        {
            return _movementMode == EMovementMode.Falling;
        }

        public virtual bool IsFlying()
        {
            return _movementMode == EMovementMode.Flying;
        }

        public virtual bool IsSwimming()
        {
            return _movementMode == EMovementMode.Swimming;
        }

        public virtual bool IsDodging()
        {
            return _movementMode == EMovementMode.Custom && _customMovementMode == (int)ECustomMovementMode.Dodging;
        }

        public virtual bool IsAirDashing()
        {
            return _movementMode == EMovementMode.Custom && _customMovementMode == (int)ECustomMovementMode.AirDashing;
        }

        public virtual bool IsClimbing()
        {
            return _movementMode == EMovementMode.Custom && _customMovementMode == (int)ECustomMovementMode.Climbing;
        }

        public virtual float GetMaxSpeed()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:

                    float baseSpeed = IsCrouched() ? MaxWalkSpeedCrouched : MaxWalkSpeed;

                    Vector3 movementDirection = GetMovementDirection();
                    Vector3 forwardDirection = GetForwardVector();

                    if (Vector3.Dot(movementDirection.normalized, forwardDirection.normalized) < 0)
                    {
                        baseSpeed *= 0.75f;
                    }

                    return baseSpeed;

                case EMovementMode.Falling:
                    return MaxWalkSpeed;

                case EMovementMode.Swimming:
                    return MaxSwimSpeed;

                case EMovementMode.Flying:
                    return MaxFlySpeed;

                default:
                    return 1.0f;
            }
        }

        public virtual float GetMinAnalogSpeed()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:
                case EMovementMode.Falling:
                    return MinAnalogWalkSpeed;

                default:
                    return 0.0f;
            }
        }

        public virtual float GetMaxAcceleration()
        {
            if (IsFalling())
                return MaxAcceleration * AirControl;

            return MaxAcceleration;
        }

        protected virtual float GetMaxBrakingDeceleration()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:
                    return BrakingDecelerationWalking;

                case EMovementMode.Falling:
                    return CharacterMovement.IsOnGround ? 0.0f : BrakingDecelerationFalling;

                case EMovementMode.Swimming:
                    return BrakingDecelerationSwimming;

                case EMovementMode.Flying:
                    return BrakingDecelerationFlying;

                default:
                    return 0.0f;
            }
        }

        protected virtual float ComputeAnalogInputModifier(Vector3 desiredVelocity)
        {
            float maxSpeed = GetMaxSpeed();

            if (desiredVelocity.sqrMagnitude > 0.0f && maxSpeed > 0.00000001f)
            {
                return Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);
            }

            return 0.0f;
        }

        protected virtual Vector3 ApplyVelocityBraking(Vector3 velocity, float friction, float maxBrakingDeceleration, float deltaTime)
        {
            const float minTickTime = 0.000001f;
            if (velocity.IsZero() || deltaTime < minTickTime)
                return velocity;

            bool isZeroFriction = friction == 0.0f;
            bool isZeroBraking = maxBrakingDeceleration == 0.0f;
            if (isZeroFriction && isZeroBraking)
            {
                return velocity;
            }

            Vector3 oldVel = velocity;
            Vector3 revAccel = isZeroBraking ? Vector3.zero : -maxBrakingDeceleration * velocity.normalized;

            const float maxTimeStep = 1.0f / 33.0f;

            float remainingTime = deltaTime;
            while (remainingTime >= minTickTime)
            {
                float dt = remainingTime > maxTimeStep && !isZeroFriction
                    ? Mathf.Min(maxTimeStep, remainingTime * 0.5f)
                    : remainingTime;

                remainingTime -= dt;

                velocity += (-friction * velocity + revAccel) * dt;

                if (Vector3.Dot(velocity, oldVel) <= 0.0f)
                    return Vector3.zero;
            }

            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed <= 0.00001f || (!isZeroBraking && sqrSpeed <= 0.1f))
                return Vector3.zero;

            return velocity;
        }

        protected virtual Vector3 CalcVelocity(Vector3 velocity, Vector3 desiredVelocity, float friction, bool isFluid, float deltaTime)
        {
            const float minTickTime = 0.000001f;
            if (deltaTime < minTickTime)
                return velocity;

            float desiredSpeed = desiredVelocity.magnitude;
            Vector3 desiredMoveDirection = desiredSpeed > 0.0f ? desiredVelocity / desiredSpeed : Vector3.zero;

            float analogInputModifier = ComputeAnalogInputModifier(desiredVelocity);
            Vector3 inputAcceleration = GetMaxAcceleration() * analogInputModifier * desiredMoveDirection;

            float actualMaxSpeed = Mathf.Max(GetMinAnalogSpeed(), GetMaxSpeed() * analogInputModifier);

            bool isZeroAcceleration = inputAcceleration.IsZero();
            bool isVelocityOverMax = velocity.IsExceeding(actualMaxSpeed);

            if (isZeroAcceleration || isVelocityOverMax)
            {
                Vector3 oldVelocity = velocity;

                float actualBrakingFriction = UseSeparateBrakingFriction ? BrakingFriction : friction;
                float actualBrakingAcceleration =
                    UseSeparateBrakingDeceleration ? BrakingDeceleration : GetMaxBrakingDeceleration();

                velocity = ApplyVelocityBraking(velocity, actualBrakingFriction, actualBrakingAcceleration, deltaTime);

                if (isVelocityOverMax && velocity.sqrMagnitude < actualMaxSpeed.Square() && Vector3.Dot(inputAcceleration, oldVelocity) > 0.0f)
                    velocity = oldVelocity.normalized * actualMaxSpeed;
            }
            else
            {
                Vector3 accelDir = inputAcceleration.normalized;
                float velMag = velocity.magnitude;

                velocity -= (velocity - accelDir * velMag) * Mathf.Min(friction * deltaTime, 1.0f);
            }

            if (isFluid)
                velocity *= 1.0f - Mathf.Min(friction * deltaTime, 1.0f);

            if (!isZeroAcceleration)
            {
                float newMaxSpeed = velocity.IsExceeding(actualMaxSpeed) ? velocity.magnitude : actualMaxSpeed;

                velocity += inputAcceleration * deltaTime;
                velocity = velocity.ClampedTo(newMaxSpeed);
            }

            return velocity;
        }

        protected virtual Vector3 ConstrainInputVector(Vector3 inputVector)
        {
            Vector3 worldUp = -GetGravityDirection();

            float inputVectorDotWorldUp = Vector3.Dot(inputVector, worldUp);
            if (!Mathf.Approximately(inputVectorDotWorldUp, 0.0f) && (IsWalking() || IsFalling()))
                inputVector = Vector3.ProjectOnPlane(inputVector, worldUp);

            return CharacterMovement.ConstrainVectorToPlane(inputVector);
        }

        protected virtual void CalcDesiredVelocity(float deltaTime)
        {
            Vector3 movementDirection = Vector3.ClampMagnitude(GetMovementDirection(), 1.0f);

            _desiredVelocity = ConstrainInputVector(movementDirection * GetMaxSpeed());
        }

        protected virtual Vector3 GetDesiredVelocity()
        {
            return _desiredVelocity;
        }

        protected float GetSignedSlopeAngle()
        {
            Vector3 movementDirection = GetMovementDirection();
            if (movementDirection.IsZero() || !IsOnGround())
                return 0.0f;

            Vector3 projMovementDirection =
                Vector3.ProjectOnPlane(movementDirection, CharacterMovement.GroundNormal).normalized;

            return Mathf.Asin(Vector3.Dot(projMovementDirection, -GetGravityDirection())) * Mathf.Rad2Deg;
        }

        protected virtual void ApplyDownwardsForce()
        {
            Rigidbody groundRigidbody = CharacterMovement.GroundRigidbody;
            if (!groundRigidbody || groundRigidbody.isKinematic)
                return;

            Vector3 downwardForce = Mass * GetGravityVector();
            groundRigidbody.AddForceAtPosition(downwardForce * StandingDownwardForceScale, GetPosition());
        }

        protected virtual void WalkingMovementMode(float deltaTime)
        {
            CharacterMovement.Velocity = CalcVelocity(CharacterMovement.Velocity, GetDesiredVelocity(), GroundFriction, false, deltaTime);

            if (ApplyStandingDownwardForce)
                ApplyDownwardsForce();
        }

        protected virtual void FallingMovementMode(float deltaTime)
        {
            Vector3 desiredVelocity = GetDesiredVelocity();

            Vector3 worldUp = -GetGravityDirection();

            if (IsOnGround() && !IsOnWalkableGround())
            {
                Vector3 groundNormal = CharacterMovement.GroundNormal;

                if (Vector3.Dot(desiredVelocity, groundNormal) < 0.0f)
                {
                    Vector3 groundNormal2D = Vector3.ProjectOnPlane(groundNormal, worldUp).normalized;
                    desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, groundNormal2D);
                }

                worldUp = Vector3.ProjectOnPlane(worldUp, groundNormal).normalized;
            }

            Vector3 verticalVelocity = Vector3.Project(CharacterMovement.Velocity, worldUp);
            Vector3 lateralVelocity = CharacterMovement.Velocity - verticalVelocity;

            lateralVelocity = CalcVelocity(lateralVelocity, desiredVelocity, FallingLateralFriction, false, deltaTime);

            verticalVelocity += Gravity * deltaTime;

            float actualFallSpeed = MaxFallSpeed;
            if (_physicsVolume)
                actualFallSpeed = _physicsVolume.MaxFallSpeed;

            if (Vector3.Dot(verticalVelocity, worldUp) < -actualFallSpeed)
                verticalVelocity = Vector3.ClampMagnitude(verticalVelocity, actualFallSpeed);

            CharacterMovement.Velocity = lateralVelocity + verticalVelocity;

            _fallingTime += deltaTime;
        }

        protected virtual void FlyingMovementMode(float deltaTime)
        {
            float actualFriction = IsInWaterPhysicsVolume() ? _physicsVolume.Friction : FlyingFriction;

            CharacterMovement.Velocity
                = CalcVelocity(CharacterMovement.Velocity, GetDesiredVelocity(), actualFriction, true, deltaTime);
        }

        public virtual float CalcImmersionDepth()
        {
            float depth = 0.0f;

            if (IsInWaterPhysicsVolume())
            {
                float height = CharacterMovement.Height;
                if (height == 0.0f || Buoyancy == 0.0f)
                    depth = 1.0f;
                else
                {
                    Vector3 worldUp = -GetGravityDirection();

                    Vector3 rayOrigin = GetPosition() + worldUp * height;
                    Vector3 rayDirection = -worldUp;

                    BoxCollider waterVolumeCollider = _physicsVolume.BoxCollider;
                    depth = !waterVolumeCollider.Raycast(new Ray(rayOrigin, rayDirection), out RaycastHit hitInfo, height)
                        ? 1.0f
                        : 1.0f - Mathf.InverseLerp(0.0f, height, hitInfo.distance);
                }
            }

            return depth;
        }

        protected virtual void SwimmingMovementMode(float deltaTime)
        {
            float depth = CalcImmersionDepth();
            float actualBuoyancy = Buoyancy * depth;

            Vector3 desiredVelocity = GetDesiredVelocity();
            Vector3 newVelocity = CharacterMovement.Velocity;

            Vector3 worldUp = -GetGravityDirection();
            float verticalSpeed = Vector3.Dot(newVelocity, worldUp);

            if (verticalSpeed > MaxSwimSpeed * 0.33f && actualBuoyancy > 0.0f)
            {
                verticalSpeed = Mathf.Max(MaxSwimSpeed * 0.33f, verticalSpeed * depth * depth);
                newVelocity = Vector3.ProjectOnPlane(newVelocity, worldUp) + worldUp * verticalSpeed;
            }
            else if (depth < 0.65f)
            {
                float verticalDesiredSpeed = Vector3.Dot(desiredVelocity, worldUp);

                desiredVelocity =
                    Vector3.ProjectOnPlane(desiredVelocity, worldUp) + worldUp * Mathf.Min(0.1f, verticalDesiredSpeed);
            }

            float actualFriction = IsInWaterPhysicsVolume()
                ? _physicsVolume.Friction * depth
                : SwimmingFriction * depth;

            newVelocity = CalcVelocity(newVelocity, desiredVelocity, actualFriction, true, deltaTime);

            newVelocity += Gravity * ((1.0f - actualBuoyancy) * deltaTime);

            CharacterMovement.Velocity = newVelocity;
        }

        protected virtual void CustomMovementMode(float deltaTime)
        {
            OnCustomMovementMode(deltaTime);
        }

    }
}
