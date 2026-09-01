namespace _App
{
    public readonly struct GameplayTimeScaleRequest
    {
        public float Duration { get; }
        public float Scale { get; }

        public GameplayTimeScaleRequest(float duration, float scale)
        {
            if (!IsFinite(duration) || duration <= 0.0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(duration));
            }

            if (!IsFinite(scale) || scale < 0.0f || scale > 1.0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(scale));
            }

            Duration = duration;
            Scale = scale;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IGameplayTimeScaleRequests
    {
        void Request(in GameplayTimeScaleRequest request);
    }

    public interface IGameplayTimeScaleState
    {
        bool IsActive { get; }
        float CurrentScale { get; }
        float RemainingUnscaledTime { get; }
    }

    public interface ITimeScaleBackend
    {
        float TimeScale { get; set; }
    }
}
