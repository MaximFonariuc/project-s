using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatActionOutcome
    {
        private const float DistanceTieEpsilon = 0.0001f;

        private CombatActionId _combatActionId;
        private float _primaryTargetDistanceSqr;
        private int _primaryTargetInstanceId;
        private bool _primaryTargetMatchesPreferred;

        public AttackExecutionId ExecutionId { get; private set; }
        public PlayerCombatActionId ActionId { get; private set; }
        public int ConfirmedHitCount { get; private set; }
        public GameObject PrimaryConfirmedTarget { get; private set; }
        public bool IsTracking => ExecutionId.IsValid;
        public bool HasConfirmedHit => ConfirmedHitCount > 0;

        public void Begin(
            AttackExecutionId executionId,
            PlayerCombatActionId actionId)
        {
            if (IsTracking)
            {
                throw new InvalidOperationException(
                    "A combat action outcome is already tracking an execution.");
            }

            if (!executionId.IsValid)
            {
                throw new ArgumentException(
                    "Combat action outcome requires a valid execution id.",
                    nameof(executionId));
            }

            if (string.IsNullOrEmpty(actionId.Value))
            {
                throw new ArgumentException(
                    "Combat action outcome requires a valid action id.",
                    nameof(actionId));
            }

            ExecutionId = executionId;
            ActionId = actionId;
            _combatActionId = new CombatActionId(actionId.Value);
            ConfirmedHitCount = 0;
            ResetPrimaryTarget();
        }

        public bool TryRecord(
            in CombatHitResult hit,
            Transform preferredTarget)
        {
            if (!IsTracking ||
                hit.ExecutionId != ExecutionId ||
                hit.ActionId != _combatActionId ||
                hit.Outcome != CombatHitOutcome.Damaged)
            {
                return false;
            }

            ConfirmedHitCount++;
            TryPromotePrimaryTarget(in hit, preferredTarget);
            return true;
        }

        public void End()
        {
            ExecutionId = default;
            ActionId = default;
            _combatActionId = default;
            ConfirmedHitCount = 0;
            ResetPrimaryTarget();
        }

        private void TryPromotePrimaryTarget(
            in CombatHitResult hit,
            Transform preferredTarget)
        {
            if (hit.Target == null)
            {
                return;
            }

            Transform candidate = hit.Target.transform;
            bool matchesPreferred = IsSameTarget(candidate, preferredTarget);
            float distanceSqr = ResolveDistanceSqr(in hit);
            int instanceId = hit.Target.GetInstanceID();
            if (PrimaryConfirmedTarget != null &&
                !IsBetterCandidate(
                    matchesPreferred,
                    distanceSqr,
                    instanceId))
            {
                return;
            }

            PrimaryConfirmedTarget = hit.Target;
            _primaryTargetMatchesPreferred = matchesPreferred;
            _primaryTargetDistanceSqr = distanceSqr;
            _primaryTargetInstanceId = instanceId;
        }

        private bool IsBetterCandidate(
            bool matchesPreferred,
            float distanceSqr,
            int instanceId)
        {
            if (matchesPreferred != _primaryTargetMatchesPreferred)
            {
                return matchesPreferred;
            }

            if (distanceSqr < _primaryTargetDistanceSqr - DistanceTieEpsilon)
            {
                return true;
            }

            return Mathf.Abs(distanceSqr - _primaryTargetDistanceSqr) <=
                   DistanceTieEpsilon &&
                   instanceId < _primaryTargetInstanceId;
        }

        private static float ResolveDistanceSqr(in CombatHitResult hit)
        {
            if (hit.Source == null || hit.Target == null)
            {
                return float.PositiveInfinity;
            }

            return (hit.Target.transform.position -
                    hit.Source.transform.position).sqrMagnitude;
        }

        private static bool IsSameTarget(
            Transform candidate,
            Transform preferredTarget)
        {
            return candidate != null &&
                   preferredTarget != null &&
                   (candidate == preferredTarget ||
                    candidate.IsChildOf(preferredTarget) ||
                    preferredTarget.IsChildOf(candidate));
        }

        private void ResetPrimaryTarget()
        {
            PrimaryConfirmedTarget = null;
            _primaryTargetDistanceSqr = float.PositiveInfinity;
            _primaryTargetInstanceId = int.MaxValue;
            _primaryTargetMatchesPreferred = false;
        }
    }
}
