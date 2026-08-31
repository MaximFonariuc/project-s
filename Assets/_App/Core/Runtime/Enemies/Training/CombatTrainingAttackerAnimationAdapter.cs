using ModularAnimation;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatTrainingAttacker))]
    [RequireComponent(typeof(CharacterAnimationHost))]
    [AddComponentMenu("Project S/Enemies/Combat Training Attacker Animation Adapter")]
    public sealed class CombatTrainingAttackerAnimationAdapter : MonoBehaviour
    {
        [SerializeField] private CombatTrainingAttacker _attacker;
        [SerializeField] private CharacterAnimationHost _animationHost;
        [SerializeField] private CharacterReactionHost _reactionHost;

        private PlayerAnimationCoordinator _animation;
        private CombatTrainingAttackPhase? _observedPhase;
        private bool _ownsAttackPresentation;
        private bool _ownsParryPresentation;

        private void Awake()
        {
            ResolveReferences();
            if (_attacker == null || _animationHost == null ||
                !_animationHost.TryGetAnimation(out _animation))
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingAttackerAnimationAdapter)} requires " +
                    "an attacker and initialized animation host.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_attacker == null || _animation == null)
            {
                return;
            }

            _attacker.StateChanged += OnAttackStateChanged;
            Observe(_attacker.Snapshot);
        }

        private void OnDisable()
        {
            if (_attacker != null)
            {
                _attacker.StateChanged -= OnAttackStateChanged;
            }

            ReleaseParryPresentationIfOwned();
            ReleaseAttackPresentation(true);
            _observedPhase = null;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnAttackStateChanged(CombatTrainingAttackSnapshot snapshot)
        {
            Observe(snapshot);
        }

        private void Observe(CombatTrainingAttackSnapshot snapshot)
        {
            if (_observedPhase == snapshot.Phase)
            {
                return;
            }

            CombatTrainingAttackPhase? previousPhase = _observedPhase;
            bool wasParryStagger =
                previousPhase == CombatTrainingAttackPhase.ParryStagger;
            _observedPhase = snapshot.Phase;

            if (snapshot.Phase == CombatTrainingAttackPhase.ParryStagger)
            {
                _ownsAttackPresentation = false;
                PlayParryStagger();
                return;
            }

            if (wasParryStagger)
            {
                ReleaseParryPresentationIfOwned();
            }

            if (snapshot.Phase == CombatTrainingAttackPhase.Windup)
            {
                PlayAttack();
                return;
            }

            if (previousPhase.HasValue &&
                IsAttackPhase(previousPhase.Value) &&
                !IsAttackPhase(snapshot.Phase))
            {
                bool completedNormally =
                    previousPhase == CombatTrainingAttackPhase.Recovery &&
                    snapshot.Phase == CombatTrainingAttackPhase.Waiting;
                ReleaseAttackPresentation(!completedNormally);
            }
        }

        private void PlayAttack()
        {
            var request = new AnimationActionRequest(
                PlayerAnimatorActions.LightAttack,
                0,
                _attacker.AttackActionDuration);
            if (_animation.TryPlayAction(
                    PlayerAnimationSource.Combat,
                    in request,
                    out _))
            {
                _ownsAttackPresentation = true;
                return;
            }

            Debug.LogWarning(
                "Training attack has no compatible BaseAttack1 state.",
                this);
        }

        private void PlayParryStagger()
        {
            AnimationPlayback playback = _animation.CrossFade(
                PlayerAnimationSource.Reaction,
                PlayerAnimationSlots.Reaction(
                    CharacterReactionStateId.HitStun),
                _attacker.ParryStaggerDuration);
            if (playback.IsValid)
            {
                _ownsParryPresentation = true;
                return;
            }

            Debug.LogWarning(
                "Training parry stagger has no compatible reaction state.",
                this);
        }

        private void ReleaseAttackPresentation(bool interrupted)
        {
            if (!_ownsAttackPresentation || _animation == null)
            {
                return;
            }

            _ownsAttackPresentation = false;
            if (interrupted)
            {
                _animation.Cancel(PlayerAnimationSource.Combat);
            }
            else
            {
                _animation.ReleaseAll(PlayerAnimationSource.Combat);
            }
        }

        private void ReleaseParryPresentationIfOwned()
        {
            if (!_ownsParryPresentation || _animation == null)
            {
                return;
            }

            _ownsParryPresentation = false;
            if (_reactionHost?.State is { BlocksActions: true })
            {
                return;
            }

            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
        }

        private static bool IsAttackPhase(CombatTrainingAttackPhase phase)
        {
            return phase is CombatTrainingAttackPhase.Windup or
                CombatTrainingAttackPhase.Active or
                CombatTrainingAttackPhase.Recovery;
        }

        private void ResolveReferences()
        {
            if (_attacker == null)
            {
                _attacker = GetComponent<CombatTrainingAttacker>();
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
