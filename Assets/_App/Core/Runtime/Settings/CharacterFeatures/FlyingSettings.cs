using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "FlyingSettings", menuName = "Project S/Character/Flying")]
    public sealed class FlyingSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverFly = true;

        public bool IsEnabled => _canEverFly;
    }
}
