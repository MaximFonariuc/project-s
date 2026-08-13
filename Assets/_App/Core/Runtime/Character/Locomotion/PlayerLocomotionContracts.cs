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

    public readonly struct PlayerMovementIntent
    {
        public static PlayerMovementIntent Unrestricted => new PlayerMovementIntent(1.0f, true, true, false);

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
        bool IsFalling { get; }
        bool IsJumping { get; }
        bool IsDodging { get; }
        bool IsSwimming { get; }
        bool IsClimbing { get; }
        bool IsCrouched { get; }
        ERotationMode RotationMode { get; }
        Vector3 Up { get; }
        Vector3 Forward { get; }
        Vector3 Velocity { get; }

        void SetMovementDirection(Vector3 worldDirection);
        void FaceDirection(Vector3 worldDirection, float deltaTime);
        void SnapFacingDirection(Vector3 worldDirection);
        void PressJump();
        void ReleaseJump();
        void SetSpeedMode(PlayerSpeedMode speedMode);
        bool TryInvertGravity();
    }

    public interface IPlayerMovementReference
    {
        Vector3 ToWorldDirection(Vector2 input, Vector3 up, bool cameraRelative);
    }

    public interface IPlayerLocomotionPresentation
    {
        void EnterState(PlayerLocomotionStateId stateId, bool dodgeBackwards);
        void SetSpeedMode(PlayerSpeedMode speedMode);
        void Tick(float deltaTime);
    }

    public interface IPlayerLocomotionEvents
    {
        event Action DodgeAccepted;
    }

    public interface IPlayerLocomotionState
    {
        PlayerLocomotionStateId? CurrentState { get; }
        PlayerSpeedMode SpeedMode { get; }
    }

    public interface IPlayerLocomotionCommands
    {
        bool TryInvertGravity();
    }
}
