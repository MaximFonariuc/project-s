using ModularAnimation;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatTrainingApproach))]
    [RequireComponent(typeof(CharacterAnimationHost))]
    [AddComponentMenu("Project S/Enemies/Combat Training Approach Animation Adapter")]
    public sealed class CombatTrainingApproachAnimationAdapter : MonoBehaviour
    {
        [SerializeField] private CombatTrainingApproach _approach;
        [SerializeField] private CharacterAnimationHost _animationHost;
        [SerializeField] private CharacterReactionHost _reactionHost;
        [SerializeField] private CharacterGait _gait = CharacterGait.Walk;

        private void Awake()
        {
            ResolveReferences();
            if (_approach == null || _animationHost == null)
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingApproachAnimationAdapter)} requires " +
                    $"{nameof(CombatTrainingApproach)} and " +
                    $"{nameof(CharacterAnimationHost)}.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (IsPresentationSuspended())
            {
                return;
            }

            CombatTrainingApproachSnapshot snapshot = _approach.Snapshot;
            var frame = new CharacterLocomotionFrame(
                snapshot.ForwardSpeed,
                snapshot.LateralSpeed,
                snapshot.VerticalSpeed,
                snapshot.NormalizedSpeed,
                snapshot.IsGrounded,
                false,
                _gait,
                CharacterLocomotionMode.Grounded);

            _animationHost.TryApplyLocomotion(in frame, Time.deltaTime);
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private bool IsPresentationSuspended()
        {
            return _reactionHost != null &&
                   _reactionHost.State is { BlocksActions: true };
        }

        private void ResolveReferences()
        {
            if (_approach == null)
            {
                _approach = GetComponent<CombatTrainingApproach>();
            }

            if (_animationHost == null)
            {
                _animationHost = GetComponent<CharacterAnimationHost>();
            }

            if (_reactionHost == null)
            {
                _reactionHost = GetComponent<CharacterReactionHost>();
            }
        }
    }
}
