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
        public float MediumLandingImpactSpeed { get; }
        public float HardLandingImpactSpeed { get; }
        public float MediumLandingRecoveryDuration { get; }
        public float HardLandingRecoveryDuration { get; }
        public float AirDashDuration { get; }

        public PlayerLocomotionSettings(
            float walkSpeed,
            float runSpeed,
            float sprintSpeed,
            bool canJump,
            float dodgeDuration,
            float jumpInputBufferDuration,
            float dodgeInputBufferDuration,
            float airDashDuration = 0.0f,
            float mediumLandingImpactSpeed = 7.0f,
            float hardLandingImpactSpeed = 10.0f,
            float mediumLandingRecoveryDuration = 0.64f,
            float hardLandingRecoveryDuration = 0.95f)
        {
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            SprintSpeed = sprintSpeed;
            CanJump = canJump;
            DodgeDuration = Math.Max(0.0f, dodgeDuration);
            JumpInputBufferDuration = Math.Max(0.0f, jumpInputBufferDuration);
            DodgeInputBufferDuration = Math.Max(0.0f, dodgeInputBufferDuration);
            AirDashDuration = Math.Max(0.0f, airDashDuration);
            MediumLandingImpactSpeed = Math.Max(0.0f, mediumLandingImpactSpeed);
            HardLandingImpactSpeed = Math.Max(
                MediumLandingImpactSpeed,
                hardLandingImpactSpeed);
            MediumLandingRecoveryDuration = Math.Max(
                0.0f,
                mediumLandingRecoveryDuration);
            HardLandingRecoveryDuration = Math.Max(
                0.0f,
                hardLandingRecoveryDuration);
        }

        public PlayerLanding ResolveLanding(float impactSpeed)
        {
            float safeImpactSpeed = Math.Max(0.0f, impactSpeed);
            if (safeImpactSpeed > HardLandingImpactSpeed)
            {
                return new PlayerLanding(
                    PlayerLandingType.Hard,
                    safeImpactSpeed,
                    HardLandingRecoveryDuration);
            }

            if (safeImpactSpeed > MediumLandingImpactSpeed)
            {
                return new PlayerLanding(
                    PlayerLandingType.Medium,
                    safeImpactSpeed,
                    MediumLandingRecoveryDuration);
            }

            return new PlayerLanding(PlayerLandingType.Soft, safeImpactSpeed, 0.0f);
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
            AirDashSettings airDash = features.GetEnabled<AirDashSettings>();
            LandingRecoverySettings landing =
                features.GetEnabled<LandingRecoverySettings>();

            return new PlayerLocomotionSettings(
                walk?.MaxSpeed ?? runSpeed,
                runSpeed,
                sprint?.MaxSpeed ?? runSpeed,
                character.CanEverJump,
                dodge?.Duration ?? 0.0f,
                character.JumpMaxPreGroundedTime,
                dodge?.InputBufferDuration ?? 0.0f,
                airDash?.Duration ?? 0.0f,
                landing?.MediumImpactSpeed ?? 7.0f,
                landing?.HardImpactSpeed ?? 10.0f,
                landing?.MediumRecoveryDuration ?? 0.64f,
                landing?.HardRecoveryDuration ?? 0.95f);
        }
    }
}
