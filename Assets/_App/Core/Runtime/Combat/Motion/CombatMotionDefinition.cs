using System;
using UnityEngine;

namespace _App
{
    internal enum CombatMotionTargetSource
    {
        LockOnTarget = 0,
        PreviousConfirmedHit = 1,
        PreviousConfirmedHitOrLockOnTarget = 2
    }

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
        public CombatMotionTargetSource TargetSource { get; }
        public float TargetStoppingDistance { get; }
        public bool TrackTargetHeight { get; }
        public float TargetHeightOffset { get; }
        public float TargetHeightTolerance { get; }
        public float TargetHeightCorrectionSpeed { get; }
        public float MaxTargetVerticalSpeed { get; }
        public bool UsesPreviousConfirmedHitTarget =>
            TargetSource is CombatMotionTargetSource.PreviousConfirmedHit or
                CombatMotionTargetSource.PreviousConfirmedHitOrLockOnTarget;
        public bool HasTranslation =>
            Window.IsEnabled &&
            (OverridePlanarVelocity ||
             VerticalMode != CharacterActionVerticalMode.PreserveVelocity ||
             !Mathf.Approximately(VerticalImpulse, 0.0f) ||
             TrackTargetHeight);
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
            CombatMotionTargetSource targetSource,
            float targetStoppingDistance,
            bool trackTargetHeight,
            float targetHeightOffset,
            float targetHeightTolerance,
            float targetHeightCorrectionSpeed,
            float maxTargetVerticalSpeed)
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

            if (targetHeightTolerance < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHeightTolerance));
            }

            if (trackTargetHeight && targetHeightCorrectionSpeed <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHeightCorrectionSpeed));
            }

            if (trackTargetHeight && maxTargetVerticalSpeed <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTargetVerticalSpeed));
            }

            Window = window;
            OverridePlanarVelocity = overridePlanarVelocity;
            PlanarDistance = OverridePlanarVelocity ? planarDistance : 0.0f;
            VerticalMode = verticalMode;
            VerticalVelocity = verticalMode != CharacterActionVerticalMode.PreserveVelocity
                ? verticalVelocity
                : 0.0f;
            VerticalImpulse = verticalImpulse;
            OverrideVerticalVelocityOnImpulse = overrideVerticalVelocityOnImpulse;
            FaceTarget = faceTarget;
            TargetSource = targetSource;
            TargetStoppingDistance = targetStoppingDistance;
            TrackTargetHeight = trackTargetHeight;
            TargetHeightOffset = trackTargetHeight ? targetHeightOffset : 0.0f;
            TargetHeightTolerance = trackTargetHeight ? targetHeightTolerance : 0.0f;
            TargetHeightCorrectionSpeed = trackTargetHeight
                ? targetHeightCorrectionSpeed
                : 0.0f;
            MaxTargetVerticalSpeed = trackTargetHeight ? maxTargetVerticalSpeed : 0.0f;
        }
    }
}
