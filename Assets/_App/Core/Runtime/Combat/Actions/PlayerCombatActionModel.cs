using System;

namespace _App
{
    public readonly struct PlayerCombatCommandId : IEquatable<PlayerCombatCommandId>
    {
        public string Value { get; }

        public PlayerCombatCommandId(string value)
        {
            Value = !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("Combat command id cannot be empty.", nameof(value));
        }

        public bool Equals(PlayerCombatCommandId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerCombatCommandId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(PlayerCombatCommandId left, PlayerCombatCommandId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerCombatCommandId left, PlayerCombatCommandId right)
        {
            return !left.Equals(right);
        }
    }

    public static class PlayerCombatCommandIds
    {
        public static readonly PlayerCombatCommandId LightAttack = new PlayerCombatCommandId("attack.light");
        public static readonly PlayerCombatCommandId HeavyAttack = new PlayerCombatCommandId("attack.heavy");
        public static readonly PlayerCombatCommandId BoneCrash = new PlayerCombatCommandId("skill.bone-crash");
        public static readonly PlayerCombatCommandId FlashStance = new PlayerCombatCommandId("skill.flash-stance");
        public static readonly PlayerCombatCommandId Special = new PlayerCombatCommandId("skill.special");

    }

    public readonly struct PlayerCombatActionId : IEquatable<PlayerCombatActionId>
    {
        public string Value { get; }

        public PlayerCombatActionId(string value)
        {
            Value = !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("Combat action id cannot be empty.", nameof(value));
        }

        public bool Equals(PlayerCombatActionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerCombatActionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(PlayerCombatActionId left, PlayerCombatActionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerCombatActionId left, PlayerCombatActionId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct PlayerCombatAnimationId : IEquatable<PlayerCombatAnimationId>
    {
        public string Value { get; }

        public PlayerCombatAnimationId(string value)
        {
            Value = !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("Combat animation id cannot be empty.", nameof(value));
        }

        public bool Equals(PlayerCombatAnimationId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerCombatAnimationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(PlayerCombatAnimationId left, PlayerCombatAnimationId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerCombatAnimationId left, PlayerCombatAnimationId right)
        {
            return !left.Equals(right);
        }
    }

    public static class PlayerCombatAnimationIds
    {
        public static readonly PlayerCombatAnimationId HeavyAttack =
            new PlayerCombatAnimationId("attack.heavy");
        public static readonly PlayerCombatAnimationId Special =
            new PlayerCombatAnimationId("skill.special");
    }

    [Flags]
    public enum PlayerCombatEnvironment
    {
        None = 0,
        Grounded = 1 << 0,
        Airborne = 1 << 1,
        Any = Grounded | Airborne
    }

    public enum PlayerCombatPresentationKind
    {
        ComboStep,
        NamedAnimation,
        AirComboStep
    }

    public enum PlayerCombatTransitionWindow
    {
        Chain,
        Cancel
    }

    internal readonly struct PlayerCombatPresentation
    {
        public PlayerCombatPresentationKind Kind { get; }
        public int ComboIndex { get; }
        public PlayerCombatAnimationId AnimationId { get; }

        private PlayerCombatPresentation(
            PlayerCombatPresentationKind kind,
            int comboIndex,
            PlayerCombatAnimationId animationId)
        {
            Kind = kind;
            ComboIndex = comboIndex;
            AnimationId = animationId;
        }

        public static PlayerCombatPresentation Combo(int comboIndex)
        {
            if (comboIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(comboIndex));
            }

            return new PlayerCombatPresentation(
                PlayerCombatPresentationKind.ComboStep,
                comboIndex,
                default);
        }

        public static PlayerCombatPresentation AirCombo(int comboIndex)
        {
            if (comboIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(comboIndex));
            }

            return new PlayerCombatPresentation(
                PlayerCombatPresentationKind.AirComboStep,
                comboIndex,
                default);
        }

        public static PlayerCombatPresentation Named(PlayerCombatAnimationId animationId)
        {
            if (string.IsNullOrEmpty(animationId.Value))
            {
                throw new ArgumentException("Named combat presentation requires an animation id.", nameof(animationId));
            }

            return new PlayerCombatPresentation(
                PlayerCombatPresentationKind.NamedAnimation,
                0,
                animationId);
        }
    }

    internal sealed class PlayerCombatAction
    {
        public PlayerCombatActionId Id { get; }
        public PlayerCombatEnvironment Environment { get; }
        public PlayerCombatPresentation Presentation { get; }
        public CombatActionDefinition Definition { get; }

        public PlayerCombatAction(
            PlayerCombatActionId id,
            PlayerCombatEnvironment environment,
            PlayerCombatPresentation presentation,
            CombatActionDefinition definition)
        {
            if (environment == PlayerCombatEnvironment.None)
            {
                throw new ArgumentOutOfRangeException(nameof(environment));
            }

            Id = id;
            Environment = environment;
            Presentation = presentation;
            Definition = definition;
        }

        public bool Supports(PlayerCombatEnvironment environment)
        {
            return (Environment & environment) != 0;
        }
    }

    internal sealed class PlayerWeaponTransitionAction
    {
        public CombatActionDefinition Definition { get; }

        public PlayerWeaponTransitionAction(CombatActionDefinition definition)
        {
            Definition = definition;
        }
    }
}
