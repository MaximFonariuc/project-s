using System;
using ModularAnimation;

namespace _App
{
    internal sealed class PlayerCombatAnimationAdapter : IPlayerCombatAnimation
    {
        private const int SupportedComboCount = 3;
        private const int SupportedAirComboCount = 3;

        private readonly PlayerAnimationCoordinator _animation;
        private readonly PlayerAnimatorLoadout _loadout;

        private bool _disposed;

        public int ComboCount => SupportedComboCount;
        public int AirComboCount => SupportedAirComboCount;

        public PlayerCombatAnimationAdapter(
            PlayerAnimationCoordinator animation,
            PlayerAnimatorLoadout loadout)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public void ApplyLoadout(ECharacterType characterType, EWeaponType weaponType)
        {
            ThrowIfDisposed();
            _loadout.Apply(weaponType);
        }

        public bool HasNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId)
        {
            ThrowIfDisposed();
            return TryResolveNamedAction(animationId, out _);
        }

        public bool TryPlayComboStep(
            int comboIndex,
            float expectedDuration,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (comboIndex < 0 ||
                comboIndex >= SupportedComboCount ||
                expectedDuration <= 0.0f)
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                PlayerAnimatorActions.LightAttack,
                comboIndex,
                expectedDuration);
            return _animation.TryPlayAction(
                PlayerAnimationSource.Combat,
                in request,
                out playback);
        }

        public bool TryPlayNamedAnimation(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAnimationId animationId,
            float expectedDuration,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (!TryResolveNamedAction(animationId, out AnimationActionId action) ||
                expectedDuration <= 0.0f)
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                action,
                0,
                expectedDuration);
            return _animation.TryPlayAction(
                PlayerAnimationSource.Combat,
                in request,
                out playback);
        }

        public bool TryPlayAirComboStep(
            int comboIndex,
            float expectedDuration,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (comboIndex < 0 ||
                comboIndex >= SupportedAirComboCount ||
                expectedDuration <= 0.0f)
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                PlayerAnimatorActions.AirAttack,
                comboIndex,
                expectedDuration);
            return _animation.TryPlayAction(
                PlayerAnimationSource.Combat,
                in request,
                out playback);
        }

        public bool TryPlayWeaponTransition(
            EWeaponType weaponType,
            bool draw,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (weaponType != EWeaponType.TwoHanded)
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                PlayerAnimatorActions.WeaponTransition,
                draw ? 1 : 0,
                PlayerAnimatorActions.WeaponTransitionDuration);
            return _animation.TryPlayAction(
                PlayerAnimationSource.Combat,
                in request,
                out playback);
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

        private static bool TryResolveNamedAction(
            PlayerCombatAnimationId animationId,
            out AnimationActionId action)
        {
            if (animationId == PlayerCombatAnimationIds.HeavyAttack)
            {
                action = PlayerAnimatorActions.HeavyAttack;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.Special)
            {
                action = PlayerAnimatorActions.SpecialAttack;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.Launcher)
            {
                action = PlayerAnimatorActions.Launcher;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.AirChase)
            {
                action = PlayerAnimatorActions.AirChase;
                return true;
            }

            action = default;
            return false;
        }
    }
}
