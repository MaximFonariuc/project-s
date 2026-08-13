using System;

namespace ModularInput
{
    public readonly struct InputActionId : IEquatable<InputActionId>
    {
        public Guid Value { get; }
        public bool IsValid => Value != Guid.Empty;

        public InputActionId(Guid value)
        {
            Value = value;
        }

        public static InputActionId Parse(string value)
        {
            return new InputActionId(Guid.Parse(value));
        }

        public bool Equals(InputActionId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is InputActionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("D");

        public static bool operator ==(InputActionId left, InputActionId right) => left.Equals(right);
        public static bool operator !=(InputActionId left, InputActionId right) => !left.Equals(right);
    }
}
