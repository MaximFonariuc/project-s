using System;
using UnityEngine;

namespace _App
{
    public sealed class CharacterMotor :
        ICharacterMotor,
        ICharacterImpactMotor,
        ICharacterActionMotionMotor,
        IDisposable
    {
        private readonly Character _character;
        private readonly float _walkSpeed;
        private readonly float _runSpeed;
        private readonly float _sprintSpeed;
        private PlayerSpeedMode _speedMode = PlayerSpeedMode.Run;
        private Vector3 _actionPlanarVelocity;
        private Vector3 _actionFacingDirection;
        private bool _hasActionMotion;

        public bool IsGrounded => _character.IsGrounded();
        public bool IsFalling => _character.IsFalling();
        public bool IsJumping => _character.IsJumping();
        public bool IsDodging => _character.IsDodging();
        public bool IsSwimming => _character.IsSwimming();
        public bool IsClimbing => _character.IsClimbing();
        public bool IsFlying => _character.IsFlying();
        public bool IsCrouched => _character.IsCrouched();
        public ERotationMode RotationMode => _character.RotationMode;
        public float MaxSpeed => _character.GetMaxSpeed();
        public Vector3 Up => _character.GetUpVector();
        public Vector3 Forward => _character.GetForwardVector();
        public Vector3 Position => _character.GetPosition();
        public Vector3 Velocity => _character.GetVelocity();

        public CharacterMotor(Character character, PlayerLocomotionSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _walkSpeed = settings.WalkSpeed;
            _runSpeed = settings.RunSpeed;
            _sprintSpeed = settings.SprintSpeed;
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void SetMovementDirection(Vector3 worldDirection)
        {
            _character.SetMovementDirection(Vector3.ClampMagnitude(worldDirection, 1.0f));
        }

        public void FaceDirection(Vector3 worldDirection, float deltaTime)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Up);
            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Up);
            Quaternion rotation = Quaternion.RotateTowards(
                _character.GetRotation(),
                targetRotation,
                _character.RotationRate * deltaTime);

            _character.SetRotation(rotation);
        }

        public void SnapFacingDirection(Vector3 worldDirection)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Up);
            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _character.SetRotation(Quaternion.LookRotation(planarDirection.normalized, Up));
        }

        public void PressJump()
        {
            _character.Jump();
        }

        public void ReleaseJump()
        {
            _character.StopJumping();
        }

        public void SetSpeedMode(PlayerSpeedMode speedMode)
        {
            if (_speedMode == speedMode)
            {
                return;
            }

            _speedMode = speedMode;
            _character.MaxWalkSpeed = speedMode switch
            {
                PlayerSpeedMode.Walk => _walkSpeed,
                PlayerSpeedMode.Sprint => _sprintSpeed,
                _ => _runSpeed
            };
        }

        public bool TryInvertGravity()
        {
            if (!IsFalling)
            {
                return false;
            }

            _character.GravityScale *= -1.0f;
            return true;
        }

        public void ApplyKnockback(Vector3 worldVelocity)
        {
            _character.LaunchCharacter(worldVelocity, false, true);
        }

        public void SetActionMotion(Vector3 planarVelocity, Vector3 facingDirection)
        {
            _actionPlanarVelocity = Vector3.ProjectOnPlane(planarVelocity, Up);
            _actionFacingDirection = Vector3.ProjectOnPlane(facingDirection, Up);
            _hasActionMotion = true;
        }

        public void ClearActionMotion()
        {
            _actionPlanarVelocity = Vector3.zero;
            _actionFacingDirection = Vector3.zero;
            _hasActionMotion = false;
        }

        public void Dispose()
        {
            ClearActionMotion();
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
        }

        private void OnAfterSimulationUpdated(float deltaTime)
        {
            if (!_hasActionMotion)
            {
                return;
            }

            Vector3 up = Up;
            Vector3 verticalVelocity = Vector3.Project(_character.GetVelocity(), up);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(_actionPlanarVelocity, up);
            _character.SetVelocity(verticalVelocity + planarVelocity);

            Vector3 facingDirection = Vector3.ProjectOnPlane(_actionFacingDirection, up);
            if (facingDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, up);
            Quaternion rotation = Quaternion.RotateTowards(
                _character.GetRotation(),
                targetRotation,
                _character.RotationRate * deltaTime);
            _character.SetRotation(rotation);
        }
    }
}
