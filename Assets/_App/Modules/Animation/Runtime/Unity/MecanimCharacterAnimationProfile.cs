using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ModularAnimation.Unity
{
    [CreateAssetMenu(
        fileName = "MecanimCharacterAnimationProfile",
        menuName = "Modular Animation/Character Animation Profile")]
    public sealed class MecanimCharacterAnimationProfile : ScriptableObject
    {
        [Header("Locomotion")]
        [SerializeField] private MecanimLocomotionClipSet _locomotion = new();

        [Header("Semantic States")]
        [SerializeField] private List<MecanimAnimationStateAuthoring> _states = new();

        [Header("Generic In-Place Motion")]
        [SerializeField] private string _planarMotionRootPath;

        public MecanimLocomotionClipSet Locomotion => _locomotion;
        public IReadOnlyList<MecanimAnimationStateAuthoring> States =>
            _states ?? (IReadOnlyList<MecanimAnimationStateAuthoring>)Array.Empty<MecanimAnimationStateAuthoring>();

        public MecanimAnimationGraphConfiguration CreateConfiguration(Animator animator)
        {
            return new MecanimAnimationGraphConfiguration(
                animator,
                _locomotion ?? throw new InvalidOperationException(
                    $"Animation profile '{name}' requires a locomotion clip set."),
                CreateStateDefinitions(),
                _planarMotionRootPath);
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
    public sealed class MecanimDirectionalClipSet
    {
        [SerializeField] private AnimationClip _forward;
        [SerializeField] private AnimationClip _backward;
        [SerializeField] private AnimationClip _left;
        [SerializeField] private AnimationClip _right;

        public AnimationClip Forward => _forward;
        public AnimationClip Backward => _backward;
        public AnimationClip Left => _left;
        public AnimationClip Right => _right;

        internal AnimationClip Get(int direction)
        {
            return direction switch
            {
                0 => _forward,
                1 => _backward,
                2 => _left,
                3 => _right,
                _ => null
            };
        }
    }

    [Serializable]
    public sealed class MecanimLocomotionClipSet
    {
        [SerializeField, Min(0.0f)] private float _blendSeconds = 0.15f;
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private MecanimDirectionalClipSet _walk = new();
        [SerializeField] private MecanimDirectionalClipSet _run = new();
        [SerializeField] private MecanimDirectionalClipSet _sprint = new();

        public float BlendSeconds => Mathf.Max(0.0f, _blendSeconds);
        public AnimationClip Idle => _idle;
        public MecanimDirectionalClipSet Walk => _walk;
        public MecanimDirectionalClipSet Run => _run;
        public MecanimDirectionalClipSet Sprint => _sprint;

        internal AnimationClip Get(CharacterGait gait, int direction)
        {
            MecanimDirectionalClipSet selected = gait switch
            {
                CharacterGait.Walk => _walk,
                CharacterGait.Sprint => _sprint,
                _ => _run
            };

            AnimationClip clip = selected?.Get(direction);
            if (clip != null)
            {
                return clip;
            }

            clip = _run?.Get(direction);
            if (clip != null)
            {
                return clip;
            }

            clip = selected?.Forward ?? _run?.Forward;
            return clip != null ? clip : _idle;
        }
    }

    [Serializable]
    public sealed class MecanimAnimationStateAuthoring
    {
        [SerializeField] private string _slot;
        [Tooltip("Use the continuously blended locomotion graph instead of a finite clip.")]
        [SerializeField] private bool _usesLocomotion;
        [SerializeField] private AnimationClip _clip;
        [FormerlySerializedAs("_transitionDuration")]
        [SerializeField, Min(0.0f)] private float _transitionSeconds = 0.1f;
        [SerializeField, Min(0.0f)] private float _expectedDuration;
        [Tooltip("Optional semantic slot used when this clip is unavailable.")]
        [SerializeField] private string _fallbackSlot;
        [SerializeField] private bool _required;
        [SerializeField] private bool _applyFootIk;

        public string Slot => _slot;
        public bool UsesLocomotion => _usesLocomotion;
        public AnimationClip Clip => _clip;
        public float TransitionSeconds => _transitionSeconds;
        public float ExpectedDuration => _expectedDuration;
        public string FallbackSlot => _fallbackSlot;
        public bool Required => _required;
        public bool ApplyFootIk => _applyFootIk;

        internal MecanimAnimationStateDefinition CreateDefinition()
        {
            return new MecanimAnimationStateDefinition(
                new AnimationSlotId(_slot),
                _usesLocomotion,
                _clip,
                _transitionSeconds,
                _expectedDuration,
                new AnimationSlotId(_fallbackSlot),
                _required,
                _applyFootIk);
        }
    }
}
