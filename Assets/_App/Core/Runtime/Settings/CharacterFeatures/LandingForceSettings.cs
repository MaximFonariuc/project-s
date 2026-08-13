using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LandingForceSettings",
        menuName = "Project S/Character/Landing Force")]
    public sealed class LandingForceSettings : CharacterFeatureSettings
    {
        [SerializeField] private float _landingForceScale = 1.0f;

        public float Scale => _landingForceScale;
    }
}
