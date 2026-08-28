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

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                CreateActionGraph();
                CreateWeaponTransitionDefinition();
                CreateInputBufferSettings();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Combat action profile '{name}' is invalid: {exception.Message}",
                    this);
            }
        }
#endif
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
                PlayerCombatPresentationKind.AirComboStep =>
                    PlayerCombatPresentation.AirCombo(_comboIndex),
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
        [SerializeField] private PlayerCombatTransitionCondition _condition =
            PlayerCombatTransitionCondition.Any;

        [Header("Optional Transition Timing Gate")]
        [Tooltip(
            "Restricts this route to a sub-range of the selected Chain/Cancel window.")]
        [SerializeField] private bool _useAcceptanceWindow;
        [SerializeField, Range(0.0f, 1.0f)] private float _acceptanceStart = 0.5f;
        [SerializeField, Range(0.0f, 1.0f)] private float _acceptanceEnd = 0.75f;

        [Header("Optional Input Timing Gate")]
        [Tooltip(
            "Restricts this route by the normalized source-action time at which input was received. " +
            "Use non-overlapping ranges for rapid and delayed variants of the same command.")]
        [SerializeField] private bool _useInputWindow;
        [SerializeField, Range(0.0f, 1.0f)] private float _inputStart;
        [SerializeField, Range(0.0f, 1.0f)] private float _inputEnd = 0.75f;

        internal PlayerCombatTransitionRule Build()
        {
            return new PlayerCombatTransitionRule(
                new PlayerCombatActionId(_sourceActionId),
                new PlayerCombatCommandId(_commandId),
                new PlayerCombatActionId(_targetActionId),
                _window,
                _condition,
                _useAcceptanceWindow
                    ? new NormalizedActionWindow(_acceptanceStart, _acceptanceEnd)
                    : NormalizedActionWindow.Disabled,
                _useInputWindow
                    ? new NormalizedActionWindow(_inputStart, _inputEnd)
                    : NormalizedActionWindow.Disabled);
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

        [Header("Impact")]
        [SerializeField, Min(0)] private int _damage = 10;
        [SerializeField, Min(0.0f)] private float _staggerDamage = 10.0f;
        [SerializeField, Min(0.0f)] private float _knockback;
        [Tooltip(
            "Signed vertical impact speed. Positive launches the target; negative slams it down.")]
        [SerializeField] private float _verticalKnockback;
        [SerializeField] private string _reactionId = "hit.light";
        [SerializeField, Min(0.0f)] private float _hitStopDuration = 0.06f;
        [SerializeField, Range(0.0f, 1.0f)] private float _hitStopTimeScale;

        [Header("Lifecycle")]
        [SerializeField, Min(0.01f)] private float _durationSeconds = 1.3f;
        [SerializeField, Range(0.0f, 1.0f)] private float _recoveryStart = 0.6f;
        [SerializeField, Range(0.01f, 1.0f)] private float _completionTime = 0.75f;
        [SerializeField] private bool _hasCommit;
        [SerializeField, Range(0.0f, 1.0f)] private float _commitTime = 0.5f;
        [SerializeField, Min(0.0f)] private float _cooldown;

        [Header("Movement")]
        [SerializeField, Range(0.0f, 1.0f)] private float _translationScale;
        [SerializeField] private bool _allowJump;
        [Tooltip("Allows locomotion dodge only while this action's Cancel Window is open.")]
        [SerializeField] private bool _allowDodge = true;
        [SerializeField] private bool _faceMoveDirection = true;

        [Header("Combat Motion")]
        [Tooltip("Applies collision-resolved planar motion during the configured action window.")]
        [SerializeField] private bool _useCombatMotion;
        [SerializeField, Range(0.0f, 1.0f)] private float _combatMotionStart;
        [SerializeField, Range(0.0f, 1.0f)] private float _combatMotionEnd = 0.3f;
        [SerializeField] private bool _overridePlanarVelocity;
        [SerializeField, Min(0.0f)] private float _combatMotionDistance = 0.75f;

        [Header("Combat Vertical Motion")]
        [Tooltip("OverrideVelocity sets an exact vertical speed. MinimumVelocity preserves ascent and limits falling speed.")]
        [SerializeField] private CharacterActionVerticalMode _verticalMotionMode =
            CharacterActionVerticalMode.PreserveVelocity;
        [SerializeField] private float _combatVerticalVelocity;
        [Tooltip("One-time signed velocity applied when the combat motion window opens.")]
        [SerializeField] private float _combatVerticalImpulse;
        [SerializeField] private bool _overrideVerticalVelocityOnImpulse = true;

        [Header("Combat Target Motion")]
        [SerializeField] private bool _faceCombatTarget = true;
        [SerializeField] private CombatMotionTargetSource _combatMotionTargetSource =
            CombatMotionTargetSource.LockOnTarget;
        [SerializeField, Min(0.0f)] private float _targetStoppingDistance = 0.8f;
        [Tooltip("Selects one nearby target when the action starts if hard lock-on is inactive.")]
        [SerializeField] private bool _allowTargetAssist;
        [SerializeField, Min(0.01f)] private float _targetAssistDistance = 5.0f;
        [SerializeField, Range(0.01f, 180.0f)] private float _targetAssistAngle = 55.0f;

        [Header("Combat Target Height")]
        [Tooltip("Matches target vertical motion during this bounded combat motion window.")]
        [SerializeField] private bool _trackTargetHeight;
        [Tooltip("Desired actor height relative to the target. Negative values keep the actor below it.")]
        [SerializeField] private float _targetHeightOffset;
        [SerializeField, Min(0.0f)] private float _targetHeightTolerance = 0.1f;
        [SerializeField, Min(0.01f)] private float _targetHeightCorrectionSpeed = 6.0f;
        [SerializeField, Min(0.01f)] private float _maxTargetVerticalSpeed = 8.0f;

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
                new CombatHitDefinition(
                    _damage,
                    _staggerDamage,
                    _knockback,
                    _verticalKnockback,
                    string.IsNullOrWhiteSpace(_reactionId)
                        ? CombatReactionId.None
                        : new CombatReactionId(_reactionId),
                    _hitStopDuration > 0.0f
                        ? new CombatHitStop(_hitStopDuration, _hitStopTimeScale)
                        : CombatHitStop.Disabled),
                Mathf.Max(0.01f, _durationSeconds),
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
                _useCombatMotion
                    ? new CombatMotionDefinition(
                        new NormalizedActionWindow(_combatMotionStart, _combatMotionEnd),
                        _overridePlanarVelocity,
                        _combatMotionDistance,
                        _verticalMotionMode,
                        _combatVerticalVelocity,
                        _combatVerticalImpulse,
                        _overrideVerticalVelocityOnImpulse,
                        _faceCombatTarget,
                        _combatMotionTargetSource,
                        _targetStoppingDistance,
                        _allowTargetAssist,
                        _targetAssistDistance,
                        _targetAssistAngle,
                        _trackTargetHeight,
                        _targetHeightOffset,
                        _targetHeightTolerance,
                        _targetHeightCorrectionSpeed,
                        _maxTargetVerticalSpeed)
                    : CombatMotionDefinition.Disabled);
        }

        internal static CombatActionAuthoring CreatePrototypeCombo()
        {
            return new CombatActionAuthoring
            {
                _useCombatMotion = true,
                _overridePlanarVelocity = true
            };
        }

        internal static CombatActionAuthoring CreatePrototypeSkill()
        {
            return new CombatActionAuthoring
            {
                _useChainWindow = false,
                _useCancelWindow = false,
                _allowDodge = false,
                _cooldown = 1.25f,
                _useCombatMotion = true,
                _overridePlanarVelocity = true,
                _combatMotionDistance = 1.0f
            };
        }

        internal static CombatActionAuthoring CreatePrototypeHeavy()
        {
            return new CombatActionAuthoring
            {
                _useChainWindow = false,
                _hitboxStart = 0.24f,
                _hitboxEnd = 0.45f,
                _cancelStart = 0.72f,
                _cancelEnd = 0.94f,
                _damage = 24,
                _staggerDamage = 20.0f,
                _knockback = 6.5f,
                _reactionId = "hit.heavy",
                _hitStopDuration = 0.08f,
                _durationSeconds = 1.32f,
                _recoveryStart = 0.80f,
                _completionTime = 0.98f,
                _translationScale = 0.08f,
                _useCombatMotion = true,
                _combatMotionEnd = 0.48f,
                _overridePlanarVelocity = true,
                _combatMotionDistance = 0.9f
            };
        }

        internal static CombatActionAuthoring CreatePrototypeWeaponTransition()
        {
            return new CombatActionAuthoring
            {
                _useHitboxWindow = false,
                _useChainWindow = false,
                _useCancelWindow = false,
                _damage = 0,
                _staggerDamage = 0.0f,
                _reactionId = string.Empty,
                _durationSeconds = 1.0f,
                _completionTime = 1.0f,
                _recoveryStart = 0.9f,
                _hasCommit = true,
                _commitTime = 0.5f,
                _translationScale = 1.0f,
                _allowDodge = false,
                _faceMoveDirection = false,
                _useCombatMotion = false
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
