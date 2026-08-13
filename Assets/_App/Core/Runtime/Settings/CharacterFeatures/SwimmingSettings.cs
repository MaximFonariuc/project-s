using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "SwimmingSettings", menuName = "Project S/Character/Swimming")]
    public sealed class SwimmingSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverSwim = true;

        public bool IsEnabled => _canEverSwim;
    }
}
