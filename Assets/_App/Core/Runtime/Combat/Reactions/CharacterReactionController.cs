using System;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    public sealed class CharacterReactionController :
        ICharacterReactionState,
        IDisposable
    {
        private const float DirectionThresholdSqr = 0.0001f;

        private readonly Health _health;
        private readonly CharacterPoise _poise;
        private readonly ICharacterImpactMotor _impactMotor;
        private readonly ICharacterReactionPresentation _presentation;
        private readonly IStateMachine<CharacterReactionStateId> _stateMachine;
        private readonly CharacterReactionSettings _settings;

        private CombatHitResult _lastHit;
        private CharacterReactionDirection _reactionDirection;
        private float _remainingDuration;
        private bool _enabled;

        public event Action<CharacterReactionStateId> StateChanged;
        public event Action<CharacterReactionSnapshot> ReactionStarted;

        public CharacterReactionStateId? CurrentState => _stateMachine.CurrentStateId;
        public CharacterReactionSnapshot Snapshot => CreateSnapshot();
        public float CurrentPoise => _poise.Current;
        public float MaxPoise => _poise.Max;
        public bool BlocksActions =>
            _stateMachine.CurrentStateId is CharacterReactionStateId state &&
            state != CharacterReactionStateId.Stable;

        internal CharacterReactionController(
            Health health,
            CharacterPoise poise,
            ICharacterImpactMotor impactMotor,
            ICharacterReactionPresentation presentation,
            IStateMachine<CharacterReactionStateId> stateMachine,
            CharacterReactionSettings settings)
        {
            _health = health != null
                ? health
                : throw new ArgumentNullException(nameof(health));
            _poise = poise ?? throw new ArgumentNullException(nameof(poise));
            _impactMotor = impactMotor ?? throw new ArgumentNullException(nameof(impactMotor));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _health.Revived += OnRevived;
            _stateMachine.StateChanged += OnStateChanged;
            _poise.RestoreFull();
            _lastHit = default;
            _reactionDirection = CharacterReactionDirection.None;
            _remainingDuration = 0.0f;
            _enabled = true;

            if (_health.IsAlive)
            {
                _stateMachine.Start<CharacterStableState>();
            }
            else
            {
                _poise.Deplete();
                _stateMachine.Start<CharacterDeadState, CombatHitResult>(default);
            }
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            _health.Revived -= OnRevived;
            _stateMachine.StateChanged -= OnStateChanged;

            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }

            _poise.RestoreFull();
            _lastHit = default;
            _reactionDirection = CharacterReactionDirection.None;
            _remainingDuration = 0.0f;
            _presentation.Reset();
            _enabled = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_enabled || !_stateMachine.IsRunning)
            {
                return;
            }

            _stateMachine.Tick(deltaTime);
            if (_stateMachine.CurrentStateId == CharacterReactionStateId.Stable)
            {
                _poise.Tick(Mathf.Max(0.0f, deltaTime));
                return;
            }

            if (_stateMachine.CurrentStateId == CharacterReactionStateId.Dead)
            {
                return;
            }

            _remainingDuration -= Mathf.Max(0.0f, deltaTime);
            if (_remainingDuration <= 0.0f)
            {
                AdvanceTimedReaction(-_remainingDuration);
            }
        }

        public void Dispose()
        {
            Disable();
            StateChanged = null;
            ReactionStarted = null;
        }

        private void OnDamaged(CombatHitResult hit)
        {
            if (!_enabled || !_health.IsAlive)
            {
                return;
            }

            bool poiseBroken = _poise.ApplyStagger(hit.StaggerDamage);
            bool hasImpact = hit.Knockback > 0.0f || hit.VerticalKnockback > 0.0f;
            if (!poiseBroken && !hasImpact)
            {
                return;
            }

            if (hit.VerticalKnockback > 0.0f || !_impactMotor.IsGrounded)
            {
                EnterTimedReaction(
                    CharacterReactionStateId.Airborne,
                    in hit,
                    _settings.AirborneDuration);
                return;
            }

            if (hit.Knockback > 0.0f && hit.Knockback >= _settings.KnockdownThreshold)
            {
                EnterTimedReaction(
                    CharacterReactionStateId.Knockdown,
                    in hit,
                    _settings.KnockdownDuration);
                return;
            }

            EnterTimedReaction(
                CharacterReactionStateId.HitStun,
                in hit,
                _settings.HitStunDuration);
        }

        private void OnDied(CombatHitResult hit)
        {
            if (!_enabled)
            {
                return;
            }

            _poise.Deplete();
            _lastHit = hit;
            _reactionDirection = ResolveDirection(
                hit.Direction,
                _impactMotor.Up,
                _impactMotor.Forward);
            _remainingDuration = 0.0f;
            _stateMachine.ChangeState<CharacterDeadState, CombatHitResult>(hit);
            ApplyKnockback(in hit);

            CharacterReactionSnapshot snapshot = CreateSnapshot();
            _presentation.Present(in snapshot);
            ReactionStarted?.Invoke(snapshot);
        }

        private void OnRevived()
        {
            if (!_enabled || !_stateMachine.IsRunning)
            {
                return;
            }

            _poise.RestoreFull();
            _lastHit = default;
            _reactionDirection = CharacterReactionDirection.None;
            _remainingDuration = 0.0f;
            if (_stateMachine.CurrentStateId != CharacterReactionStateId.Stable)
            {
                _stateMachine.ChangeState<CharacterStableState>();
            }

            _presentation.Reset();
        }

        private void EnterTimedReaction(
            CharacterReactionStateId state,
            in CombatHitResult hit,
            float duration)
        {
            _lastHit = hit;
            _reactionDirection = ResolveDirection(
                hit.Direction,
                _impactMotor.Up,
                _impactMotor.Forward);
            _remainingDuration = duration;
            var context = new CharacterReactionContext(in hit, duration);

            switch (state)
            {
                case CharacterReactionStateId.HitStun:
                    _stateMachine.ChangeState<CharacterHitStunState, CharacterReactionContext>(context);
                    break;
                case CharacterReactionStateId.Knockdown:
                    _stateMachine.ChangeState<CharacterKnockdownState, CharacterReactionContext>(context);
                    break;
                case CharacterReactionStateId.Airborne:
                    _stateMachine.ChangeState<CharacterAirborneReactionState, CharacterReactionContext>(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            ApplyKnockback(in hit);
            CharacterReactionSnapshot snapshot = CreateSnapshot();
            _presentation.Present(in snapshot);
            ReactionStarted?.Invoke(snapshot);
        }

        private void ReturnToStable()
        {
            if (_poise.IsBroken)
            {
                _poise.RestoreFull();
            }

            _lastHit = default;
            _reactionDirection = CharacterReactionDirection.None;
            _remainingDuration = 0.0f;
            _stateMachine.ChangeState<CharacterStableState>();
            _presentation.Reset();
        }

        private void AdvanceTimedReaction(float elapsedPastEnd)
        {
            if (_stateMachine.CurrentStateId == CharacterReactionStateId.Airborne &&
                !_impactMotor.IsGrounded)
            {
                _remainingDuration = 0.0f;
                return;
            }

            if (_stateMachine.CurrentStateId == CharacterReactionStateId.Knockdown)
            {
                EnterGetUp(elapsedPastEnd);
                return;
            }

            ReturnToStable();
        }

        private void EnterGetUp(float elapsedBeforeEntry)
        {
            _remainingDuration = Mathf.Max(
                0.0f,
                _settings.GetUpDuration - Mathf.Max(0.0f, elapsedBeforeEntry));
            var context = new CharacterReactionContext(
                in _lastHit,
                _settings.GetUpDuration);
            _stateMachine.ChangeState<CharacterGetUpState, CharacterReactionContext>(context);

            if (_remainingDuration <= 0.0f)
            {
                ReturnToStable();
                return;
            }

            CharacterReactionSnapshot snapshot = CreateSnapshot();
            _presentation.Present(in snapshot);
        }

        private void ApplyKnockback(in CombatHitResult hit)
        {
            Vector3 velocity = Vector3.zero;
            if (hit.Knockback > 0.0f && hit.Direction.sqrMagnitude > DirectionThresholdSqr)
            {
                Vector3 direction = hit.Direction;
                if (_impactMotor.IsGrounded || hit.VerticalKnockback > 0.0f)
                {
                    direction = Vector3.ProjectOnPlane(direction, _impactMotor.Up);
                }

                if (direction.sqrMagnitude > DirectionThresholdSqr)
                {
                    velocity = direction.normalized * hit.Knockback;
                }
            }

            if (hit.VerticalKnockback > 0.0f)
            {
                Vector3 up = _impactMotor.Up;
                if (up.sqrMagnitude <= DirectionThresholdSqr)
                {
                    up = Vector3.up;
                }

                velocity += up.normalized * hit.VerticalKnockback;
            }

            if (velocity.sqrMagnitude > DirectionThresholdSqr)
            {
                _impactMotor.ApplyKnockback(velocity);
            }
        }

        private CharacterReactionSnapshot CreateSnapshot()
        {
            return _stateMachine.CurrentStateId is CharacterReactionStateId state
                ? new CharacterReactionSnapshot(
                    state,
                    _reactionDirection,
                    in _lastHit,
                    _poise.Current,
                    _poise.Max,
                    _remainingDuration)
                : CharacterReactionSnapshot.Invalid;
        }

        internal static CharacterReactionDirection ResolveDirection(
            Vector3 worldImpulseDirection,
            Vector3 characterUp,
            Vector3 characterForward)
        {
            Vector3 up = characterUp;
            if (up.sqrMagnitude <= DirectionThresholdSqr)
            {
                up = Vector3.up;
            }
            else
            {
                up.Normalize();
            }

            Vector3 impulseDirection = Vector3.ProjectOnPlane(worldImpulseDirection, up);
            if (impulseDirection.sqrMagnitude <= DirectionThresholdSqr)
            {
                return CharacterReactionDirection.Front;
            }

            Vector3 forward = Vector3.ProjectOnPlane(characterForward, up);
            if (forward.sqrMagnitude <= DirectionThresholdSqr)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
                if (forward.sqrMagnitude <= DirectionThresholdSqr)
                {
                    forward = Vector3.ProjectOnPlane(Vector3.right, up);
                }
            }

            impulseDirection.Normalize();
            forward.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            float forwardDot = Vector3.Dot(impulseDirection, forward);
            float rightDot = Vector3.Dot(impulseDirection, right);

            if (Mathf.Abs(forwardDot) >= Mathf.Abs(rightDot))
            {
                return forwardDot >= 0.0f
                    ? CharacterReactionDirection.Back
                    : CharacterReactionDirection.Front;
            }

            return rightDot >= 0.0f
                ? CharacterReactionDirection.Left
                : CharacterReactionDirection.Right;
        }

        private void OnStateChanged(
            CharacterReactionStateId previous,
            CharacterReactionStateId current)
        {
            StateChanged?.Invoke(current);
        }
    }
}
