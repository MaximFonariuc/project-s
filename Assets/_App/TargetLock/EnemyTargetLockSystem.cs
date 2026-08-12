using System;
using UnityEngine;

namespace _App
{
    public class EnemyTargetLockSystem : TargetLockSystemBase
    {
        public EnemyTargetLockSystem(Character character) : base(character, "Player")
        {
        }

        public override void AcquireTarget(Action<ERotationMode> onModeChanged = null)
        {
            if (IsTargetLocked)
                return;

            Transform bestTarget = FindClosestPlayer();
            if (bestTarget != null)
            {
                CurrentTargetInternal = bestTarget;
                onModeChanged?.Invoke(ERotationMode.OrientRotationToMovement);
                Debug.Log("Enemy targetLOck");
            }
        }

        private Transform FindClosestPlayer()
        {
            int hits = TryOverlapWithAutoResize();
            if (hits == 0) return null;

            Transform best = null;
            float bestDist = float.MaxValue;
            Vector3 origin = Character.transform.position;

            for (int i = 0; i < hits; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;
                OverlapBuffer[i] = null;

                float dist = Vector3.Distance(origin, col.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = col.transform;
                }
            }
            return best;
        }
    }
}