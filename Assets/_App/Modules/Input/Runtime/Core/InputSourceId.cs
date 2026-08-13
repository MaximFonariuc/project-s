using System;

namespace ModularInput
{
    public readonly struct InputSourceId : IEquatable<InputSourceId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public InputSourceId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(InputSourceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InputSourceId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(InputSourceId left, InputSourceId right) => left.Equals(right);
        public static bool operator !=(InputSourceId left, InputSourceId right) => !left.Equals(right);
    }
}
