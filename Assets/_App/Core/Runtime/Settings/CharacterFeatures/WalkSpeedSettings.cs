using UnityEngine;
using UnityEngine.Serialization;

namespace _App
{
    [CreateAssetMenu(
        fileName = "WalkSpeedSettings",
        menuName = "Project S/Character/Walk Speed")]
    public sealed class WalkSpeedSettings : CharacterFeatureSettings
    {
        [FormerlySerializedAs("_maxSprintSpeed")]
        [SerializeField] private float _maxWalkSpeed = 3.0f;

        public float MaxSpeed => Mathf.Max(0.0f, _maxWalkSpeed);
    }
}
