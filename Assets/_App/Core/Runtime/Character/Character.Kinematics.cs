using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public partial class Character
    {
        public Vector3 GetGravityVector()
        {
            return Gravity;
        }

        public Vector3 GetGravityDirection()
        {
            return Gravity.normalized;
        }

        public float GetGravityMagnitude()
        {
            return Gravity.magnitude;
        }

        protected void SetGravityVector(Vector3 newGravityVector)
        {
            _gravity = newGravityVector;
        }

        public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
        {
            CharacterMovement.AddForce(force, forceMode);
        }

        public void LaunchCharacter(Vector3 launchVelocity, bool overrideVerticalVelocity = false,
            bool overrideLateralVelocity = false)
        {
            CharacterMovement.LaunchCharacter(launchVelocity, overrideVerticalVelocity, overrideLateralVelocity);
        }

        public void IgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            CharacterMovement.IgnoreCollision(otherCollider, ignore);
        }

        public void PauseGroundConstraint(float seconds = 0.1f)
        {
            CharacterMovement.PauseGroundConstraint(seconds);
        }

        public void EnableGroundConstraint(bool enable)
        {
            CharacterMovement.ConstrainToGround = enable;
        }

        public bool WasOnGround()
        {
            return CharacterMovement.WasOnGround;
        }

        private bool IsOnGround()
        {
            return CharacterMovement.IsOnGround;
        }

        private bool IsOnWalkableGround()
        {
            return CharacterMovement.IsOnWalkableGround;
        }

        public bool IsGrounded()
        {
            return CharacterMovement.IsGrounded;
        }

        public CharacterMovement GetCharacterMovement()
        {
            return CharacterMovement;
        }

        public Vector3 GetPosition()
        {
            return CharacterMovement.Position;
        }

        public void TeleportPosition(Vector3 newPosition, bool interpolating = true, bool updateGround = false)
        {
            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.None;
            }

            CharacterMovement.SetPosition(newPosition, updateGround);

            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public Quaternion GetRotation()
        {
            return CharacterMovement.Rotation;
        }

        public void SetRotation(Quaternion newRotation)
        {
            CharacterMovement.Rotation = newRotation;
        }

        public void TeleportRotation(Quaternion newRotation, bool interpolating = true)
        {
            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.None;
            }

            CharacterMovement.SetRotation(newRotation);

            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public Vector3 GetVelocity()
        {
            return CharacterMovement.Velocity;
        }

        public void SetVelocity(Vector3 newVelocity)
        {
            CharacterMovement.Velocity = newVelocity;
        }

        public float GetSpeed()
        {
            return CharacterMovement.Velocity.magnitude;
        }

        public Vector3 GetMovementDirection()
        {
            return _movementDirection;
        }

        public void SetMovementDirection(Vector3 movementDirection)
        {
            _movementDirection = movementDirection;
        }
    }
}
