using System;

namespace ModularInput
{
    public readonly struct InputMapId : IEquatable<InputMapId>
    {
        public Guid Value { get; }
        public bool IsValid => Value != Guid.Empty;

        public InputMapId(Guid value)
        {
            Value = value;
        }

        public static InputMapId Parse(string value)
        {
            return new InputMapId(Guid.Parse(value));
        }

        public bool Equals(InputMapId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is InputMapId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("D");

        public static bool operator ==(InputMapId left, InputMapId right) => left.Equals(right);
        public static bool operator !=(InputMapId left, InputMapId right) => !left.Equals(right);
    }
}
