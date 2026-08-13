using UnityEngine;

namespace _App
{
    internal sealed class CharacterCollisionBuffer
    {
        private readonly CollisionResult[] _results;

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
            if (otherRigidbody != null)
            {
                if (otherRigidbody == currentPlatform)
                    return;

                for (int i = 0; i < Count; i++)
                {
                    if (otherRigidbody == _results[i].Rigidbody)
                        return;
                }
            }

            if (Count < _results.Length)
                _results[Count++] = collisionResult;
        }

        public CollisionResult Get(int index)
        {
            return _results[index];
        }
    }
}
