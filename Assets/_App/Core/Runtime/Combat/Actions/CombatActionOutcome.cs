using System;

namespace _App
{
    internal sealed class CombatActionOutcome
    {
        public AttackExecutionId ExecutionId { get; private set; }
        public PlayerCombatActionId ActionId { get; private set; }
        public int ConfirmedHitCount { get; private set; }
        public bool IsTracking => ExecutionId.IsValid;
        public bool HasConfirmedHit => ConfirmedHitCount > 0;

        public void Begin(
            AttackExecutionId executionId,
            PlayerCombatActionId actionId)
        {
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
            ConfirmedHitCount = 0;
        }

        public bool TryRecord(in CombatHitResult hit)
        {
            if (!IsTracking ||
                hit.ExecutionId != ExecutionId ||
                hit.ActionId != ActionId)
            {
                return false;
            }

            ConfirmedHitCount++;
            return true;
        }

        public void End()
        {
            ExecutionId = default;
            ActionId = default;
            ConfirmedHitCount = 0;
        }
    }
}
