using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LadderTraversalSettings",
        menuName = "Project S/Character/Ladder Traversal")]
    public sealed class LadderTraversalSettings : CharacterFeatureSettings
    {
        [SerializeField] private LayerMask _ladderMask = 1;

        [Header("Climb Geometry")]
        [SerializeField, Min(0.01f)] private float _ascendCycleRise = 0.52f;
        [SerializeField, Min(0.1f)] private float _approachSpeed = 2.5f;
        [SerializeField, Min(0.01f)] private float _minApproachSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float _maxApproachSeconds = 0.6f;
        [SerializeField, Min(1.0f)] private float _approachTurnSpeed = 540.0f;
        [SerializeField, Min(0.01f)] private float _climbRateBlendSeconds = 0.12f;
        [SerializeField, Min(0.0f)] private float _hangBlendLeadSeconds = 0.18f;
        [SerializeField] private float[] _restPhases = { 0.22f, 0.74f };

        [Header("Exit Top Root Motion")]
        [SerializeField] private AnimationCurve _exitTopRiseProfile = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);
        [SerializeField] private AnimationCurve _exitTopForwardProfile = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);

        [Header("Enter Top Root Motion")]
        [SerializeField] private AnimationCurve _enterTopDescentProfile = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);
        [SerializeField] private AnimationCurve _enterTopBackwardProfile = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);

        [Header("Clip Lengths (seconds)")]
        [SerializeField, Min(0.01f)] private float _enterTopFrontSeconds = 1.8333334f;
        [SerializeField, Min(0.01f)] private float _enterTopBackSeconds = 1.8333334f;
        [SerializeField, Min(0.01f)] private float _enterBottomSeconds = 0.4333333f;
        [SerializeField, Min(0.01f)] private float _idleSeconds = 1.0f;
        [SerializeField, Min(0.01f)] private float _ascendSeconds = 1.0f;
        [SerializeField, Min(0.01f)] private float _descendSeconds = 1.0f;
        [SerializeField, Min(0.01f)] private float _exitTopSeconds = 1.8333334f;
        [SerializeField, Min(0.01f)] private float _exitBottomSeconds = 0.6666667f;

        public LayerMask LadderMask => _ladderMask;

        public float AscendCycleRise => _ascendCycleRise;
        public float ApproachSpeed => _approachSpeed;
        public float MinApproachSeconds => _minApproachSeconds;
        public float MaxApproachSeconds => Mathf.Max(_maxApproachSeconds, _minApproachSeconds);
        public float ApproachTurnSpeed => _approachTurnSpeed;
        public float ClimbRateBlendSeconds => _climbRateBlendSeconds;
        public float HangBlendLeadSeconds => _hangBlendLeadSeconds;
        public IReadOnlyList<float> RestPhases => _restPhases;
        public AnimationCurve ExitTopRiseProfile => _exitTopRiseProfile;
        public AnimationCurve ExitTopForwardProfile => _exitTopForwardProfile;
        public AnimationCurve EnterTopDescentProfile => _enterTopDescentProfile;
        public AnimationCurve EnterTopBackwardProfile => _enterTopBackwardProfile;

        public float EnterTopFrontSeconds => _enterTopFrontSeconds;
        public float EnterTopBackSeconds => _enterTopBackSeconds;
        public float EnterBottomSeconds => _enterBottomSeconds;
        public float IdleSeconds => _idleSeconds;
        public float AscendSeconds => _ascendSeconds;
        public float DescendSeconds => _descendSeconds;
        public float ExitTopSeconds => _exitTopSeconds;
        public float ExitBottomSeconds => _exitBottomSeconds;
    }
}