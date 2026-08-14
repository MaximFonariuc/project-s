using System;
using UnityEngine;

namespace _App
{
    internal readonly struct CombatMotionDefinition
    {
        public static CombatMotionDefinition Disabled => default;

        public NormalizedActionWindow Window { get; }
        public bool OverridePlanarVelocity { get; }
        public float PlanarDistance { get; }
        public CharacterActionVerticalMode VerticalMode { get; }
        public float VerticalVelocity { get; }
        public float VerticalImpulse { get; }
        public bool OverrideVerticalVelocityOnImpulse { get; }
        public bool FaceTarget { get; }
        public float TargetStoppingDistance { get; }
        public bool HasTranslation =>
            Window.IsEnabled &&
            (OverridePlanarVelocity ||
             VerticalMode != CharacterActionVerticalMode.PreserveVelocity ||
             !Mathf.Approximately(VerticalImpulse, 0.0f));
        public bool IsEnabled => Window.IsEnabled && (HasTranslation || FaceTarget);

        public CombatMotionDefinition(
            NormalizedActionWindow window,
            bool overridePlanarVelocity,
            float planarDistance,
            CharacterActionVerticalMode verticalMode,
            float verticalVelocity,
            float verticalImpulse,
            bool overrideVerticalVelocityOnImpulse,
            bool faceTarget,
            float targetStoppingDistance)
        {
            if (!window.IsEnabled)
            {
                throw new ArgumentException("Combat motion requires an enabled action window.", nameof(window));
            }

            if (planarDistance < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(planarDistance));
            }

            if (targetStoppingDistance < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetStoppingDistance));
            }

            Window = window;
            OverridePlanarVelocity = overridePlanarVelocity;
            PlanarDistance = overridePlanarVelocity ? planarDistance : 0.0f;
            VerticalMode = verticalMode;
            VerticalVelocity = verticalMode != CharacterActionVerticalMode.PreserveVelocity
                ? verticalVelocity
                : 0.0f;
            VerticalImpulse = verticalImpulse;
            OverrideVerticalVelocityOnImpulse = overrideVerticalVelocityOnImpulse;
            FaceTarget = faceTarget;
            TargetStoppingDistance = targetStoppingDistance;
        }
    }
}
