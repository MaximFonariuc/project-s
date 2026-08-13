using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "GroundOrientationSettings",
        menuName = "Project S/Character/Ground Orientation")]
    public sealed class GroundOrientationSettings : CharacterFeatureSettings
    {
        [SerializeField] private float _maxSlopeAngle = 30.0f;
        [SerializeField] private float _alignRate = 10.0f;
        [SerializeField] private float _rayOffset = 0.1f;
        [SerializeField] private LayerMask _groundMask = 1;
        [SerializeField] private bool _drawRays = true;

        public float MaxSlopeAngle => _maxSlopeAngle;
        public float AlignRate => _alignRate;
        public float RayOffset => _rayOffset;
        public LayerMask GroundMask => _groundMask;
        public bool DrawRays => _drawRays;
    }
}
