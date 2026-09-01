using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LandingRecoverySettings",
        menuName = "Project S/Character/Landing Recovery")]
    public sealed class LandingRecoverySettings : CharacterFeatureSettings
    {
        [Header("Impact Classification")]
        [SerializeField, Min(0.0f)] private float _mediumImpactSpeed = 7.0f;
        [SerializeField, Min(0.0f)] private float _hardImpactSpeed = 10.0f;

        [Header("Action Recovery")]
        [SerializeField, Min(0.0f)] private float _mediumRecoveryDuration = 0.64f;
        [SerializeField, Min(0.0f)] private float _hardRecoveryDuration = 0.95f;

        public float MediumImpactSpeed => Mathf.Max(0.0f, _mediumImpactSpeed);
        public float HardImpactSpeed => Mathf.Max(MediumImpactSpeed, _hardImpactSpeed);
        public float MediumRecoveryDuration => Mathf.Max(0.0f, _mediumRecoveryDuration);
        public float HardRecoveryDuration => Mathf.Max(0.0f, _hardRecoveryDuration);

        private void OnValidate()
        {
            _mediumImpactSpeed = Mathf.Max(0.0f, _mediumImpactSpeed);
            _hardImpactSpeed = Mathf.Max(_mediumImpactSpeed, _hardImpactSpeed);
            _mediumRecoveryDuration = Mathf.Max(0.0f, _mediumRecoveryDuration);
            _hardRecoveryDuration = Mathf.Max(0.0f, _hardRecoveryDuration);
        }
    }
}
