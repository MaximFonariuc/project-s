using UnityEngine;

namespace _App
{
    internal sealed class UnityCombatClock : ICombatClock
    {
        public float ScaledTime => Time.time;
        public float UnscaledTime => Time.unscaledTime;
    }
}
