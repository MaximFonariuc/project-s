using System;

namespace ModularAnimation
{
    public enum CharacterLocomotionMode
    {
        Grounded,
        Airborne,
        Swimming,
        Climbing,
        Flying
    }

    public enum CharacterGait
    {
        Walk = 1,
        Run = 2,
        Sprint = 3
    }

    public readonly struct CharacterLocomotionFrame
    {
        public float ForwardSpeed { get; }
        public float LateralSpeed { get; }
        public float VerticalSpeed { get; }
        public float NormalizedSpeed { get; }
        public bool IsGrounded { get; }
        public CharacterGait Gait { get; }
        public CharacterLocomotionMode Mode { get; }

        public CharacterLocomotionFrame(
            float forwardSpeed,
            float lateralSpeed,
            float verticalSpeed,
            float normalizedSpeed,
            bool isGrounded,
            CharacterGait gait,
            CharacterLocomotionMode mode)
        {
            ForwardSpeed = forwardSpeed;
            LateralSpeed = lateralSpeed;
            VerticalSpeed = verticalSpeed;
            NormalizedSpeed = normalizedSpeed;
            IsGrounded = isGrounded;
            Gait = gait;
            Mode = mode;
        }
    }

    public readonly struct AnimationStateId : IEquatable<AnimationStateId>
    {
        public static AnimationStateId None => default;

        public int Hash { get; }

        public bool IsValid => Hash != 0;

        public AnimationStateId(int hash)
        {
            Hash = hash;
        }

        public bool Equals(AnimationStateId other) => Hash == other.Hash;
        public override bool Equals(object obj) => obj is AnimationStateId other && Equals(other);
        public override int GetHashCode() => Hash;
        public static bool operator ==(AnimationStateId left, AnimationStateId right) => left.Equals(right);
        public static bool operator !=(AnimationStateId left, AnimationStateId right) => !left.Equals(right);
    }

    public readonly struct AnimationLayerId : IEquatable<AnimationLayerId>
    {
        public static AnimationLayerId Base => new AnimationLayerId(0);

        public int Index { get; }

        public AnimationLayerId(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Index = index;
        }

        public bool Equals(AnimationLayerId other) => Index == other.Index;
        public override bool Equals(object obj) => obj is AnimationLayerId other && Equals(other);
        public override int GetHashCode() => Index;
        public static bool operator ==(AnimationLayerId left, AnimationLayerId right) => left.Equals(right);
        public static bool operator !=(AnimationLayerId left, AnimationLayerId right) => !left.Equals(right);
    }

    public readonly struct AnimationPlayback
    {
        public static AnimationPlayback Invalid => default;

        public AnimationStateId State { get; }
        public AnimationLayerId Layer { get; }
        public float Duration { get; }

        public bool IsValid => State.IsValid;

        public AnimationPlayback(AnimationStateId state, AnimationLayerId layer, float duration)
        {
            State = state;
            Layer = layer;
            Duration = duration > 0.0f ? duration : 0.0f;
        }
    }

    public interface ICharacterAnimationDriver
    {
        void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime);
        AnimationPlayback CrossFade(
            AnimationStateId state,
            AnimationLayerId layer,
            float transitionDuration,
            float expectedDuration = 0.0f);
        void SetPlaybackSpeed(float speed);
        void RefreshBindings();
    }

    public interface ICharacterLookAnimationDriver
    {
        void SetLook(float horizontal, float vertical);
    }
}
