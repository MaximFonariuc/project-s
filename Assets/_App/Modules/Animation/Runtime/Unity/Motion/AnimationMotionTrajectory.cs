using System;
using UnityEngine;

namespace ModularAnimation.Unity
{
    [CreateAssetMenu(
        fileName = "AnimationMotionTrajectory",
        menuName = "Modular Animation/Motion Trajectory")]
    public sealed class AnimationMotionTrajectory : ScriptableObject
    {
        [SerializeField] private AnimationClip _sourceClip;
        [SerializeField] private string _motionRootPath;
        [SerializeField, Min(0.0001f)] private float _sourceDuration;
        [SerializeField, Min(1)] private int _sampleRate = 60;
        [SerializeField] private bool _hasPlanarTranslation;
        [SerializeField] private Vector3[] _positions = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] _rotations = Array.Empty<Quaternion>();

        public AnimationClip SourceClip => _sourceClip;
        public string MotionRootPath => _motionRootPath;
        public float SourceDuration => _sourceDuration;
        public int SampleRate => _sampleRate;
        public int SampleCount => _positions?.Length ?? 0;
        public bool HasPlanarTranslation => IsValid && _hasPlanarTranslation;
        public bool IsValid =>
            _sourceClip != null &&
            _sourceDuration > 0.0f &&
            _positions != null &&
            _positions.Length >= 2 &&
            _rotations != null &&
            _rotations.Length == _positions.Length;
        public Vector3 TotalDisplacement =>
            IsValid ? _positions[_positions.Length - 1] - _positions[0] : Vector3.zero;

        public Vector3 EvaluatePosition(float normalizedTime)
        {
            if (!IsValid)
            {
                return Vector3.zero;
            }

            GetSampleInterval(normalizedTime, out int from, out int to, out float blend);
            return Vector3.LerpUnclamped(_positions[from], _positions[to], blend);
        }

        public Quaternion EvaluateRotation(float normalizedTime)
        {
            if (!IsValid)
            {
                return Quaternion.identity;
            }

            GetSampleInterval(normalizedTime, out int from, out int to, out float blend);
            return Quaternion.SlerpUnclamped(_rotations[from], _rotations[to], blend);
        }

#if UNITY_EDITOR
        public void SetBakedData(
            AnimationClip sourceClip,
            string motionRootPath,
            int sampleRate,
            Vector3[] positions,
            Quaternion[] rotations)
        {
            if (sourceClip == null)
            {
                throw new ArgumentNullException(nameof(sourceClip));
            }

            if (string.IsNullOrWhiteSpace(motionRootPath))
            {
                throw new ArgumentException("A motion root path is required.", nameof(motionRootPath));
            }

            if (positions == null || positions.Length < 2)
            {
                throw new ArgumentException("At least two position samples are required.", nameof(positions));
            }

            if (rotations == null || rotations.Length != positions.Length)
            {
                throw new ArgumentException(
                    "Rotation samples must match the position sample count.",
                    nameof(rotations));
            }

            _sourceClip = sourceClip;
            _motionRootPath = motionRootPath;
            _sourceDuration = sourceClip.length;
            _sampleRate = Mathf.Max(1, sampleRate);
            _positions = positions;
            _rotations = rotations;
            _hasPlanarTranslation = false;
            Vector3 origin = positions[0];
            for (int i = 1; i < positions.Length; i++)
            {
                Vector3 delta = positions[i] - origin;
                if (delta.x * delta.x + delta.z * delta.z > 0.0000001f)
                {
                    _hasPlanarTranslation = true;
                    break;
                }
            }
        }
#endif

        private void GetSampleInterval(
            float normalizedTime,
            out int from,
            out int to,
            out float blend)
        {
            float sample = Mathf.Clamp01(normalizedTime) * (_positions.Length - 1);
            from = Mathf.Min(Mathf.FloorToInt(sample), _positions.Length - 2);
            to = from + 1;
            blend = sample - from;
        }
    }
}
