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
            CharacterAbilityRuntime abilities)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            float runSpeed = character.MaxWalkSpeed;
            return new PlayerLocomotionSettings(
                abilities.Get<WalkAbility>()?.MaxSpeed ?? runSpeed,
                runSpeed,
                abilities.Get<SprintAbility>()?.MaxSpeed ?? runSpeed);
        }
    }
}
