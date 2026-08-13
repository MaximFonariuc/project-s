using System;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public readonly struct MecanimParameterBinding
    {
        public string Name { get; }
        public int Hash { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Name);

        public MecanimParameterBinding(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Animator parameter name is required.", nameof(name));
            }

            Name = name;
            Hash = Animator.StringToHash(name);
        }
    }

    public readonly struct MecanimCharacterAnimationBindings
    {
        public MecanimParameterBinding MovementType { get; }
        public MecanimParameterBinding MovementDirection { get; }
        public MecanimParameterBinding OnGround { get; }
        public MecanimParameterBinding FallingDuration { get; }
        public MecanimParameterBinding SpeedMultiplier { get; }
        public bool IsValid =>
            MovementType.IsValid &&
            MovementDirection.IsValid &&
            OnGround.IsValid &&
            FallingDuration.IsValid &&
            SpeedMultiplier.IsValid;

        public MecanimCharacterAnimationBindings(
            string movementType,
            string movementDirection,
            string onGround,
            string fallingDuration,
            string speedMultiplier)
        {
            MovementType = new MecanimParameterBinding(movementType);
            MovementDirection = new MecanimParameterBinding(movementDirection);
            OnGround = new MecanimParameterBinding(onGround);
            FallingDuration = new MecanimParameterBinding(fallingDuration);
            SpeedMultiplier = new MecanimParameterBinding(speedMultiplier);
        }
    }
}
