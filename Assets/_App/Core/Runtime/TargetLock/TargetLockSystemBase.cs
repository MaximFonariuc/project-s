using System;
using UnityEngine;

namespace _App
{
    public abstract class TargetLockSystemBase
    {
        protected readonly Character Character;
        protected readonly LayerMask TargetMask;

        protected Transform CurrentTargetInternal;
        protected Collider[] OverlapBuffer;

        public const float MAX_LOCK_DISTANCE = 25f;
        private const int INITIAL_BUFFER_SIZE = 5;

        public Transform CurrentTarget => CurrentTargetInternal;
        public bool IsTargetLocked => CurrentTargetInternal != null;

        protected TargetLockSystemBase(Character character, string targetLayerName)
        {
            Character = character;
            TargetMask = LayerMask.GetMask(targetLayerName);
            OverlapBuffer = new Collider[INITIAL_BUFFER_SIZE];
        }

        public virtual void ClearTarget()
        {
            CurrentTargetInternal = null;
        }

        protected int TryOverlapWithAutoResize()
        {
            int hits;
            while (true)
            {
                hits = Physics.OverlapSphereNonAlloc(
                    Character.transform.position,
                    MAX_LOCK_DISTANCE,
                    OverlapBuffer,
                    TargetMask,
                    QueryTriggerInteraction.Ignore
                );

                if (hits < OverlapBuffer.Length)
                    break;

                int newSize = OverlapBuffer.Length * 2;
                OverlapBuffer = new Collider[newSize];
            }
            return hits;
        }

        public abstract void AcquireTarget(Action<ERotationMode> onModeChanged = null);
    }
}