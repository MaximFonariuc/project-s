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
        public bool IsJumpStarting { get; }
        public CharacterGait Gait { get; }
        public CharacterLocomotionMode Mode { get; }

        public CharacterLocomotionFrame(
            float forwardSpeed,
            float lateralSpeed,
            float verticalSpeed,
            float normalizedSpeed,
            bool isGrounded,
            bool isJumpStarting,
            CharacterGait gait,
            CharacterLocomotionMode mode)
        {
            ForwardSpeed = forwardSpeed;
            LateralSpeed = lateralSpeed;
            VerticalSpeed = verticalSpeed;
            NormalizedSpeed = normalizedSpeed;
            IsGrounded = isGrounded;
            IsJumpStarting = isJumpStarting;
            Gait = gait;
            Mode = mode;
        }
    }

    public readonly struct CharacterLookFrame
    {
        public float YawDegrees { get; }
        public float PitchDegrees { get; }

        public CharacterLookFrame(float yawDegrees, float pitchDegrees)
        {
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
        }
    }

    public readonly struct AnimationSlotId : IEquatable<AnimationSlotId>
    {
        public static AnimationSlotId None => default;

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public AnimationSlotId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public bool Equals(AnimationSlotId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is AnimationSlotId other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(AnimationSlotId left, AnimationSlotId right) => left.Equals(right);
        public static bool operator !=(AnimationSlotId left, AnimationSlotId right) => !left.Equals(right);
    }

    public readonly struct AnimationActionId : IEquatable<AnimationActionId>
    {
        public static AnimationActionId None => default;

        public int Value { get; }
        public bool IsValid => Value > 0;

        public AnimationActionId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public bool Equals(AnimationActionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AnimationActionId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(AnimationActionId left, AnimationActionId right) => left.Equals(right);
        public static bool operator !=(AnimationActionId left, AnimationActionId right) => !left.Equals(right);
    }

    public readonly struct AnimationActionRequest
    {
        public AnimationActionId Action { get; }
        public int Variant { get; }
        public float ExpectedDuration { get; }

        public bool IsValid => Action.IsValid && ExpectedDuration > 0.0f;

        public AnimationActionRequest(
            AnimationActionId action,
            int variant,
            float expectedDuration)
        {
            Action = action;
            Variant = variant;
            ExpectedDuration = Math.Max(0.0f, expectedDuration);
        }
    }

    public readonly struct AnimationPlayback
    {
        public static AnimationPlayback Invalid => default;

        public int Sequence { get; }
        public float Duration { get; }
        public bool IsValid => Sequence != 0;

        public AnimationPlayback(int sequence, float duration)
        {
            Sequence = sequence;
            Duration = Math.Max(0.0f, duration);
        }
    }

    public readonly struct AnimationActionPlayback
    {
        public static AnimationActionPlayback Invalid => default;

        public int Sequence { get; }
        public float Duration { get; }
        public bool IsValid => Sequence != 0;

        public AnimationActionPlayback(int sequence, float duration)
        {
            Sequence = sequence;
            Duration = Math.Max(0.0f, duration);
        }
    }

    public interface ICharacterAnimationDriver
    {
        void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime);
        AnimationActionPlayback PlayAction(in AnimationActionRequest request);
        void CompleteAction();
        void CancelAction();
        void SetPlaybackSpeed(float speed);
        void Reset();
    }

    public interface ICharacterLookAnimationDriver
    {
        void ApplyLook(in CharacterLookFrame frame, float deltaTime);
    }
}
