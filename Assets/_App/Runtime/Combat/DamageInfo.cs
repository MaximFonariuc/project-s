using UnityEngine;

namespace _App
{
    public readonly struct DamageInfo
    {
        public int Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }

        public DamageInfo(int amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }
}
