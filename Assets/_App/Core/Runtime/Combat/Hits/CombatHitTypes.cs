using System;
using UnityEngine;

namespace _App
{
    public readonly struct CombatActionId : IEquatable<CombatActionId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public CombatActionId(string value)
        {
            Value = CombatIdentifier.RequireCanonical(
                value,
                "Combat action id",
                nameof(value));
        }

        public bool Equals(CombatActionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatActionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(CombatActionId left, CombatActionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CombatActionId left, CombatActionId right)
        {
            return !left.Equals(right);
        }
    }

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
            Value = CombatIdentifier.RequireCanonical(
                value,
                "Combat reaction id",
                nameof(value));
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

    public static class CombatReactionIds
    {
        private const float VerticalImpactThreshold = 0.0001f;

        public static readonly CombatReactionId Light = new CombatReactionId("hit.light");
        public static readonly CombatReactionId Heavy = new CombatReactionId("hit.heavy");
        public static readonly CombatReactionId Launcher = new CombatReactionId("hit.launcher");
        public static readonly CombatReactionId Slam = new CombatReactionId("hit.slam");

        internal static void ValidateVerticalImpact(
            CombatReactionId reactionId,
            float verticalKnockback)
        {
            if (reactionId == Launcher && verticalKnockback <= VerticalImpactThreshold)
            {
                throw new ArgumentException(
                    $"Reaction '{Launcher}' requires positive vertical knockback.",
                    nameof(verticalKnockback));
            }

            if (reactionId == Slam && verticalKnockback >= -VerticalImpactThreshold)
            {
                throw new ArgumentException(
                    $"Reaction '{Slam}' requires negative vertical knockback.",
                    nameof(verticalKnockback));
            }
        }
    }

    public readonly struct CombatHitStop
    {
        public static CombatHitStop Disabled => default;

        public bool IsEnabled => Duration > 0.0f;
        public float Duration { get; }
        public float TimeScale { get; }

        public CombatHitStop(float duration, float timeScale)
        {
            CombatActionNumeric.RequireFinite(duration, nameof(duration));
            CombatActionNumeric.RequireFinite(timeScale, nameof(timeScale));
            if (duration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (timeScale < 0.0f || timeScale > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale));
            }

            Duration = duration;
            TimeScale = timeScale;
        }
    }

    internal readonly struct CombatHitDefinition
    {
        private const float ImpactTolerance = 0.0001f;

        public int Damage { get; }
        public float StaggerDamage { get; }
        public float Knockback { get; }
        public float VerticalKnockback { get; }
        public CombatReactionId ReactionId { get; }
        public CombatHitStop HitStop { get; }
        public CombatHitRepeatPolicy RepeatPolicy { get; }
        public bool HasGameplayImpact =>
            Damage > 0 ||
            StaggerDamage > ImpactTolerance ||
            Knockback > ImpactTolerance ||
            Mathf.Abs(VerticalKnockback) > ImpactTolerance ||
            ReactionId.IsValid ||
            HitStop.IsEnabled ||
            RepeatPolicy.AllowsRepeatedHits;

        public CombatHitDefinition(
            int damage,
            float staggerDamage,
            float knockback,
            float verticalKnockback,
            CombatReactionId reactionId,
            CombatHitStop hitStop,
            CombatHitRepeatPolicy repeatPolicy)
        {
            CombatActionNumeric.RequireFinite(staggerDamage, nameof(staggerDamage));
            CombatActionNumeric.RequireFinite(knockback, nameof(knockback));
            CombatActionNumeric.RequireFinite(verticalKnockback, nameof(verticalKnockback));
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

            CombatReactionIds.ValidateVerticalImpact(reactionId, verticalKnockback);

            Damage = damage;
            StaggerDamage = staggerDamage;
            Knockback = knockback;
            VerticalKnockback = verticalKnockback;
            ReactionId = reactionId;
            HitStop = hitStop;
            RepeatPolicy = repeatPolicy;
        }
    }

    internal readonly struct CombatHitRepeatPolicy
    {
        public static CombatHitRepeatPolicy OncePerAction => default;

        public bool AllowsRepeatedHits => MaxHitsPerTarget > 1;
        public float IntervalSeconds { get; }
        public int MaxHitsPerTarget { get; }

        public CombatHitRepeatPolicy(float intervalSeconds, int maxHitsPerTarget)
        {
            CombatActionNumeric.RequireFinite(intervalSeconds, nameof(intervalSeconds));
            if (intervalSeconds <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            }

            if (maxHitsPerTarget <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitsPerTarget));
            }

            IntervalSeconds = intervalSeconds;
            MaxHitsPerTarget = maxHitsPerTarget;
        }
    }

    public readonly struct CombatHitResult
    {
        public AttackExecutionId ExecutionId { get; }
        public CombatActionId ActionId { get; }
        public int Damage { get; }
        public float StaggerDamage { get; }
        public float Knockback { get; }
        public float VerticalKnockback { get; }
        public CombatReactionId ReactionId { get; }
        public CombatHitStop HitStop { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
        public string HitZoneId { get; }

        internal CombatHitResult(
            AttackExecutionId executionId,
            CombatActionId actionId,
            int damage,
            float staggerDamage,
            float knockback,
            float verticalKnockback,
            CombatReactionId reactionId,
            CombatHitStop hitStop,
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
            VerticalKnockback = verticalKnockback;
            ReactionId = reactionId;
            HitStop = hitStop;
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
