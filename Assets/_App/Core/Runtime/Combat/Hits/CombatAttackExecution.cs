using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    internal sealed class CombatAttackExecution
    {
        private const float HitIntervalTolerance = 0.0001f;

        private readonly Dictionary<int, TargetHitRecord> _targetHits = new();

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
            _targetHits.Clear();
        }

        public bool CanHitTarget(int targetId, float scaledTime)
        {
            if (!IsActive || targetId == SourceRootId)
            {
                return false;
            }

            if (!_targetHits.TryGetValue(targetId, out TargetHitRecord record))
            {
                return true;
            }

            CombatHitRepeatPolicy repeat = HitDefinition.RepeatPolicy;
            return repeat.AllowsRepeatedHits &&
                   record.Count < repeat.MaxHitsPerTarget &&
                   scaledTime - record.LastHitTime + HitIntervalTolerance >=
                   repeat.IntervalSeconds;
        }

        public void RegisterConfirmedTarget(int targetId, float scaledTime)
        {
            if (!CanHitTarget(targetId, scaledTime))
            {
                return;
            }

            int count = _targetHits.TryGetValue(targetId, out TargetHitRecord record)
                ? record.Count + 1
                : 1;
            _targetHits[targetId] = new TargetHitRecord(count, scaledTime);
        }

        public void End()
        {
            IsActive = false;
            Source = null;
            SourceRootId = 0;
            _targetHits.Clear();
        }

        private readonly struct TargetHitRecord
        {
            public int Count { get; }
            public float LastHitTime { get; }

            public TargetHitRecord(int count, float lastHitTime)
            {
                Count = count;
                LastHitTime = lastHitTime;
            }
        }
    }
}
