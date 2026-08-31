using System;

namespace _App
{
    internal static class CombatIdentifier
    {
        public static string RequireCanonical(
            string value,
            string identifierKind,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"{identifierKind} cannot be empty.",
                    parameterName);
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"{identifierKind} cannot contain leading or trailing whitespace.",
                    parameterName);
            }

            return value;
        }
    }

    public readonly struct PlayerCombatCommandId : IEquatable<PlayerCombatCommandId>
    {
        public string Value { get; }

        public PlayerCombatCommandId(string value)
        {
            Value = CombatIdentifier.RequireCanonical(
                value,
                "Combat command id",
                nameof(value));
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
        public static readonly PlayerCombatCommandId Launcher = new PlayerCombatCommandId("attack.launcher");
        public static readonly PlayerCombatCommandId FlashStance = new PlayerCombatCommandId("skill.flash-stance");
        public static readonly PlayerCombatCommandId Special = new PlayerCombatCommandId("skill.special");

    }

    internal readonly struct PlayerCombatCommandSample
    {
        public PlayerCombatCommandId CommandId { get; }
        public PlayerCombatActionId SourceActionId { get; }
        public float SourceNormalizedTime { get; }
        public bool HasSourceAction { get; }

        private PlayerCombatCommandSample(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId sourceActionId,
            float sourceNormalizedTime,
            bool hasSourceAction)
        {
            if (string.IsNullOrEmpty(commandId.Value))
            {
                throw new ArgumentException("Combat command sample requires a command id.", nameof(commandId));
            }

            if (hasSourceAction && string.IsNullOrEmpty(sourceActionId.Value))
            {
                throw new ArgumentException("Combat command sample requires a source action id.", nameof(sourceActionId));
            }

            if (sourceNormalizedTime < 0.0f || sourceNormalizedTime > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceNormalizedTime));
            }

            CommandId = commandId;
            SourceActionId = sourceActionId;
            SourceNormalizedTime = sourceNormalizedTime;
            HasSourceAction = hasSourceAction;
        }

        public static PlayerCombatCommandSample Entry(PlayerCombatCommandId commandId)
        {
            return new PlayerCombatCommandSample(
                commandId,
                default,
                0.0f,
                false);
        }

        public static PlayerCombatCommandSample Transition(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId sourceActionId,
            float sourceNormalizedTime)
        {
            return new PlayerCombatCommandSample(
                commandId,
                sourceActionId,
                sourceNormalizedTime,
                true);
        }
    }

    public readonly struct PlayerCombatActionId : IEquatable<PlayerCombatActionId>
    {
        public string Value { get; }

        public PlayerCombatActionId(string value)
        {
            Value = CombatIdentifier.RequireCanonical(
                value,
                "Combat action id",
                nameof(value));
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
            Value = CombatIdentifier.RequireCanonical(
                value,
                "Combat animation id",
                nameof(value));
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
        public static readonly PlayerCombatAnimationId Launcher =
            new PlayerCombatAnimationId("attack.launcher");
        public static readonly PlayerCombatAnimationId AirChase =
            new PlayerCombatAnimationId("movement.air-chase");
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

    public enum PlayerCombatTransitionCondition
    {
        Any,
        OnHit,
        OnWhiff
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
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException(
                    "Combat action requires a valid id.",
                    nameof(id));
            }

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
