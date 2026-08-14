using System;
using UnityEngine;

namespace _App
{
    public readonly struct AttackExecutionId : IEquatable<AttackExecutionId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public AttackExecutionId(ulong value)
        {
            Value = value != 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public bool Equals(AttackExecutionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AttackExecutionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(AttackExecutionId left, AttackExecutionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttackExecutionId left, AttackExecutionId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct CombatReactionId : IEquatable<CombatReactionId>
    {
        public static CombatReactionId None => default;

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public CombatReactionId(string value)
        {
            Value = !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : throw new ArgumentException("Combat reaction id cannot be empty.", nameof(value));
        }

        public bool Equals(CombatReactionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatReactionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(CombatReactionId left, CombatReactionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CombatReactionId left, CombatReactionId right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct CombatHitDefinition
    {
        public int Damage { get; }
        public float StaggerDamage { get; }
        public float Knockback { get; }
        public CombatReactionId ReactionId { get; }

        public CombatHitDefinition(
            int damage,
            float staggerDamage,
            float knockback,
            CombatReactionId reactionId)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (staggerDamage < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(staggerDamage));
            }

            if (knockback < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(knockback));
            }

            Damage = damage;
            StaggerDamage = staggerDamage;
            Knockback = knockback;
            ReactionId = reactionId;
        }
    }

    public readonly struct CombatHitResult
    {
        public AttackExecutionId ExecutionId { get; }
        public PlayerCombatActionId ActionId { get; }
        public int Damage { get; }
        public float StaggerDamage { get; }
        public float Knockback { get; }
        public CombatReactionId ReactionId { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public string HitZoneId { get; }

        internal CombatHitResult(
            AttackExecutionId executionId,
            PlayerCombatActionId actionId,
            int damage,
            float staggerDamage,
            float knockback,
            CombatReactionId reactionId,
            Vector3 point,
            Vector3 direction,
            GameObject source,
            GameObject target,
            string hitZoneId)
        {
            ExecutionId = executionId;
            ActionId = actionId;
            Damage = damage;
            StaggerDamage = staggerDamage;
            Knockback = knockback;
            ReactionId = reactionId;
            Point = point;
            Direction = direction;
            Source = source;
            Target = target;
            HitZoneId = hitZoneId;
        }
    }

    public interface ICombatHitEvents
    {
        event Action<CombatHitResult> HitResolved;
    }

    internal readonly struct CombatHitCandidate
    {
        public ICombatHitTarget Target { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }

        public CombatHitCandidate(
            ICombatHitTarget target,
            Vector3 point,
            Vector3 direction)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Point = point;
            Direction = direction;
        }
    }

    internal interface ICombatHitTarget
    {
        int TargetId { get; }
        bool IsAlive { get; }
        GameObject Owner { get; }
        string HitZoneId { get; }
        float DamageMultiplier { get; }

        bool TryApplyHit(in CombatHitResult hit);
    }

    internal interface ICombatHitResolver
    {
        bool TryResolve(
            CombatAttackExecution execution,
            in CombatHitCandidate candidate,
            out CombatHitResult result);
    }
}
