using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeSettings", menuName = "Project S/Character/Dodge")]
    public sealed class DodgeSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;
        [SerializeField, Min(0.0f)] private float _inputBufferDuration = 0.12f;

        [Header("Invulnerability Window")]
        [SerializeField] private bool _useInvulnerability = true;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityStart = 0.08f;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityEnd = 0.58f;

        public bool IsEnabled => _canEverDodge;
        public float Impulse => _dodgeImpulse;
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
