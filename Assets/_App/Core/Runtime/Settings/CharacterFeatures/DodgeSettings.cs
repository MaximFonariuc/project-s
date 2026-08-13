using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeSettings", menuName = "Project S/Character/Dodge")]
    public sealed class DodgeSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;

        public bool IsEnabled => _canEverDodge;
        public float Impulse => _dodgeImpulse;
        public float Duration => Mathf.Max(0.01f, _dodgeDuration);
    }
}
