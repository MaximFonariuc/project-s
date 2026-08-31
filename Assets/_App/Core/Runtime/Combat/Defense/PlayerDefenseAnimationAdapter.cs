using System;
using ModularAnimation;

namespace _App
{
    /// <summary>
    /// Presents confirmed defense state without giving Mecanim ownership of
    /// guard, block or parry rules.
    /// </summary>
    internal sealed class PlayerDefenseAnimationAdapter : IDisposable
    {
        private enum ImpactPresentation
        {
            None,
            Block,
            Parry
        }

        private readonly IPlayerDefenseState _defense;
        private readonly PlayerAnimationCoordinator _animation;

        private ImpactPresentation _impact;
        private float _impactTimeRemaining;
        private bool _enabled;

        public PlayerDefenseAnimationAdapter(
            IPlayerDefenseState defense,
            PlayerAnimationCoordinator animation)
        {
            _defense = defense ?? throw new ArgumentNullException(nameof(defense));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _defense.GuardStarted += OnGuardStarted;
            _defense.GuardEnded += OnGuardEnded;
            _defense.Defended += OnDefended;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _defense.GuardStarted -= OnGuardStarted;
            _defense.GuardEnded -= OnGuardEnded;
            _defense.Defended -= OnDefended;
            Reset();
            _enabled = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_enabled || _impact == ImpactPresentation.None)
            {
                return;
            }

            _impactTimeRemaining -= Math.Max(0.0f, deltaTime);
            if (_impactTimeRemaining > 0.0f)
            {
                return;
            }

            _impact = ImpactPresentation.None;
            _impactTimeRemaining = 0.0f;
            if (_defense.IsGuarding)
            {
                PlayGuard(resumeHoldPose: true);
            }
            else
            {
                _animation.Cancel(PlayerAnimationSource.Defense);
            }
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnGuardStarted()
        {
            if (_impact == ImpactPresentation.None)
            {
                PlayGuard(resumeHoldPose: false);
            }
        }

        private void OnGuardEnded()
        {
            if (_impact == ImpactPresentation.None)
            {
                _animation.Cancel(PlayerAnimationSource.Defense);
            }
        }

        private void OnDefended(PlayerDefenseResult result)
        {
            if (_impact != ImpactPresentation.None)
            {
                return;
            }

            AnimationActionId action;
            float duration;
            switch (result.Outcome)
            {
                case CombatHitOutcome.Blocked:
                    _impact = ImpactPresentation.Block;
                    action = PlayerAnimatorActions.Block;
                    duration = PlayerAnimatorActions.BlockDuration;
                    break;
                case CombatHitOutcome.Parried:
                    _impact = ImpactPresentation.Parry;
                    action = PlayerAnimatorActions.Parry;
                    duration = PlayerAnimatorActions.ParryDuration;
                    break;
                default:
                    return;
            }

            var request = new AnimationActionRequest(action, 0, duration);
            if (_animation.TryPlayAction(
                    PlayerAnimationSource.Defense,
                    in request,
                    out AnimationActionPlayback playback))
            {
                _impactTimeRemaining = playback.Duration;
                return;
            }

            _impact = ImpactPresentation.None;
            _impactTimeRemaining = 0.0f;
        }

        private void PlayGuard(bool resumeHoldPose)
        {
            var request = new AnimationActionRequest(
                PlayerAnimatorActions.Guard,
                resumeHoldPose ? 1 : 0,
                PlayerAnimatorActions.GuardDuration);
            _animation.TryPlayAction(
                PlayerAnimationSource.Defense,
                in request,
                out _);
        }

        private void Reset()
        {
            _impact = ImpactPresentation.None;
            _impactTimeRemaining = 0.0f;
            _animation.Cancel(PlayerAnimationSource.Defense);
            _animation.ReleaseAll(PlayerAnimationSource.Defense);
        }
    }
}
