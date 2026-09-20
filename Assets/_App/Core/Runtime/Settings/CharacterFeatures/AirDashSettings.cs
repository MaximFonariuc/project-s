using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "AirDashSettings", menuName = "Project S/Character/Air Dash")]
    public sealed class AirDashSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverAirDash = true;
        [SerializeField] private float _impulse = 12.0f;
        [SerializeField, Min(0.01f)] private float _duration = 0.32f;
        [SerializeField, Min(1)] private int _chargesPerAirtime = 1;
        [SerializeField] private AnimationCurve _speedProfile = new AnimationCurve(
            new Keyframe(0.0f, 1.8f),
            new Keyframe(0.4f, 1.25f),
            new Keyframe(0.8f, 0.45f),
            new Keyframe(1.0f, 0.2f));

        [Header("Invulnerability Window")]
        [SerializeField] private bool _useInvulnerability;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityStart = 0.05f;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityEnd = 0.6f;

        public bool IsEnabled => _canEverAirDash;
        public float Impulse => _impulse;
        public float Duration => Mathf.Max(0.01f, _duration);
        public int ChargesPerAirtime => Mathf.Max(1, _chargesPerAirtime);

        public float SampleNormalizedSpeed(float normalizedTime)
        {
            if (_speedProfile == null || _speedProfile.length == 0)
            {
                return 1.0f;
            }

            float mean = 0.0f;
            const int samples = 32;
            for (int i = 0; i < samples; i++)
            {
                mean += _speedProfile.Evaluate((i + 0.5f) / samples);
            }

            mean /= samples;
            return mean > 0.0001f
                ? _speedProfile.Evaluate(Mathf.Clamp01(normalizedTime)) / mean
                : 1.0f;
        }

        public bool HasInvulnerability =>
            _useInvulnerability && InvulnerabilityEnd > InvulnerabilityStart;
        public float InvulnerabilityStart => Mathf.Clamp01(_invulnerabilityStart);
        public float InvulnerabilityEnd => Mathf.Clamp01(_invulnerabilityEnd);

        private void OnValidate()
        {
            _duration = Mathf.Max(0.01f, _duration);
            _chargesPerAirtime = Mathf.Max(1, _chargesPerAirtime);
            _invulnerabilityStart = Mathf.Clamp01(_invulnerabilityStart);
            _invulnerabilityEnd = Mathf.Clamp(
                _invulnerabilityEnd,
                _invulnerabilityStart,
                1.0f);
        }
    }
}