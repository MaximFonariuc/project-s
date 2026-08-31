using System;

namespace _App
{
    public sealed class PlayerLocomotionSettings
    {
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float SprintSpeed { get; }
        public bool CanJump { get; }
        public float DodgeDuration { get; }
        public float JumpInputBufferDuration { get; }
        public float DodgeInputBufferDuration { get; }

        public PlayerLocomotionSettings(
            float walkSpeed,
            float runSpeed,
            float sprintSpeed,
            bool canJump,
            float dodgeDuration,
            float jumpInputBufferDuration,
            float dodgeInputBufferDuration)
        {
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            SprintSpeed = sprintSpeed;
            CanJump = canJump;
            DodgeDuration = Math.Max(0.0f, dodgeDuration);
            JumpInputBufferDuration = Math.Max(0.0f, jumpInputBufferDuration);
            DodgeInputBufferDuration = Math.Max(0.0f, dodgeInputBufferDuration);
        }

        internal static PlayerLocomotionSettings From(
            Character character,
            CharacterFeatureSet features)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            float runSpeed = character.MaxWalkSpeed;
            WalkSpeedSettings walk = features.GetEnabled<WalkSpeedSettings>();
            SprintSpeedSettings sprint = features.GetEnabled<SprintSpeedSettings>();
            DodgeSettings dodge = features.GetEnabled<DodgeSettings>();

            return new PlayerLocomotionSettings(
                walk?.MaxSpeed ?? runSpeed,
                runSpeed,
                sprint?.MaxSpeed ?? runSpeed,
                character.CanEverJump,
                dodge?.Duration ?? 0.0f,
                character.JumpMaxPreGroundedTime,
                dodge?.InputBufferDuration ?? 0.0f);
        }
    }
}
