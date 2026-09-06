using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LadderTraversalSettings",
        menuName = "Project S/Character/Ladder Traversal")]
    public sealed class LadderTraversalSettings : CharacterFeatureSettings
    {
        [SerializeField] private LayerMask _ladderMask = 1;

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
