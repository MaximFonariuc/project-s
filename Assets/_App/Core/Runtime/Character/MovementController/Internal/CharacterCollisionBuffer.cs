using System;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterCollisionBuffer
    {
        private CollisionResult[] _results;

        public CharacterCollisionBuffer(int capacity)
        {
            _results = new CollisionResult[capacity];
        }

        public int Count { get; private set; }

        public CollisionFlags Flags { get; private set; }

        public ref CollisionResult this[int index] => ref _results[index];

        public void Clear()
        {
            Count = 0;
            Flags = CollisionFlags.None;
        }

        public void Add(ref CollisionResult collisionResult, Rigidbody currentPlatform)
        {
            Flags |= (CollisionFlags)collisionResult.HitLocation;

            Rigidbody otherRigidbody = collisionResult.Rigidbody;
            Collider otherCollider = collisionResult.Collider;
            if (otherRigidbody == currentPlatform)
                return;

            for (int i = 0; i < Count; i++)
            {
                ref CollisionResult existing = ref _results[i];
                if (otherRigidbody != null && otherRigidbody == existing.Rigidbody)
                    return;

                if (otherRigidbody == null && otherCollider != null && otherCollider == existing.Collider)
                    return;
            }

            if (Count == _results.Length)
                Array.Resize(ref _results, Mathf.Max(4, _results.Length * 2));

            _results[Count++] = collisionResult;
        }

        public CollisionResult Get(int index)
        {
            return _results[index];
        }
    }
}
