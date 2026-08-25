using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatHitResolver : ICombatHitResolver, ICombatHitEvents, IDisposable
    {
        public event Action<CombatHitResult> HitResolved;

        public bool TryResolve(
            CombatAttackExecution execution,
            in CombatHitCandidate candidate,
            out CombatHitResult result)
        {
            result = default;
            ICombatHitTarget target = candidate.Target;
            if (execution == null || !execution.IsActive || execution.Source == null ||
                target == null || !target.IsAlive ||
                target.Owner == null || target.DamageMultiplier <= 0.0f)
            {
                return false;
            }

            CombatHitDefinition definition = execution.HitDefinition;
            int damage = definition.Damage > 0
                ? Mathf.Max(1, Mathf.RoundToInt(definition.Damage * target.DamageMultiplier))
                : 0;
            if (damage <= 0 || !execution.CanHitTarget(target.TargetId))
            {
                return false;
            }

            Vector3 direction = candidate.Direction.sqrMagnitude > 0.0001f
                ? candidate.Direction.normalized
                : execution.Source.transform.forward;
            result = new CombatHitResult(
                execution.Id,
                execution.ActionId,
                damage,
                definition.StaggerDamage,
                definition.Knockback,
                definition.VerticalKnockback,
                definition.ReactionId,
                definition.HitStop,
                candidate.Point,
                direction,
                execution.Source,
                target.Owner,
                target.HitZoneId);

            if (!target.TryApplyHit(in result))
            {
                result = default;
                return false;
            }

            execution.RegisterConfirmedTarget(target.TargetId);
            HitResolved?.Invoke(result);
            return true;
        }

        public void Dispose()
        {
            HitResolved = null;
        }
    }
}
