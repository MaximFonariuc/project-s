using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatActionOutcome
    {
        private CombatActionId _combatActionId;

        public AttackExecutionId ExecutionId { get; private set; }
        public PlayerCombatActionId ActionId { get; private set; }
        public int ConfirmedHitCount { get; private set; }
        public GameObject LastConfirmedTarget { get; private set; }
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
            LastConfirmedTarget = null;
        }

        public bool TryRecord(in CombatHitResult hit)
        {
            if (!IsTracking ||
                hit.ExecutionId != ExecutionId ||
                hit.ActionId != _combatActionId ||
                hit.Outcome != CombatHitOutcome.Damaged)
            {
                return false;
            }

            ConfirmedHitCount++;
            LastConfirmedTarget = hit.Target;
            return true;
        }

        public void End()
        {
            ExecutionId = default;
            ActionId = default;
            _combatActionId = default;
            ConfirmedHitCount = 0;
            LastConfirmedTarget = null;
        }
    }
}
