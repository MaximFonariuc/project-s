using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public class SlidingCharacter : Character
    {
        [Space(15.0f)]
        [SerializeField] private float _slideImpulse = 20.0f;
        [SerializeField] private float _slideDownAcceleration = 20.0f;

        public override float GetMaxSpeed()
        {
            return IsSliding() ? MaxWalkSpeed : base.GetMaxSpeed();
        }

        public override float GetMaxAcceleration()
        {
            return IsSliding() ? MaxAcceleration * 0.1f : base.GetMaxAcceleration();
        }
        
        public override bool IsWalking()
        {
            return IsSliding() || base.IsWalking();
        }
        
        public bool IsSliding()
        {
            return MovementMode == EMovementMode.Custom && CustomMovement == (int)ECustomMovementMode.Sliding;
        }

        protected virtual bool CanSlide()
        {
            if (!IsGrounded())
                return false;
            
            float sqrSpeed = Velocity.sqrMagnitude;
            float slideSpeedThreshold = MaxWalkSpeedCrouched * MaxWalkSpeedCrouched;

            return sqrSpeed >= slideSpeedThreshold * 1.02f;
        }
        
        protected virtual Vector3 CalcSlideDirection()
        {
            Vector3 slideDirection = GetMovementDirection();
            if (slideDirection.IsZero())
                slideDirection = GetVelocity();
            else if (slideDirection.IsZero())
                slideDirection = GetForwardVector();
            
            slideDirection = ConstrainInputVector(slideDirection);
            
            return slideDirection.normalized;
        }

        protected virtual void CheckSlideInput()
        {
            bool isSliding = IsSliding();
            bool wantsToSlide = CrouchInputPressed;

            if (!isSliding && wantsToSlide && CanSlide())
            {
                SetMovementMode(EMovementMode.Custom, (int)ECustomMovementMode.Sliding);
            }
            else if (isSliding && (!wantsToSlide || !CanSlide()))
            {
                SetMovementMode(EMovementMode.Walking);
            }
        }

        protected override void OnMovementModeChanged(EMovementMode prevMovementMode, int prevCustomMode)
        {
            base.OnMovementModeChanged(prevMovementMode, prevCustomMode);
            
            if (IsSliding())
            {
                Vector3 slideDirection = CalcSlideDirection();
                CharacterMovement.Velocity += slideDirection * _slideImpulse;
                
                SetRotationMode(ERotationMode.None);
            }
            
            bool wasSliding = prevMovementMode == EMovementMode.Custom &&
                              prevCustomMode == (int)ECustomMovementMode.Sliding;
            
            if (wasSliding)
            {
                SetRotationMode(ERotationMode.OrientRotationToMovement);
                
                if (IsFalling())
                {
                    Vector3 worldUp = -GetGravityDirection();
                    Vector3 verticalVelocity = Vector3.Project(Velocity, worldUp);
                    Vector3 lateralVelocity = Vector3.ClampMagnitude(Velocity - verticalVelocity, MaxWalkSpeed);

                    CharacterMovement.Velocity = lateralVelocity + verticalVelocity;
                }
            }
        }

        protected override void OnBeforeSimulationUpdate(float deltaTime)
        {
            base.OnBeforeSimulationUpdate(deltaTime);
            
            CheckSlideInput();
        }

        protected virtual void SlidingMovementMode(float deltaTime)
        {
            Vector3 desiredVelocity = Vector3.Project(GetDesiredVelocity(), GetRightVector());
            
            CharacterMovement.Velocity =
                CalcVelocity(CharacterMovement.Velocity, desiredVelocity, GroundFriction * 0.2f, false, deltaTime);
            
            Vector3 slideDownDirection =
                Vector3.ProjectOnPlane(GetGravityDirection(), CharacterMovement.GroundNormal).normalized;

            CharacterMovement.Velocity += _slideDownAcceleration * deltaTime * slideDownDirection;
            
            if (ApplyStandingDownwardForce)
                ApplyDownwardsForce();
        }

        protected override void CustomMovementMode(float deltaTime)
        {
            base.CustomMovementMode(deltaTime);
            
            if (CustomMovement == (int)ECustomMovementMode.Sliding)
                SlidingMovementMode(deltaTime);
        }
    }
}
