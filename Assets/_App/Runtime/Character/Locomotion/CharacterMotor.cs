using System;
using UnityEngine;

namespace _App
{
    public sealed class CharacterMotor : ICharacterMotor
    {
        private readonly Character _character;
        private readonly AbilityController _abilities;
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

        public CharacterMotor(Character character, AbilityController abilities)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));

            _runSpeed = _character.MaxWalkSpeed;
            _walkSpeed = _abilities.GetAbility<WalkAbility>()?.MaxSpeed ?? _runSpeed;
            _sprintSpeed = _abilities.GetAbility<SprintAbility>()?.MaxSpeed ?? _runSpeed;
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

        public void PressJump()
        {
            _character.Jump();
        }

        public void ReleaseJump()
        {
            _character.StopJumping();
        }

        public void StartGliding()
        {
            _abilities.GetAbility<GlideAbility>()?.Glide();
        }

        public void StopGliding()
        {
            _abilities.GetAbility<GlideAbility>()?.StopGliding();
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

        public bool TryDodge(Vector3 worldDirection, bool backwards)
        {
            DodgeAbility dodge = _abilities.GetAbility<DodgeAbility>();
            if (dodge == null || !dodge.IsDodgeAllowed())
            {
                return false;
            }

            SetMovementDirection(worldDirection);
            if (!backwards && worldDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Up);
                if (planarDirection.sqrMagnitude > 0.0001f)
                {
                    _character.SetRotation(Quaternion.LookRotation(planarDirection.normalized, Up));
                }
            }

            dodge.Dodge();
            return true;
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
