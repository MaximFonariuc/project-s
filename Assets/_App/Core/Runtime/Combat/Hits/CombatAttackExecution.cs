using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    internal sealed class CombatAttackExecution
    {
        private readonly HashSet<int> _hitTargets = new HashSet<int>();

        public AttackExecutionId Id { get; private set; }
        public PlayerCombatActionId ActionId { get; private set; }
        public CombatHitDefinition HitDefinition { get; private set; }
        public GameObject Source { get; private set; }
        public int SourceRootId { get; private set; }
        public bool IsActive { get; private set; }

        public void Begin(
            AttackExecutionId id,
            PlayerCombatActionId actionId,
            CombatHitDefinition hitDefinition,
            GameObject source)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("Attack execution id must be valid.", nameof(id));
            }

            Source = source != null
                ? source
                : throw new ArgumentNullException(nameof(source));
            Id = id;
            ActionId = actionId;
            HitDefinition = hitDefinition;
            SourceRootId = source.transform.root.gameObject.GetInstanceID();
            IsActive = true;
            _hitTargets.Clear();
        }

        public bool CanHitTarget(int targetId)
        {
            return IsActive &&
                   targetId != SourceRootId &&
                   !_hitTargets.Contains(targetId);
        }

        public void RegisterConfirmedTarget(int targetId)
        {
            if (CanHitTarget(targetId))
            {
                _hitTargets.Add(targetId);
            }
        }

        public void End()
        {
            IsActive = false;
            Source = null;
            SourceRootId = 0;
            _hitTargets.Clear();
        }
    }
}
