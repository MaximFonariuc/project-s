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

        public bool CanPlayAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAction action)
        {
            ThrowIfDisposed();
            if (action == null)
            {
                return false;
            }

            PlayerCombatPresentation presentation = action.Presentation;
            return TryResolveAction(in presentation, out _, out _);
        }

        public bool TryPlayAction(
            ECharacterType characterType,
            EWeaponType weaponType,
            PlayerCombatAction action,
            out AnimationActionPlayback playback)
        {
            ThrowIfDisposed();
            if (action == null || action.Definition.DurationSeconds <= 0.0f)
            {
                playback = default;
                return false;
            }

            PlayerCombatPresentation presentation = action.Presentation;
            if (!TryResolveAction(
                    in presentation,
                    out AnimationActionId animationAction,
                    out int variant))
            {
                playback = default;
                return false;
            }

            var request = new AnimationActionRequest(
                animationAction,
                variant,
                action.Definition.DurationSeconds);
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
            if (weaponType is not EWeaponType.TwoHanded and not EWeaponType.DualHand)
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

        private static bool TryResolveAction(
            in PlayerCombatPresentation presentation,
            out AnimationActionId action,
            out int variant)
        {
            switch (presentation.Kind)
            {
                case PlayerCombatPresentationKind.ComboStep
                    when presentation.ComboIndex >= 0 &&
                         presentation.ComboIndex < SupportedComboCount:
                    action = PlayerAnimatorActions.LightAttack;
                    variant = presentation.ComboIndex;
                    return true;

                case PlayerCombatPresentationKind.AirComboStep
                    when presentation.ComboIndex >= 0 &&
                         presentation.ComboIndex < SupportedAirComboCount:
                    action = PlayerAnimatorActions.AirAttack;
                    variant = presentation.ComboIndex;
                    return true;

                case PlayerCombatPresentationKind.NamedAnimation:
                    variant = 0;
                    return TryResolveNamedAction(presentation.AnimationId, out action);

                default:
                    action = default;
                    variant = 0;
                    return false;
            }
        }
    }
}
