using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    [Serializable]
    public sealed class PlayerAttackRecoilAuthoring
    {
        [SerializeField, Min(0.01f)] private float _parryDuration = 0.55f;

        internal PlayerAttackRecoilSettings Build()
        {
            return new PlayerAttackRecoilSettings(_parryDuration);
        }
    }

    internal sealed class PlayerAttackRecoilSettings
    {
        public float ParryDuration { get; }

        public PlayerAttackRecoilSettings(float parryDuration)
        {
            CombatActionNumeric.RequireFinite(parryDuration, nameof(parryDuration));
            if (parryDuration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(parryDuration));
            }

            ParryDuration = parryDuration;
        }
    }

    internal readonly struct PlayerAttackRecoilSnapshot
    {
        public static PlayerAttackRecoilSnapshot Inactive => default;

        public bool IsActive { get; }
        public CombatHitResult Hit { get; }
        public CharacterReactionDirection Direction { get; }
        public float RemainingDuration { get; }

        public PlayerAttackRecoilSnapshot(
            in CombatHitResult hit,
            CharacterReactionDirection direction,
            float remainingDuration)
        {
            IsActive = true;
            Hit = hit;
            Direction = direction;
            RemainingDuration = Mathf.Max(0.0f, remainingDuration);
        }
    }

    internal enum PlayerAttackRecoilEndReason
    {
        Completed,
        Interrupted,
        Disabled
    }

    internal interface IPlayerAttackRecoilState
    {
        event Action<PlayerAttackRecoilSnapshot> Started;
        event Action<PlayerAttackRecoilEndReason> Ended;

        bool IsActive { get; }
        PlayerAttackRecoilSnapshot Snapshot { get; }
    }

    /// <summary>
    /// Owns the source-side loss of control caused by a confirmed parry.
    /// This state is independent from damage reactions and combat phase.
    /// </summary>
    internal sealed class PlayerAttackRecoilController :
        IPlayerAttackRecoilState,
        IDisposable
    {
        private readonly CombatActionSession _actionSession;
        private readonly IPlayerCombatActor _actor;
        private readonly PlayerAttackRecoilSettings _settings;
        private readonly Transform _eventContext;

        private PlayerAttackRecoilSnapshot _snapshot;
        private float _remainingDuration;
        private bool _enabled;
        private bool _disposed;

        public event Action<PlayerAttackRecoilSnapshot> Started;
        public event Action<PlayerAttackRecoilEndReason> Ended;

        public bool IsActive => _snapshot.IsActive;
        public PlayerAttackRecoilSnapshot Snapshot => _snapshot;

        public PlayerAttackRecoilController(
            CombatActionSession actionSession,
            IPlayerCombatActor actor,
            PlayerAttackRecoilSettings settings)
        {
            _actionSession = actionSession ??
                throw new ArgumentNullException(nameof(actionSession));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _eventContext = actor.Root;
        }

        public void Enable()
        {
            ThrowIfDisposed();
            if (_enabled)
            {
                return;
            }

            _actionSession.AttackParried += OnAttackParried;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _actionSession.AttackParried -= OnAttackParried;
            _enabled = false;
            EndRecoil(PlayerAttackRecoilEndReason.Disabled);
        }

        public void Tick(float scaledDeltaTime)
        {
            if (!_enabled || !IsActive)
            {
                return;
            }

            _remainingDuration -= Mathf.Max(0.0f, scaledDeltaTime);
            if (_remainingDuration <= 0.0f)
            {
                EndRecoil(PlayerAttackRecoilEndReason.Completed);
                return;
            }

            CombatHitResult hit = _snapshot.Hit;
            _snapshot = new PlayerAttackRecoilSnapshot(
                in hit,
                _snapshot.Direction,
                _remainingDuration);
        }

        public void Interrupt()
        {
            EndRecoil(PlayerAttackRecoilEndReason.Interrupted);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disable();
            Started = null;
            Ended = null;
            _disposed = true;
        }

        private void OnAttackParried(CombatHitResult hit)
        {
            if (!_enabled || hit.Outcome != CombatHitOutcome.Parried)
            {
                return;
            }

            Vector3 up = _eventContext != null ? _eventContext.up : Vector3.up;
            CharacterReactionDirection direction =
                CharacterReactionController.ResolveDirection(
                    -hit.Direction,
                    up,
                    _actor.Forward);
            _remainingDuration = _settings.ParryDuration;
            _snapshot = new PlayerAttackRecoilSnapshot(
                in hit,
                direction,
                _remainingDuration);
            SafeEvent.Publish(Started, _snapshot, _eventContext);
        }

        private void EndRecoil(PlayerAttackRecoilEndReason reason)
        {
            if (!IsActive)
            {
                _remainingDuration = 0.0f;
                _snapshot = PlayerAttackRecoilSnapshot.Inactive;
                return;
            }

            _remainingDuration = 0.0f;
            _snapshot = PlayerAttackRecoilSnapshot.Inactive;
            SafeEvent.Publish(Ended, reason, _eventContext);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerAttackRecoilController));
            }
        }
    }

    /// <summary>
    /// Adapts source-side recoil to the shared Animator owner. Gameplay timing
    /// remains in PlayerAttackRecoilController.
    /// </summary>
    internal sealed class PlayerAttackRecoilAnimationAdapter : IDisposable
    {
        private readonly IPlayerAttackRecoilState _recoil;
        private readonly PlayerAnimationCoordinator _animation;
        private bool _enabled;

        public PlayerAttackRecoilAnimationAdapter(
            IPlayerAttackRecoilState recoil,
            PlayerAnimationCoordinator animation)
        {
            _recoil = recoil ?? throw new ArgumentNullException(nameof(recoil));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _recoil.Started += OnStarted;
            _recoil.Ended += OnEnded;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _recoil.Started -= OnStarted;
            _recoil.Ended -= OnEnded;
            _animation.Cancel(PlayerAnimationSource.Reaction);
            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnStarted(PlayerAttackRecoilSnapshot snapshot)
        {
            AnimationSlotId slot = PlayerAnimationSlots.Reaction(
                CharacterReactionKind.Light,
                CharacterReactionStateId.HitStun,
                snapshot.Direction);
            _animation.CrossFade(
                PlayerAnimationSource.Reaction,
                slot,
                snapshot.RemainingDuration);
        }

        private void OnEnded(PlayerAttackRecoilEndReason reason)
        {
            if (reason == PlayerAttackRecoilEndReason.Completed)
            {
                _animation.Cancel(PlayerAnimationSource.Reaction);
                return;
            }

            // An incoming damage/death reaction will request the same higher
            // priority source immediately after its state transition. Releasing
            // ownership avoids an unnecessary Recoil -> Move -> Reaction hop.
            _animation.ReleaseAll(PlayerAnimationSource.Reaction);
        }
    }
}
