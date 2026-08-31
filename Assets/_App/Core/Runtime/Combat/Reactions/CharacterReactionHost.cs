using System;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterAnimationHost))]
    [AddComponentMenu("Project S/Combat/Character Reaction Host")]
    public sealed class CharacterReactionHost : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private CharacterAnimationHost _animationHost;
        [SerializeField] private CharacterReactionProfile _profile;
        [SerializeField] private MonoBehaviour _impactMotorSource;

        private IStateMachine<CharacterReactionStateId> _stateMachine;
        private CharacterReactionController _reaction;

        public ICharacterReactionState State => _reaction;

        private void Awake()
        {
            ResolveMissingReferences();
            if (_health == null || _animationHost == null ||
                !_animationHost.TryGetAnimation(out PlayerAnimationCoordinator animation) ||
                _impactMotorSource is not ICharacterImpactMotor impactMotor)
            {
                Debug.LogError(
                    "Character Reaction Host requires Health, Character Animation Host " +
                    "and an ICharacterImpactMotor MonoBehaviour.",
                    this);
                enabled = false;
                return;
            }

            CharacterReactionSettings settings = _profile != null
                ? _profile.CreateSettings()
                : CharacterReactionSettings.CreateDefaults();
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
            _reaction = null;
            _stateMachine = null;
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
            CharacterAnimationHost animationHost,
            CharacterReactionProfile profile,
            MonoBehaviour impactMotorSource)
        {
            _health = health;
            _animationHost = animationHost;
            _profile = profile;
            _impactMotorSource = impactMotorSource;
        }

        private void ResolveMissingReferences()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_animationHost == null)
            {
                _animationHost = GetComponent<CharacterAnimationHost>();
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
