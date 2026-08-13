using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "GlideSettings", menuName = "Project S/Character/Glide")]
    public sealed class GlideSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverGlide = true;
        [SerializeField] private float _maxFallSpeedGliding = 1.0f;

        public bool IsEnabled => _canEverGlide;
        public float MaxFallSpeed => Mathf.Max(0.0f, _maxFallSpeedGliding);
    }
}
