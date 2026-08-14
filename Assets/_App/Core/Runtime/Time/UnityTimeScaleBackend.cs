using UnityEngine;

namespace _App
{
    internal sealed class UnityTimeScaleBackend : ITimeScaleBackend
    {
        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = Mathf.Max(0.0f, value);
        }
    }
}
