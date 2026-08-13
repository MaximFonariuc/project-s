using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "SprintSpeedSettings",
        menuName = "Project S/Character/Sprint Speed")]
    public sealed class SprintSpeedSettings : CharacterFeatureSettings
    {
        [SerializeField] private float _maxSprintSpeed = 10.0f;

        public float MaxSpeed => Mathf.Max(0.0f, _maxSprintSpeed);
    }
}
