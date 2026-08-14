using System;
using System.Collections.Generic;
using ModularAnimation;
using ModularAnimation.Unity;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private readonly MecanimCharacterAnimationDriver _mecanim;
        private readonly PlayerAnimationCoordinator _animation;
        private readonly AnimationsData _animationsData;

        private IReadOnlyList<CharacterAnimationActionPresentation> _comboActions =
            Array.Empty<CharacterAnimationActionPresentation>();
        private ECharacterType _characterType;
        private AnimationActionLease _activeAction;
        private bool _disposed;

        public int ComboCount => _comboActions.Count;

        public PlayerCombatAnimationAdapter(
            MecanimCharacterAnimationDriver mecanim,
            PlayerAnimationCoordinator animation,
            AnimationsData animationsData)
        {
            _mecanim = mecanim ?? throw new ArgumentNullException(nameof(mecanim));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _animationsData = animationsData != null
                ? animationsData
                : throw new ArgumentNullException(nameof(animationsData));
        }

        public void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType)
        {
            ThrowIfDisposed();
            _characterType = characterType;
            _mecanim.ApplyControllerOverrides(
                _animationsData.GetControllerOverrides(characterType, weaponType));
            _comboActions = _animationsData.GetComboActions(characterType, weaponType);
        }

        public bool HasNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId)
        {
            return _animationsData.TryGetAction(
                characterType,
                weaponType,
                animationId,
                out _);
        }

        public bool TryPlayComboStep(
            int comboIndex,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (comboIndex < 0 || comboIndex >= _comboActions.Count)
            {
                playback = default;
                return false;
            }

            return TryPlay(_comboActions[comboIndex], out playback);
        }

        public bool TryPlayNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (!_animationsData.TryGetAction(
                    characterType,
                    weaponType,
                    animationId,
                    out CharacterAnimationActionPresentation presentation))
            {
                playback = default;
                return false;
            }

            return TryPlay(presentation, out playback);
        }

        public bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (weaponType is EWeaponType.None or EWeaponType.Unarmed ||
                !_animationsData.TryGetWeaponTransition(
                    _characterType,
                    weaponType,
                    draw,
                    out CharacterAnimationActionPresentation presentation))
            {
                playback = default;
                return false;
            }

            return TryPlay(presentation, out playback);
        }

        public void Release()
        {
            if (_disposed)
            {
                return;
            }

            _activeAction.Release();
            _activeAction = default;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _activeAction.Release();
            _activeAction = default;
            _disposed = true;
        }

        private bool TryPlay(
            CharacterAnimationActionPresentation presentation,
            out AnimationActionPlayback playback)
        {
            if (presentation == null || !presentation.IsValid)
            {
                playback = default;
                return false;
            }

            AnimationClipActionRequest request = presentation.CreateRequest();
            if (!_animation.TryPlayAction(
                    PlayerAnimationSource.Combat,
                    in request,
                    presentation.BlendOutSeconds,
                    out AnimationActionLease action))
            {
                playback = default;
                return false;
            }

            _activeAction = action;
            playback = action.Playback;
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
