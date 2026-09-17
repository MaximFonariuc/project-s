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

        [Header("Invulnerability Window")]
        [SerializeField] private bool _useInvulnerability;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityStart = 0.05f;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityEnd = 0.6f;

        public bool IsEnabled => _canEverAirDash;
        public float Impulse => _impulse;
        public float Duration => Mathf.Max(0.01f, _duration);
        public int ChargesPerAirtime => Mathf.Max(1, _chargesPerAirtime);
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