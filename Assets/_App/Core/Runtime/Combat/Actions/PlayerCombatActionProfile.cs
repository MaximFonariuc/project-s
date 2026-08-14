using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "PlayerCombatActionProfile", menuName = "Combat/Player Action Profile")]
    public sealed class PlayerCombatActionProfile : ScriptableObject
    {
        [SerializeField] private CombatActionAuthoring _weaponTransition =
            CombatActionAuthoring.CreatePrototypeWeaponTransition();
        [SerializeField] private CombatInputBufferAuthoring _inputBuffer = new();

        [Header("Action Graph")]
        [SerializeField] private List<PlayerCombatActionAuthoring> _actions = new();
        [SerializeField] private List<PlayerCombatEntryAuthoring> _entries = new();
        [SerializeField] private List<PlayerCombatTransitionAuthoring> _transitions = new();

        internal PlayerCombatActionGraph CreateActionGraph()
        {
            var actions = new List<PlayerCombatAction>();
            if (_actions != null)
            {
                for (int i = 0; i < _actions.Count; i++)
                {
                    PlayerCombatActionAuthoring entry = _actions[i];
                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            $"{name} has a null combat action at index {i}.");
                    }

                    actions.Add(entry.Build());
                }
            }

            var entries = new List<PlayerCombatEntryRule>();
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    PlayerCombatEntryAuthoring entry = _entries[i];
                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            $"{name} has a null combat entry at index {i}.");
                    }

                    entries.Add(entry.Build());
                }
            }

            var transitions = new List<PlayerCombatTransitionRule>();
            if (_transitions != null)
            {
                for (int i = 0; i < _transitions.Count; i++)
                {
                    PlayerCombatTransitionAuthoring transition = _transitions[i];
                    if (transition == null)
                    {
                        throw new InvalidOperationException(
                            $"{name} has a null combat transition at index {i}.");
                    }

                    transitions.Add(transition.Build());
                }
            }

            return new PlayerCombatActionGraph(actions, entries, transitions);
        }

        internal CombatActionDefinition CreateWeaponTransitionDefinition()
        {
            if (_weaponTransition == null)
            {
                throw new InvalidOperationException($"{name} requires a weapon transition action.");
            }

            return _weaponTransition.Build();
        }

        internal CombatInputBufferSettings CreateInputBufferSettings()
        {
            return _inputBuffer != null
                ? _inputBuffer.Build()
                : CombatInputBufferSettings.CreatePrototypeDefaults();
        }
    }

    [Serializable]
    public sealed class PlayerCombatActionAuthoring
    {
        [SerializeField] private string _id = "combo.0";
        [SerializeField] private PlayerCombatEnvironment _environment =
            PlayerCombatEnvironment.Grounded;
        [SerializeField] private PlayerCombatPresentationKind _presentation =
            PlayerCombatPresentationKind.ComboStep;
        [SerializeField, Min(0)] private int _comboIndex;
        [SerializeField] private string _animationId = "skill.special";
        [SerializeField] private CombatActionAuthoring _action =
            CombatActionAuthoring.CreatePrototypeCombo();

        internal PlayerCombatAction Build()
        {
            if (_action == null)
            {
                throw new InvalidOperationException($"Combat action '{_id}' has no timeline.");
            }

            PlayerCombatPresentation presentation = _presentation switch
            {
                PlayerCombatPresentationKind.ComboStep =>
                    PlayerCombatPresentation.Combo(_comboIndex),
                PlayerCombatPresentationKind.NamedAnimation =>
                    PlayerCombatPresentation.Named(
                        new PlayerCombatAnimationId(_animationId)),
                _ => throw new ArgumentOutOfRangeException(nameof(_presentation), _presentation, null)
            };

            return new PlayerCombatAction(
                new PlayerCombatActionId(_id),
                _environment,
                presentation,
                _action.Build());
        }
    }

    [Serializable]
    public sealed class PlayerCombatEntryAuthoring
    {
        [SerializeField] private string _commandId = "attack.light";
        [SerializeField] private string _targetActionId = "combo.0";

        internal PlayerCombatEntryRule Build()
        {
            return new PlayerCombatEntryRule(
                new PlayerCombatCommandId(_commandId),
                new PlayerCombatActionId(_targetActionId));
        }
    }

    [Serializable]
    public sealed class PlayerCombatTransitionAuthoring
    {
        [SerializeField] private string _sourceActionId = "combo.0";
        [SerializeField] private string _commandId = "attack.light";
        [SerializeField] private string _targetActionId = "combo.1";
        [SerializeField] private PlayerCombatTransitionWindow _window =
            PlayerCombatTransitionWindow.Chain;

        internal PlayerCombatTransitionRule Build()
        {
            return new PlayerCombatTransitionRule(
                new PlayerCombatActionId(_sourceActionId),
                new PlayerCombatCommandId(_commandId),
                new PlayerCombatActionId(_targetActionId),
                _window);
        }
    }

    [Serializable]
    public sealed class CombatActionAuthoring
    {
        [Header("Hitbox Window")]
        [SerializeField] private bool _useHitboxWindow = true;
        [SerializeField, Range(0.0f, 1.0f)] private float _hitboxStart = 0.2f;
        [SerializeField, Range(0.0f, 1.0f)] private float _hitboxEnd = 0.55f;

        [Header("Combo Chain Window")]
        [SerializeField] private bool _useChainWindow = true;
        [SerializeField, Range(0.0f, 1.0f)] private float _chainStart = 0.5f;
        [SerializeField, Range(0.0f, 1.0f)] private float _chainEnd = 0.75f;

        [Header("Action Cancel Window")]
        [SerializeField] private bool _useCancelWindow = true;
        [SerializeField, Range(0.0f, 1.0f)] private float _cancelStart = 0.5f;
        [SerializeField, Range(0.0f, 1.0f)] private float _cancelEnd = 0.75f;

        [Header("Movement Constraint Window")]
        [Tooltip("When disabled, movement settings apply for the whole action.")]
        [SerializeField] private bool _useMovementConstraintWindow;
        [SerializeField, Range(0.0f, 1.0f)] private float _movementConstraintStart;
        [SerializeField, Range(0.0f, 1.0f)] private float _movementConstraintEnd = 0.75f;

        [Header("Invulnerability Window")]
        [SerializeField] private bool _useInvulnerabilityWindow;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityStart;
        [SerializeField, Range(0.0f, 1.0f)] private float _invulnerabilityEnd = 0.25f;

        [Header("Lifecycle")]
        [SerializeField, Range(0.0f, 1.0f)] private float _recoveryStart = 0.6f;
        [SerializeField, Range(0.01f, 1.0f)] private float _completionTime = 0.75f;
        [SerializeField] private bool _hasCommit;
        [SerializeField, Range(0.0f, 1.0f)] private float _commitTime = 0.5f;
        [SerializeField, Min(0.0f)] private float _cooldown;

        [Header("Movement")]
        [SerializeField, Range(0.0f, 1.0f)] private float _translationScale;
        [SerializeField] private bool _allowJump;
        [SerializeField] private bool _allowDodge = true;
        [SerializeField] private bool _faceMoveDirection = true;
        [SerializeField] private RootMotionPolicy _rootMotionPolicy = RootMotionPolicy.Ignore;

        internal CombatActionDefinition Build()
        {
            return new CombatActionDefinition(
                CreateWindow(_useHitboxWindow, _hitboxStart, _hitboxEnd),
                CreateWindow(_useChainWindow, _chainStart, _chainEnd),
                CreateWindow(_useCancelWindow, _cancelStart, _cancelEnd),
                CreateWindow(
                    _useMovementConstraintWindow,
                    _movementConstraintStart,
                    _movementConstraintEnd),
                CreateWindow(
                    _useInvulnerabilityWindow,
                    _invulnerabilityStart,
                    _invulnerabilityEnd),
                _recoveryStart,
                _completionTime,
                _hasCommit,
                _commitTime,
                _cooldown,
                new PlayerMovementIntent(
                    _translationScale,
                    _allowJump,
                    _allowDodge,
                    _faceMoveDirection),
                _rootMotionPolicy);
        }

        internal static CombatActionAuthoring CreatePrototypeCombo()
        {
            return new CombatActionAuthoring();
        }

        internal static CombatActionAuthoring CreatePrototypeSkill()
        {
            return new CombatActionAuthoring
            {
                _useChainWindow = false,
                _useCancelWindow = false,
                _allowDodge = false,
                _cooldown = 1.25f
            };
        }

        internal static CombatActionAuthoring CreatePrototypeWeaponTransition()
        {
            return new CombatActionAuthoring
            {
                _useHitboxWindow = false,
                _useChainWindow = false,
                _useCancelWindow = false,
                _completionTime = 0.5f,
                _recoveryStart = 0.5f,
                _hasCommit = true,
                _commitTime = 0.5f,
                _translationScale = 1.0f,
                _allowDodge = false,
                _faceMoveDirection = false
            };
        }

        private static NormalizedActionWindow CreateWindow(bool enabled, float start, float end)
        {
            return enabled
                ? new NormalizedActionWindow(start, end)
                : NormalizedActionWindow.Disabled;
        }
    }
}
