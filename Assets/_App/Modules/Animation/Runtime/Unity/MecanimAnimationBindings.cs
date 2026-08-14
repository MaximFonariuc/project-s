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
        public MecanimParameterBinding VerticalSpeed { get; }
        public MecanimParameterBinding SpeedMultiplier { get; }
        public bool IsValid =>
            MovementType.IsValid &&
            MovementDirection.IsValid &&
            VerticalSpeed.IsValid &&
            SpeedMultiplier.IsValid;

        public MecanimCharacterAnimationBindings(
            string movementType,
            string movementDirection,
            string verticalSpeed,
            string speedMultiplier)
        {
            MovementType = new MecanimParameterBinding(movementType);
            MovementDirection = new MecanimParameterBinding(movementDirection);
            VerticalSpeed = new MecanimParameterBinding(verticalSpeed);
            SpeedMultiplier = new MecanimParameterBinding(speedMultiplier);
        }
    }
}
