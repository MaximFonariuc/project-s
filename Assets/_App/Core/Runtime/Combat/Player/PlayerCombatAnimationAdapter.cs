using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private const string UnsheathePostfix = "_Unsheathe";
        private const string SheathePostfix = "_Sheathe";

        private readonly AnimationController _animation;
        private readonly AnimationsData _animationsData;
        private readonly RuntimeAnimatorController _sourceController;
        private readonly AnimatorOverrideController _overrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _baselineOverrides;
        private readonly HashSet<string> _overrideNames;

        private IReadOnlyList<AnimationClip> _comboClips = Array.Empty<AnimationClip>();
        private bool _disposed;

        public int ComboCount => _comboClips.Count;

        public PlayerCombatAnimationAdapter(
            AnimationController animation,
            AnimationsData animationsData)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _animationsData = animationsData != null
                ? animationsData
                : throw new ArgumentNullException(nameof(animationsData));

            Animator animator = _animation.Animator;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException("Player combat requires an Animator runtime controller.");
            }

            _sourceController = animator.runtimeAnimatorController;
            _overrideController = new AnimatorOverrideController(_sourceController);
            _baselineOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(
                _overrideController.overridesCount);
            _overrideController.GetOverrides(_baselineOverrides);

            _overrideNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _baselineOverrides.Count; i++)
            {
                AnimationClip original = _baselineOverrides[i].Key;
                if (original == null)
                {
                    continue;
                }

                _overrideNames.Add(original.name);
                _baselineOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, original);
            }

            animator.runtimeAnimatorController = _overrideController;
        }

        public void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType)
        {
            ThrowIfDisposed();
            _overrideController.ApplyOverrides(_baselineOverrides);

            IReadOnlyList<AnimationClip> defaults =
                _animationsData.GetDefaultsAnimationClips(characterType, weaponType);
            ApplyOverrides(defaults);

            DefaultSkillCombo combo =
                _animationsData.GetSkill<DefaultSkillCombo>(characterType, weaponType);
            _comboClips = combo?.ComboClips;
            if (_comboClips == null)
            {
                _comboClips = Array.Empty<AnimationClip>();
            }
            ApplyOverrides(_comboClips);
        }

        public bool HasSkill(
            ECharacterType characterType,
            EWeaponType weaponType,
            ESkillType skillType)
        {
            Skill skill = _animationsData.GetSkill<Skill>(characterType, weaponType, skillType);
            return skill?.AnimationClip != null;
        }

        public bool TryPlayComboStep(int comboIndex, out string stateName)
        {
            ThrowIfDisposed();
            stateName = null;
            if (comboIndex < 0 || comboIndex >= _comboClips.Count)
            {
                return false;
            }

            AnimationClip clip = _comboClips[comboIndex];
            if (clip == null)
            {
                return false;
            }

            stateName = clip.name;
            _animation.CrossFade(stateName, 0.1f, AnimationController.BASE_LAYER_INDEX);
            return true;
        }

        public bool TryPlaySkill(
            ECharacterType characterType,
            EWeaponType weaponType,
            ESkillType skillType,
            out string stateName)
        {
            ThrowIfDisposed();
            stateName = null;

            Skill skill = _animationsData.GetSkill<Skill>(characterType, weaponType, skillType);
            AnimationClip clip = skill?.AnimationClip;
            if (clip == null || !TryApplyOverride(clip))
            {
                return false;
            }

            stateName = "Skill";
            _animation.CrossFade(AnimationHashes.Skill, 0.1f, AnimationController.BASE_LAYER_INDEX);
            return true;
        }

        public bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out string stateName)
        {
            ThrowIfDisposed();
            if (weaponType is EWeaponType.None or EWeaponType.Unarmed)
            {
                stateName = null;
                return false;
            }

            stateName = weaponType + (draw ? UnsheathePostfix : SheathePostfix);
            _animation.CrossFade(stateName, 0.1f, AnimationController.SWITCH_LAYER_INDEX);
            return true;
        }

        public bool IsInState(string stateName, int layerIndex)
        {
            return !string.IsNullOrEmpty(stateName) && _animation.IsInState(stateName, layerIndex);
        }

        public float GetNormalizedTime(int layerIndex)
        {
            return _animation.GetNormalizedTime(layerIndex);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_animation.Animator != null && _animation.Animator.runtimeAnimatorController == _overrideController)
            {
                _animation.Animator.runtimeAnimatorController = _sourceController;
            }

            Object.Destroy(_overrideController);
        }

        private void ApplyOverrides(IReadOnlyList<AnimationClip> clips)
        {
            if (clips == null)
            {
                return;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null)
                {
                    TryApplyOverride(clip);
                }
            }
        }

        private bool TryApplyOverride(AnimationClip clip)
        {
            if (!_overrideNames.Contains(clip.name))
            {
                Debug.LogWarning(
                    $"Animator controller has no override slot named '{clip.name}'.",
                    _animation.Animator);
                return false;
            }

            _overrideController[clip.name] = clip;
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerCombatAnimationAdapter));
            }
        }
    }
}
