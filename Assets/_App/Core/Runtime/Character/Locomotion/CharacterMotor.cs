using System;
using UnityEngine;

namespace _App
{
    public sealed class CharacterMotor : ICharacterMotor
    {
        private readonly Character _character;
        private readonly float _walkSpeed;
        private readonly float _runSpeed;
        private readonly float _sprintSpeed;
        private PlayerSpeedMode _speedMode = PlayerSpeedMode.Run;

        public bool IsFalling => _character.IsFalling();
        public bool IsJumping => _character.IsJumping();
        public bool IsDodging => _character.IsDodging();
        public bool IsSwimming => _character.IsSwimming();
        public bool IsClimbing => _character.IsClimbing();
        public bool IsCrouched => _character.IsCrouched();
        public ERotationMode RotationMode => _character.RotationMode;
        public Vector3 Up => _character.GetUpVector();
        public Vector3 Forward => _character.GetForwardVector();
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

    }
}
