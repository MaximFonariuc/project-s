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
        private CharacterActionMotionIntent _actionMotion;
        private float _pendingActionVerticalImpulse;
        private bool _overrideVerticalVelocityOnImpulse;
        private bool _hasActionMotion;
        private bool _releaseJumpAfterSimulation;

        public event Action Jumped;
        public event Action<float> Landed;

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
            _character.Jumped += OnJumped;
            _character.Landed += OnLanded;
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void SetMovementDirection(Vector3 worldDirection)
        {
            _character.SetMovementDirection(Vector3.ClampMagnitude(worldDirection, 1.0f));
        }

        public void StopPlanarMovement()
        {
            Vector3 up = Up.normalized;
            Vector3 velocity = _character.GetVelocity();
            ClearActionMotion();
            _character.SetMovementDirection(Vector3.zero);
            _character.SetVelocity(up * Vector3.Dot(velocity, up));
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

        public void PressJump(bool releaseAfterSimulation)
        {
            _releaseJumpAfterSimulation = releaseAfterSimulation;
            _character.Jump();
        }

        public void ReleaseJump()
        {
            _releaseJumpAfterSimulation = false;
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

        public void SetActionMotion(in CharacterActionMotionIntent intent)
        {
            _actionMotion = intent;
            _hasActionMotion = true;
        }

        public void QueueActionVerticalImpulse(float verticalVelocity, bool overrideVerticalVelocity)
        {
            if (Mathf.Approximately(verticalVelocity, 0.0f))
            {
                return;
            }

            _pendingActionVerticalImpulse = verticalVelocity;
            _overrideVerticalVelocityOnImpulse = overrideVerticalVelocity;
        }

        public void ClearActionMotion()
        {
            _actionMotion = default;
            _pendingActionVerticalImpulse = 0.0f;
            _overrideVerticalVelocityOnImpulse = false;
            _hasActionMotion = false;
        }

        public void Dispose()
        {
            ClearActionMotion();
            _releaseJumpAfterSimulation = false;
            _character.Jumped -= OnJumped;
            _character.Landed -= OnLanded;
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
            Jumped = null;
            Landed = null;
        }

        private void OnJumped()
        {
            Jumped?.Invoke();
        }

        private void OnLanded(Vector3 landingVelocity)
        {
            Vector3 up = Up.normalized;
            float impactSpeed = Mathf.Max(0.0f, -Vector3.Dot(landingVelocity, up));
            Landed?.Invoke(impactSpeed);
        }

        private void OnAfterSimulationUpdated(float deltaTime)
        {
            if (_releaseJumpAfterSimulation)
            {
                _releaseJumpAfterSimulation = false;
                _character.StopJumping();
            }

            bool hasPendingVerticalImpulse =
                !Mathf.Approximately(_pendingActionVerticalImpulse, 0.0f);
            if (!_hasActionMotion &&
                !hasPendingVerticalImpulse)
            {
                return;
            }

            Vector3 up = Up;
            Vector3 currentVelocity = _character.GetVelocity();
            Vector3 planarVelocity =
                _actionMotion.PlanarMode == CharacterActionPlanarMode.OverrideVelocity
                    ? Vector3.ProjectOnPlane(_actionMotion.PlanarVelocity, up)
                    : Vector3.ProjectOnPlane(currentVelocity, up);
            float verticalVelocity = Vector3.Dot(currentVelocity, up);
            verticalVelocity = _actionMotion.VerticalMode switch
            {
                CharacterActionVerticalMode.OverrideVelocity => _actionMotion.VerticalVelocity,
                CharacterActionVerticalMode.MinimumVelocity =>
                    Mathf.Max(verticalVelocity, _actionMotion.VerticalVelocity),
                _ => verticalVelocity
            };

            if (hasPendingVerticalImpulse)
            {
                verticalVelocity = _overrideVerticalVelocityOnImpulse
                    ? _pendingActionVerticalImpulse
                    : verticalVelocity + _pendingActionVerticalImpulse;
                _pendingActionVerticalImpulse = 0.0f;
                _overrideVerticalVelocityOnImpulse = false;
            }

            if (verticalVelocity > 0.0001f && _character.IsGrounded())
            {
                _character.PauseGroundConstraint();
                _character.SetMovementMode(EMovementMode.Falling);
            }

            _character.SetVelocity(planarVelocity + up * verticalVelocity);

            Vector3 facingDirection = Vector3.ProjectOnPlane(_actionMotion.FacingDirection, up);
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
