using System;
using ModularAnimation;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private readonly PlayerAnimationCoordinator _animation;

        private EWeaponType _weaponType;
        private bool _disposed;

        public int ComboCount => _weaponType == EWeaponType.Unarmed ? 1 : 0;

        public PlayerCombatAnimationAdapter(PlayerAnimationCoordinator animation)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType)
        {
            ThrowIfDisposed();
            _weaponType = weaponType;
        }

        public bool HasNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId)
        {
            return false;
        }

        public bool TryPlayComboStep(
            int comboIndex,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (_weaponType != EWeaponType.Unarmed || comboIndex != 0)
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                PlayerAnimatorActions.LightAttack,
                0,
                PlayerAnimatorActions.LightAttackDuration);
            return _animation.TryPlayAction(
                PlayerAnimationSource.Combat,
                in request,
                out playback);
        }

        public bool TryPlayNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            out AnimationActionPlayback playback)
        {
            playback = default;
            return false;
        }

        public bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out AnimationActionPlayback playback)
        {
            playback = default;
            return false;
        }

        public void Release()
        {
            if (!_disposed)
            {
                _animation.ReleaseAll(PlayerAnimationSource.Combat);
            }
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                _animation.Cancel(PlayerAnimationSource.Combat);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _animation.Cancel(PlayerAnimationSource.Combat);
            _animation.ReleaseAll(PlayerAnimationSource.Combat);
            _disposed = true;
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
