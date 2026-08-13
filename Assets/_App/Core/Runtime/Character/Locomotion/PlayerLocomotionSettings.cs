using System;

namespace _App
{
    public sealed class PlayerLocomotionSettings
    {
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float SprintSpeed { get; }

        public PlayerLocomotionSettings(
            float walkSpeed,
            float runSpeed,
            float sprintSpeed)
        {
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            SprintSpeed = sprintSpeed;
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

            return new PlayerLocomotionSettings(
                walk?.MaxSpeed ?? runSpeed,
                runSpeed,
                sprint?.MaxSpeed ?? runSpeed);
        }
    }
}
