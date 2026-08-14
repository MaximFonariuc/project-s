using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ModularAnimation.Unity
{
    public enum MecanimStateControlMode
    {
        CodeDriven,
        AnimatorTransitions
    }

    [CreateAssetMenu(
        fileName = "MecanimCharacterAnimationProfile",
        menuName = "Modular Animation/Mecanim Character Profile")]
    public sealed class MecanimCharacterAnimationProfile : ScriptableObject
    {
        [SerializeField] private string _movementType = "MovementType";
        [SerializeField] private string _movementDirection = "MovementDirection";
        [FormerlySerializedAs("_fallingDuration")]
        [SerializeField] private string _verticalSpeed = "VerticalSpeed";
        [SerializeField] private string _speedMultiplier = "SpeedMultiplier";

        [Header("Semantic Animation States")]
        [Tooltip("Optional controller used by editor validation. Runtime uses the controller assigned to Animator.")]
        [SerializeField] private RuntimeAnimatorController _validationController;
        [SerializeField] private MecanimStateControlMode _stateControlMode =
            MecanimStateControlMode.CodeDriven;
        [SerializeField] private List<MecanimAnimationStateAuthoring> _states = new();

        public RuntimeAnimatorController ValidationController => _validationController;
        public MecanimStateControlMode StateControlMode => _stateControlMode;
        public IReadOnlyList<MecanimAnimationStateAuthoring> States =>
            _states ?? (IReadOnlyList<MecanimAnimationStateAuthoring>)Array.Empty<MecanimAnimationStateAuthoring>();

        public MecanimCharacterAnimationBindings CreateBindings()
        {
            return new MecanimCharacterAnimationBindings(
                _movementType,
                _movementDirection,
                _verticalSpeed,
                _speedMultiplier);
        }

        public IAnimationStateCatalog CreateStateCatalog(Animator animator)
        {
            return new MecanimAnimationStateCatalog(animator, CreateStateDefinitions());
        }

        private IReadOnlyList<MecanimAnimationStateDefinition> CreateStateDefinitions()
        {
            if (_states == null || _states.Count == 0)
            {
                return Array.Empty<MecanimAnimationStateDefinition>();
            }

            var definitions = new MecanimAnimationStateDefinition[_states.Count];
            for (int i = 0; i < _states.Count; i++)
            {
                MecanimAnimationStateAuthoring state = _states[i];
                if (state == null)
                {
                    throw new InvalidOperationException(
                        $"Animation profile '{name}' has a null state mapping at index {i}.");
                }

                definitions[i] = state.CreateDefinition();
            }

            return definitions;
        }
    }

    [Serializable]
    public sealed class MecanimAnimationStateAuthoring
    {
        [SerializeField] private string _slot;
        [Tooltip("State path relative to its Animator layer, for example Locomotion.Roll.RollFront.")]
        [SerializeField] private string _state;
        [SerializeField] private string _layer = "Base Layer";
        [FormerlySerializedAs("_transitionDuration")]
        [SerializeField, Min(0.0f)] private float _transitionSeconds = 0.1f;
        [SerializeField, Min(0.0f)] private float _expectedDuration;
        [Tooltip("Optional semantic slot used when this state is unavailable.")]
        [SerializeField] private string _fallbackSlot;
        [SerializeField] private bool _required;

        public string Slot => _slot;
        public string State => _state;
        public string Layer => _layer;
        public float TransitionSeconds => _transitionSeconds;
        public float ExpectedDuration => _expectedDuration;
        public string FallbackSlot => _fallbackSlot;
        public bool Required => _required;

        internal MecanimAnimationStateDefinition CreateDefinition()
        {
            return new MecanimAnimationStateDefinition(
                new AnimationSlotId(_slot),
                _state,
                _layer,
                _transitionSeconds,
                _expectedDuration,
                new AnimationSlotId(_fallbackSlot),
                _required);
        }
    }
}
