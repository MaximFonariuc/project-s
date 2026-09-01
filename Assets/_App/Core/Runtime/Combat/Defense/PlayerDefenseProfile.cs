using System;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "PlayerDefenseProfile",
        menuName = "Project S/Combat/Player Defense Profile")]
    public sealed class PlayerDefenseProfile : ScriptableObject
    {
        [SerializeField, Range(1.0f, 360.0f)] private float _guardArcDegrees = 140.0f;
        [SerializeField, Min(0.0f)] private float _parryWindowSeconds = 0.12f;
        [SerializeField, Min(0.0f)] private float _parryRecoverySeconds = 0.35f;
        [SerializeField, Range(0.0f, 1.0f)] private float _movementScale = 0.35f;
        [SerializeField, Min(0.0f)] private float _blockPushbackSpeed = 1.75f;

        internal PlayerDefenseSettings CreateSettings()
        {
            return new PlayerDefenseSettings(
                _guardArcDegrees,
                _parryWindowSeconds,
                _parryRecoverySeconds,
                _movementScale,
                _blockPushbackSpeed);
        }

        private void OnValidate()
        {
            _guardArcDegrees = Mathf.Clamp(_guardArcDegrees, 1.0f, 360.0f);
            _parryWindowSeconds = Mathf.Max(0.0f, _parryWindowSeconds);
            _parryRecoverySeconds = Mathf.Max(
                _parryWindowSeconds,
                _parryRecoverySeconds);
            _movementScale = Mathf.Clamp01(_movementScale);
            _blockPushbackSpeed = Mathf.Max(0.0f, _blockPushbackSpeed);
        }
    }

    internal sealed class PlayerDefenseSettings
    {
        public float GuardArcDegrees { get; }
        public double ParryWindowSeconds { get; }
        public double ParryRecoverySeconds { get; }
        public PlayerMovementIntent MovementIntent { get; }
        public float BlockPushbackSpeed { get; }

        public PlayerDefenseSettings(
            float guardArcDegrees,
            float parryWindowSeconds,
            float parryRecoverySeconds,
            float movementScale,
            float blockPushbackSpeed)
        {
            RequireFinite(guardArcDegrees, nameof(guardArcDegrees));
            RequireFinite(parryWindowSeconds, nameof(parryWindowSeconds));
            RequireFinite(parryRecoverySeconds, nameof(parryRecoverySeconds));
            RequireFinite(movementScale, nameof(movementScale));
            RequireFinite(blockPushbackSpeed, nameof(blockPushbackSpeed));
            if (guardArcDegrees <= 0.0f || guardArcDegrees > 360.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(guardArcDegrees));
            }

            if (parryWindowSeconds < 0.0f ||
                parryRecoverySeconds < parryWindowSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(parryRecoverySeconds));
            }

            if (movementScale < 0.0f || movementScale > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementScale));
            }

            if (blockPushbackSpeed < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blockPushbackSpeed));
            }

            GuardArcDegrees = guardArcDegrees;
            ParryWindowSeconds = parryWindowSeconds;
            ParryRecoverySeconds = parryRecoverySeconds;
            MovementIntent = new PlayerMovementIntent(
                movementScale,
                true,
                true,
                false);
            BlockPushbackSpeed = blockPushbackSpeed;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
