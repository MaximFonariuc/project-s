using System;
using System.Collections.Generic;
using ModularAnimation;
using ModularAnimation.Unity;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private readonly MecanimCharacterAnimationDriver _mecanim;
        private readonly IAnimationClipActionDriver _actionDriver;
        private readonly AnimationsData _animationsData;

        private IReadOnlyList<CombatAnimationPresentation> _comboActions =
            Array.Empty<CombatAnimationPresentation>();
        private ECharacterType _characterType;
        private float _activeBlendOutSeconds;
        private bool _disposed;

        public int ComboCount => _comboActions.Count;

        public PlayerCombatAnimationAdapter(
            MecanimCharacterAnimationDriver mecanim,
            IAnimationClipActionDriver actionDriver,
            AnimationsData animationsData)
        {
            _mecanim = mecanim ?? throw new ArgumentNullException(nameof(mecanim));
            _actionDriver = actionDriver ?? throw new ArgumentNullException(nameof(actionDriver));
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
                    out CombatAnimationPresentation presentation))
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
                    out CombatAnimationPresentation presentation))
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

            _actionDriver.StopAction(_activeBlendOutSeconds);
            _activeBlendOutSeconds = 0.0f;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _actionDriver.StopAction(0.0f);
            _activeBlendOutSeconds = 0.0f;
            _disposed = true;
        }

        private bool TryPlay(
            CombatAnimationPresentation presentation,
            out AnimationActionPlayback playback)
        {
            if (presentation == null || !presentation.IsValid)
            {
                playback = default;
                return false;
            }

            AnimationClipActionRequest request = presentation.CreateRequest();
            playback = _actionDriver.PlayAction(in request);
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                playback = default;
                return false;
            }

            _activeBlendOutSeconds = presentation.BlendOutSeconds;
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
