using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "PlayerCombatActionProfile", menuName = "Combat/Player Action Profile")]
    public sealed class PlayerCombatActionProfile : ScriptableObject
    {
        [SerializeField] private CombatActionAuthoring _defaultCombo =
            CombatActionAuthoring.CreatePrototypeCombo();
        [SerializeField] private List<ComboActionOverrideAuthoring> _comboOverrides = new();
        [SerializeField] private List<SkillActionAuthoring> _skills = new()
        {
            SkillActionAuthoring.CreatePrototypeSpecial()
        };
        [SerializeField] private CombatActionAuthoring _weaponTransition =
            CombatActionAuthoring.CreatePrototypeWeaponTransition();
        [SerializeField] private CombatInputBufferAuthoring _inputBuffer = new();

        internal PlayerCombatActionCatalog CreateCatalog()
        {
            if (_defaultCombo == null)
            {
                throw new InvalidOperationException($"{name} requires a default combo action.");
            }

            if (_weaponTransition == null)
            {
                throw new InvalidOperationException($"{name} requires a weapon transition action.");
            }

            var comboOverrides = new Dictionary<int, CombatActionDefinition>();
            if (_comboOverrides != null)
            {
                for (int i = 0; i < _comboOverrides.Count; i++)
                {
                    ComboActionOverrideAuthoring entry = _comboOverrides[i];
                    if (entry == null || entry.Action == null)
                    {
                        throw new InvalidOperationException(
                            $"{name} has an incomplete combo override at index {i}.");
                    }

                    if (!comboOverrides.TryAdd(entry.ComboIndex, entry.Action.Build()))
                    {
                        throw new InvalidOperationException(
                            $"{name} has duplicate combo override {entry.ComboIndex}.");
                    }
                }
            }

            var skills = new Dictionary<ESkillType, CombatActionDefinition>();
            if (_skills != null)
            {
                for (int i = 0; i < _skills.Count; i++)
                {
                    SkillActionAuthoring entry = _skills[i];
                    if (entry == null || entry.Action == null || entry.SkillType == ESkillType.Default)
                    {
                        throw new InvalidOperationException(
                            $"{name} has an invalid skill action at index {i}.");
                    }

                    if (!skills.TryAdd(entry.SkillType, entry.Action.Build()))
                    {
                        throw new InvalidOperationException(
                            $"{name} has duplicate skill action {entry.SkillType}.");
                    }
                }
            }

            return new PlayerCombatActionCatalog(
                _defaultCombo.Build(),
                comboOverrides,
                skills,
                _weaponTransition.Build());
        }

        internal CombatInputBufferSettings CreateInputBufferSettings()
        {
            return _inputBuffer != null
                ? _inputBuffer.Build()
                : CombatInputBufferSettings.CreatePrototypeDefaults();
        }
    }

    [Serializable]
    public sealed class ComboActionOverrideAuthoring
    {
        [SerializeField, Min(0)] private int _comboIndex;
        [SerializeField] private CombatActionAuthoring _action =
            CombatActionAuthoring.CreatePrototypeCombo();

        internal int ComboIndex => _comboIndex;
        internal CombatActionAuthoring Action => _action;
    }

    [Serializable]
    public sealed class SkillActionAuthoring
    {
        [SerializeField] private ESkillType _skillType = ESkillType.Special;
        [SerializeField] private CombatActionAuthoring _action =
            CombatActionAuthoring.CreatePrototypeSkill();

        internal ESkillType SkillType => _skillType;
        internal CombatActionAuthoring Action => _action;

        internal static SkillActionAuthoring CreatePrototypeSpecial()
        {
            return new SkillActionAuthoring();
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
