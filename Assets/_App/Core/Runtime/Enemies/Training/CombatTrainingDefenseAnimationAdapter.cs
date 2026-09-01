using ModularAnimation;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatTrainingDefense))]
    [RequireComponent(typeof(CharacterAnimationHost))]
    [AddComponentMenu("Project S/Enemies/Combat Training Defense Animation Adapter")]
    public sealed class CombatTrainingDefenseAnimationAdapter : MonoBehaviour
    {
        [SerializeField] private CombatTrainingDefense _defense;
        [SerializeField] private CharacterAnimationHost _animationHost;

        private PlayerAnimationCoordinator _animation;
        private bool _ownsPresentation;

        private void Awake()
        {
            ResolveReferences();
            if (_defense == null || _animationHost == null ||
                !_animationHost.TryGetAnimation(out _animation))
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingDefenseAnimationAdapter)} requires " +
                    "defense and an initialized animation host.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_defense == null || _animation == null)
            {
                return;
            }

            _defense.StateChanged += OnStateChanged;
            Present(_defense.Snapshot);
        }

        private void OnDisable()
        {
            if (_defense != null)
            {
                _defense.StateChanged -= OnStateChanged;
            }

            ReleasePresentation();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnStateChanged(CombatTrainingDefenseSnapshot snapshot)
        {
            Present(snapshot);
        }

        private void Present(CombatTrainingDefenseSnapshot snapshot)
        {
            if (!snapshot.BlocksActions)
            {
                ReleasePresentation();
                return;
            }

            AnimationActionId action = snapshot.State == CombatTrainingDefenseState.Parry
                ? PlayerAnimatorActions.Parry
                : PlayerAnimatorActions.Block;
            var request = new AnimationActionRequest(
                action,
                0,
                snapshot.RemainingDuration);
            _ownsPresentation = _animation.TryPlayAction(
                PlayerAnimationSource.Defense,
                in request,
                out _);
        }

        private void ReleasePresentation()
        {
            if (!_ownsPresentation || _animation == null)
            {
                return;
            }

            _ownsPresentation = false;
            _animation.Cancel(PlayerAnimationSource.Defense);
            _animation.ReleaseAll(PlayerAnimationSource.Defense);
        }

        private void ResolveReferences()
        {
            if (_defense == null)
            {
                _defense = GetComponent<CombatTrainingDefense>();
            }

            if (_animationHost == null)
            {
                _animationHost = GetComponent<CharacterAnimationHost>();
            }
        }
    }
}
