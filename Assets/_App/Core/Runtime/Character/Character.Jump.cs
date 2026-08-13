using UnityEngine;

namespace _App
{
    public partial class Character
    {
        public virtual bool IsJumping()
        {
            return _jumpState.IsJumping;
        }

        public virtual void Jump()
        {
            JumpInputPressed = true;
        }

        public virtual void StopJumping()
        {
            JumpInputPressed = false;
            JumpInputHoldTime = 0.0f;

            ResetJumpState();
        }

        protected virtual void ResetJumpState()
        {
            if (!IsFalling())
                JumpCurrentCount = 0;

            JumpForceTimeRemaining = 0.0f;

            _jumpState.IsJumping = false;
        }

        public virtual bool IsJumpProvidingForce()
        {
            return JumpForceTimeRemaining > 0.0f;
        }

        protected virtual float GetMaxJumpHeight()
        {
            float gravityMagnitude = GetGravityMagnitude();
            if (gravityMagnitude > 0.0001f)
            {
                return JumpImpulse * JumpImpulse / (2.0f * gravityMagnitude);
            }

            return 0.0f;
        }

        public virtual float GetMaxJumpHeightWithJumpTime()
        {
            float maxJumpHeight = GetMaxJumpHeight();
            return maxJumpHeight + JumpImpulse * JumpMaxHoldTime;
        }

        protected virtual bool IsJumpAllowed()
        {
            if (!CanJumpWhileCrouching && IsCrouched())
                return false;

            return CanEverJump && (IsWalking() || IsFalling());
        }

        protected virtual bool CanJump()
        {
            bool isJumpAllowed = IsJumpAllowed();
            if (isJumpAllowed)
            {
                if (!_jumpState.IsJumping || JumpMaxHoldTime <= 0.0f)
                {
                    if (JumpCurrentCount == 0)
                    {
                        isJumpAllowed = JumpInputHoldTime <= JumpMaxPreGroundedTime;

                        if (isJumpAllowed)
                            JumpInputHoldTime = 0.0f;
                    }
                    else
                    {
                        isJumpAllowed = JumpCurrentCount < JumpMaxCount && JumpInputHoldTime == 0.0f;
                    }
                }
                else
                {
                    bool jumpInputHeld = JumpInputPressed && JumpInputHoldTime < JumpMaxHoldTime;

                    isJumpAllowed = jumpInputHeld && (JumpCurrentCount < JumpMaxCount || (_jumpState.IsJumping && JumpCurrentCount == JumpMaxCount));
                }
            }

            return isJumpAllowed;
        }

        protected virtual bool DoJump()
        {
            Vector3 worldUp = -GetGravityDirection();

            if (CharacterMovement.IsConstrainedToPlane &&
                Mathf.Approximately(Vector3.Dot(CharacterMovement.GetPlaneConstraintNormal(), worldUp), 1.0f))
            {
                return false;
            }

            float verticalSpeed = Mathf.Max(Vector3.Dot(CharacterMovement.Velocity, worldUp), JumpImpulse);

            CharacterMovement.Velocity =
                Vector3.ProjectOnPlane(CharacterMovement.Velocity, worldUp) + worldUp * verticalSpeed;

            return true;
        }

        protected virtual void CheckJumpInput()
        {
            if (!JumpInputPressed)
                return;

            if (JumpCurrentCount == 0 && IsFalling() && FallingTime > JumpMaxPostGroundedTime)
                JumpCurrentCount++;

            bool didJump = CanJump() && DoJump();
            if (didJump)
            {
                if (!_jumpState.IsJumping)
                {
                    JumpCurrentCount++;
                    JumpForceTimeRemaining = JumpMaxHoldTime;

                    CharacterMovement.PauseGroundConstraint();
                    SetMovementMode(EMovementMode.Falling);

                    OnJumped();
                }
            }

            _jumpState.IsJumping = didJump;
        }

        protected virtual void UpdateJumpTimers(float deltaTime)
        {
            if (JumpInputPressed)
                JumpInputHoldTime += deltaTime;

            if (JumpForceTimeRemaining > 0.0f)
            {
                JumpForceTimeRemaining -= deltaTime;
                if (JumpForceTimeRemaining <= 0.0f)
                    ResetJumpState();
            }
        }

        protected virtual void NotifyJumpApex()
        {
            if (!JumpApexNotify)
                return;

            float verticalSpeed = Vector3.Dot(GetVelocity(), -GetGravityDirection());
            if (verticalSpeed >= 0.0f)
                return;

            JumpApexNotify = false;
            OnReachedJumpApex();
        }

    }
}
