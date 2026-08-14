using System;

namespace _App
{
    internal readonly struct CombatMotionDefinition
    {
        public static CombatMotionDefinition Disabled => default;

        public NormalizedActionWindow Window { get; }
        public float Distance { get; }
        public bool FaceTarget { get; }
        public float TargetStoppingDistance { get; }
        public bool IsEnabled => Window.IsEnabled && (Distance > 0.0f || FaceTarget);
        public bool HasTranslation => Window.IsEnabled && Distance > 0.0f;

        public CombatMotionDefinition(
            NormalizedActionWindow window,
            float distance,
            bool faceTarget,
            float targetStoppingDistance)
        {
            if (!window.IsEnabled)
            {
                throw new ArgumentException("Combat motion requires an enabled action window.", nameof(window));
            }

            if (distance < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            if (targetStoppingDistance < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetStoppingDistance));
            }

            Window = window;
            Distance = distance;
            FaceTarget = faceTarget;
            TargetStoppingDistance = targetStoppingDistance;
        }
    }
}
