using System;
using System.Collections.Generic;
using ModularAnimation;
using ModularAnimation.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private readonly PlayerAnimationCoordinator _animation;
        private readonly MecanimCharacterAnimationDriver _mecanim;
        private readonly AnimationsData _animationsData;
        private readonly RuntimeAnimatorController _sourceController;
        private readonly AnimatorOverrideController _overrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _baselineOverrides;
        private readonly HashSet<string> _overrideNames;

        private IReadOnlyList<AnimationClip> _comboClips = Array.Empty<AnimationClip>();
        private bool _disposed;

        public int ComboCount => _comboClips.Count;

        public PlayerCombatAnimationAdapter(
            PlayerAnimationCoordinator animation,
            MecanimCharacterAnimationDriver mecanim,
            AnimationsData animationsData)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _mecanim = mecanim ?? throw new ArgumentNullException(nameof(mecanim));
            _animationsData = animationsData != null
                ? animationsData
                : throw new ArgumentNullException(nameof(animationsData));

            Animator animator = _mecanim.Animator;
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
            _mecanim.RefreshBindings();
        }

        public void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType)
        {
            ThrowIfDisposed();
            _overrideController.ApplyOverrides(_baselineOverrides);

            IReadOnlyList<AnimationClip> defaults =
                _animationsData.GetDefaultsAnimationClips(characterType, weaponType);
            ApplyOverrides(defaults);

            _comboClips = _animationsData.GetComboClips(characterType, weaponType);
            ApplyOverrides(_comboClips);
            _mecanim.RefreshBindings();
            _animation.CrossFade(
                PlayerAnimationSource.Equipment,
                PlayerAnimationSlots.WeaponRest);
        }

        public bool HasNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId)
        {
            return _animationsData.GetActionClip(
                characterType,
                weaponType,
                animationId) != null;
        }

        public bool TryPlayComboStep(int comboIndex, out AnimationPlayback playback)
        {
            ThrowIfDisposed();
            playback = default;
            if (comboIndex < 0 || comboIndex >= _comboClips.Count)
            {
                return false;
            }

            AnimationClip clip = _comboClips[comboIndex];
            if (clip == null)
            {
                return false;
            }

            playback = _animation.CrossFade(
                PlayerAnimationSource.Combat,
                PlayerAnimationSlots.Combo(comboIndex),
                clip.length);
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                return false;
            }

            return true;
        }

        public bool TryPlayNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out AnimationPlayback playback)
        {
            ThrowIfDisposed();
            playback = default;

            AnimationClip clip = _animationsData.GetActionClip(
                characterType,
                weaponType,
                animationId);
            if (clip == null || !TryApplyOverride(clip))
            {
                return false;
            }

            playback = _animation.CrossFade(
                PlayerAnimationSource.Combat,
                PlayerAnimationSlots.CombatAction,
                clip.length);
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                return false;
            }

            return true;
        }

        public bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out AnimationPlayback playback)
        {
            ThrowIfDisposed();
            if (weaponType is EWeaponType.None or EWeaponType.Unarmed)
            {
                playback = default;
                return false;
            }

            playback = _animation.CrossFade(
                PlayerAnimationSource.Combat,
                PlayerAnimationSlots.WeaponTransition(weaponType, draw));
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                playback = default;
                return false;
            }

            return true;
        }

        public void Release()
        {
            _animation.ReleaseAll(PlayerAnimationSource.Combat);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release();
            _animation.ReleaseAll(PlayerAnimationSource.Equipment);
            if (_mecanim.Animator != null && _mecanim.Animator.runtimeAnimatorController == _overrideController)
            {
                _mecanim.Animator.runtimeAnimatorController = _sourceController;
                _mecanim.RefreshBindings();
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
                    _mecanim.Animator);
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
