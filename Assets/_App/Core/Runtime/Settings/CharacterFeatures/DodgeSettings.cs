using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeSettings", menuName = "Project S/Character/Dodge")]
    public sealed class DodgeSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;

        [Tooltip(
            "Speed over normalized dodge time. Normalized to an average of one, " +
            "so the shape changes the feel while the impulse keeps owning distance.")]
        [SerializeField] private AnimationCurve _speedProfile = new AnimationCurve(
            new Keyframe(0.0f, 1.9f),
            new Keyframe(0.35f, 1.35f),
            new Keyframe(0.75f, 0.35f),
            new Keyframe(1.0f, 0.0f));
        [SerializeField, Min(0.0f)] private float _inputBufferDuration = 0.12f;

        [Header("Invulnerability Window")]
        [SerializeField] private bool _useInvulnerability = true;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityStart = 0.08f;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityEnd = 0.58f;

        public bool IsEnabled => _canEverDodge;
        public float Impulse => _dodgeImpulse;

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
        public float Duration => Mathf.Max(0.01f, _dodgeDuration);
        public float InputBufferDuration => Mathf.Max(0.0f, _inputBufferDuration);
        public bool HasInvulnerability =>
            _useInvulnerability && InvulnerabilityEnd > InvulnerabilityStart;
        public float InvulnerabilityStart => Mathf.Clamp01(_invulnerabilityStart);
        public float InvulnerabilityEnd => Mathf.Clamp01(_invulnerabilityEnd);

        private void OnValidate()
        {
            _dodgeDuration = Mathf.Max(0.01f, _dodgeDuration);
            _inputBufferDuration = Mathf.Max(0.0f, _inputBufferDuration);
            _invulnerabilityStart = Mathf.Clamp01(_invulnerabilityStart);
            _invulnerabilityEnd = Mathf.Clamp(
                _invulnerabilityEnd,
                _invulnerabilityStart,
                1.0f);
        }
    }
}
