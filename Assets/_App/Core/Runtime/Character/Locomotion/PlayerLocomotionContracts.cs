using System;
using UnityEngine;

namespace _App
{
    public enum PlayerLocomotionStateId
    {
        Idle,
        Run,
        Jump,
        Fall,
        Dodge
    }

    public enum PlayerSpeedMode
    {
        Walk,
        Run,
        Sprint
    }

    public enum PlayerLocomotionMode
    {
        Grounded,
        Airborne,
        Swimming,
        Climbing,
        Flying
    }

    public readonly struct PlayerLocomotionSnapshot
    {
        public static PlayerLocomotionSnapshot Invalid => default;

        public bool IsValid { get; }
        public PlayerLocomotionStateId State { get; }
        public PlayerSpeedMode SpeedMode { get; }
        public PlayerLocomotionMode Mode { get; }
        public Vector3 Velocity { get; }
        public Vector3 MoveDirection { get; }
        public float ForwardSpeed { get; }
        public float LateralSpeed { get; }
        public float VerticalSpeed { get; }
        public float NormalizedSpeed { get; }
        public bool IsGrounded { get; }
        public bool IsJumpStarting { get; }
        public bool IsCrouched { get; }
        public bool DodgeBackwards { get; }

        public PlayerLocomotionSnapshot(
            PlayerLocomotionStateId state,
            PlayerSpeedMode speedMode,
            PlayerLocomotionMode mode,
            Vector3 velocity,
            Vector3 moveDirection,
            float forwardSpeed,
            float lateralSpeed,
            float verticalSpeed,
            float normalizedSpeed,
            bool isGrounded,
            bool isJumpStarting,
            bool isCrouched,
            bool dodgeBackwards)
        {
            IsValid = true;
            State = state;
            SpeedMode = speedMode;
            Mode = mode;
            Velocity = velocity;
            MoveDirection = moveDirection;
            ForwardSpeed = forwardSpeed;
            LateralSpeed = lateralSpeed;
            VerticalSpeed = verticalSpeed;
            NormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            IsGrounded = isGrounded;
            IsJumpStarting = isJumpStarting;
            IsCrouched = isCrouched;
            DodgeBackwards = dodgeBackwards;
        }
    }

    public readonly struct PlayerMovementIntent
    {
        public static PlayerMovementIntent Unrestricted => new PlayerMovementIntent(1.0f, true, true, false);
        public static PlayerMovementIntent Locked => new PlayerMovementIntent(0.0f, false, false, false);

        public float TranslationScale { get; }
        public bool AllowJump { get; }
        public bool AllowDodge { get; }
        public bool FaceMoveDirection { get; }

        public PlayerMovementIntent(
            float translationScale,
            bool allowJump,
            bool allowDodge,
            bool faceMoveDirection)
        {
            TranslationScale = Mathf.Clamp01(translationScale);
            AllowJump = allowJump;
            AllowDodge = allowDodge;
            FaceMoveDirection = faceMoveDirection;
        }
    }

    public interface ICharacterMotor
    {
        bool IsGrounded { get; }
        bool IsFalling { get; }
        bool IsJumping { get; }
        bool IsDodging { get; }
        bool IsSwimming { get; }
        bool IsClimbing { get; }
        bool IsFlying { get; }
        bool IsCrouched { get; }
        ERotationMode RotationMode { get; }
        float MaxSpeed { get; }
        Vector3 Up { get; }
        Vector3 Forward { get; }
        Vector3 Velocity { get; }

        void SetMovementDirection(Vector3 worldDirection);
        void FaceDirection(Vector3 worldDirection, float deltaTime);
        void SnapFacingDirection(Vector3 worldDirection);
        void PressJump(bool releaseAfterSimulation);
        void ReleaseJump();
        void SetSpeedMode(PlayerSpeedMode speedMode);
        bool TryInvertGravity();
    }

    public interface ICharacterImpactMotor
    {
        bool IsGrounded { get; }
        Vector3 Up { get; }

        void ApplyKnockback(Vector3 worldVelocity);
    }

    public interface IPlayerMovementReference
    {
        Vector3 ToWorldDirection(Vector2 input, Vector3 up, bool cameraRelative);
    }

    public interface IPlayerLocomotionPresentation
    {
        void Present(in PlayerLocomotionSnapshot snapshot, float deltaTime);
        void Reset();
    }

    public interface IPlayerLocomotionEvents
    {
        event Action DodgeAccepted;
    }

    public interface IPlayerLocomotionState
    {
        PlayerLocomotionStateId? CurrentState { get; }
        PlayerSpeedMode SpeedMode { get; }
        PlayerLocomotionSnapshot Snapshot { get; }
    }

    public interface IPlayerLocomotionCommands
    {
        bool TryInvertGravity();
    }
}
