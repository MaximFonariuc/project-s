using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LocomotionAnimationSettings",
        menuName = "Project S/Character/Locomotion Animation")]
    public sealed class LocomotionAnimationSettings : CharacterFeatureSettings
    {
        [Header("Landing Classification")]
        [SerializeField, Min(0.0f)] private float _minimumAirborneSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float _mediumLandingSpeed = 7.0f;
        [SerializeField, Min(0.01f)] private float _hardLandingSpeed = 12.0f;

        [Header("Presentation Hold")]
        [SerializeField, Min(0.0f)] private float _minimumLandingSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float _softLandingSeconds = 0.28f;
        [SerializeField, Min(0.01f)] private float _mediumLandingSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float _hardLandingSeconds = 0.70f;
        [SerializeField, Range(0.0f, 1.0f)] private float _movementInterruptThreshold = 0.12f;

        internal PlayerLocomotionAnimationPolicy CreatePolicy()
        {
            return new PlayerLocomotionAnimationPolicy(
                _minimumAirborneSeconds,
                _mediumLandingSpeed,
                _hardLandingSpeed,
                _minimumLandingSeconds,
                _softLandingSeconds,
                _mediumLandingSeconds,
                _hardLandingSeconds,
                _movementInterruptThreshold);
        }
    }
}
