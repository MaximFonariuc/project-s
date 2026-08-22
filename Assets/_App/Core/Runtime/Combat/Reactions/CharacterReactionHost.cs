using System;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Project S/Combat/Character Reaction Host")]
    public sealed class CharacterReactionHost : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private Animator _animator;
        [SerializeField] private CharacterReactionProfile _profile;
        [SerializeField] private MonoBehaviour _impactMotorSource;

        private XControllerCharacterAnimationDriver _animationDriver;
        private IStateMachine<CharacterReactionStateId> _stateMachine;
        private CharacterReactionController _reaction;

        public ICharacterReactionState State => _reaction;

        private void Awake()
        {
            ResolveMissingReferences();
            if (_health == null || _animator == null ||
                _impactMotorSource is not ICharacterImpactMotor impactMotor)
            {
                Debug.LogError(
                    "Character Reaction Host requires Health, Animator and an ICharacterImpactMotor MonoBehaviour.",
                    this);
                enabled = false;
                return;
            }

            CharacterReactionSettings settings = _profile != null
                ? _profile.CreateSettings()
                : CharacterReactionSettings.CreateDefaults();
            _animationDriver = new XControllerCharacterAnimationDriver(_animator);
            var animation = new PlayerAnimationCoordinator(_animationDriver);
            var presentation = new CharacterReactionAnimationAdapter(animation);
            _stateMachine = CharacterReactionStateMachineFactory.Create();
            _reaction = new CharacterReactionController(
                _health,
                new CharacterPoise(settings),
                impactMotor,
                presentation,
                _stateMachine,
                settings);
        }

        private void OnEnable()
        {
            _reaction?.Enable();
        }

        private void Update()
        {
            _reaction?.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            _reaction?.Disable();
        }

        private void OnDestroy()
        {
            _reaction?.Dispose();
            (_stateMachine as IDisposable)?.Dispose();
            _animationDriver?.Dispose();
            _reaction = null;
            _stateMachine = null;
            _animationDriver = null;
        }

        private void Reset()
        {
            ResolveMissingReferences();
        }

        private void OnValidate()
        {
            ResolveMissingReferences();
        }

        public void Configure(
            Health health,
            Animator animator,
            CharacterReactionProfile profile,
            MonoBehaviour impactMotorSource)
        {
            _health = health;
            _animator = animator;
            _profile = profile;
            _impactMotorSource = impactMotorSource;
        }

        private void ResolveMissingReferences()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_impactMotorSource == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is ICharacterImpactMotor)
                    {
                        _impactMotorSource = behaviour;
                        break;
                    }
                }
            }
        }
    }
}
