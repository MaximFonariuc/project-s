using System;

namespace _App
{
    public sealed class PlayerLocomotionSettings
    {
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float SprintSpeed { get; }
        public float DodgeDuration { get; }

        public PlayerLocomotionSettings(
            float walkSpeed,
            float runSpeed,
            float sprintSpeed,
            float dodgeDuration)
        {
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            SprintSpeed = sprintSpeed;
            DodgeDuration = Math.Max(0.0f, dodgeDuration);
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
                dodge?.Duration ?? 0.0f);
        }
    }
}
