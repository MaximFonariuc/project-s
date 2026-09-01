using System;
using ModularInput;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerDefenseController :
        IPlayerDefenseState,
        ICombatHitReceiver,
        IDisposable
    {
        private const float DirectionThresholdSqr = 0.0001f;

        private readonly IPlayerInput _input;
        private readonly IPlayerDefenseActor _actor;
        private readonly IPlayerLocomotionEvents _locomotion;
        private readonly Health _health;
        private readonly BodyColliderRig _bodyRig;
        private readonly ICombatClock _clock;
        private readonly PlayerDefenseSettings _settings;
        private readonly float _minimumGuardDot;

        private bool _guardHeld;
        private bool _guarding;
        private bool _parryAvailable;
        private double _guardStartedAt;
        private double _nextParryAllowedAt;
        private float _impactRecoveryRemaining;
        private CombatHitOutcome _impactRecoveryOutcome;
        private bool _enabled;

        public event Action GuardStarted;
        public event Action GuardEnded;
        public event Action<PlayerDefenseResult> Defended;

        public PlayerDefenseSnapshot Snapshot => IsImpactRecovering
            ? new PlayerDefenseSnapshot(
                _impactRecoveryOutcome == CombatHitOutcome.Parried
                    ? PlayerDefenseStateId.ParryRecovery
                    : PlayerDefenseStateId.BlockRecovery,
                _guarding,
                false,
                true,
                _impactRecoveryOutcome)
            : _guarding
                ? new PlayerDefenseSnapshot(
                    PlayerDefenseStateId.Guarding,
                    true,
                    IsParryWindowOpen(),
                    false,
                    default)
                : PlayerDefenseSnapshot.Inactive;
        public bool IsGuarding => _guarding;
        public bool BlocksActions => _guarding || IsImpactRecovering;
        public PlayerMovementIntent MovementIntent => IsImpactRecovering
            ? PlayerMovementIntent.Locked
            : _guarding
                ? _settings.MovementIntent
                : PlayerMovementIntent.Unrestricted;

        private bool IsImpactRecovering => _impactRecoveryRemaining > 0.0f;

        public PlayerDefenseController(
            IPlayerInput input,
            IPlayerDefenseActor actor,
            IPlayerLocomotionEvents locomotion,
            Health health,
            BodyColliderRig bodyRig,
            ICombatClock clock,
            PlayerDefenseSettings settings)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _health = health != null
                ? health
                : throw new ArgumentNullException(nameof(health));
            _bodyRig = bodyRig != null
                ? bodyRig
                : throw new ArgumentNullException(nameof(bodyRig));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _minimumGuardDot = Mathf.Cos(
                _settings.GuardArcDegrees * 0.5f * Mathf.Deg2Rad);
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _bodyRig.SetHitReceiver(this);
            _enabled = true;
            try
            {
                _input.GuardChanged += OnGuardChanged;
                _locomotion.DodgeAccepted += OnLocomotionActionAccepted;
                _locomotion.GroundJumpIntentAccepted += OnLocomotionActionAccepted;
                _guardHeld = false;
                _nextParryAllowedAt = 0.0;
                ClearImpactRecovery();
            }
            catch
            {
                Disable();
                throw;
            }
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.GuardChanged -= OnGuardChanged;
            _locomotion.DodgeAccepted -= OnLocomotionActionAccepted;
            _locomotion.GroundJumpIntentAccepted -= OnLocomotionActionAccepted;
            _guardHeld = false;
            ClearImpactRecovery();
            ExitGuard();
            if (_bodyRig != null)
            {
                _bodyRig.ClearHitReceiver(this);
            }

            _enabled = false;
        }

        public void Tick(float scaledDeltaTime, bool canEnterGuard)
        {
            if (!_enabled)
            {
                return;
            }

            _impactRecoveryRemaining = Mathf.Max(
                0.0f,
                _impactRecoveryRemaining - Mathf.Max(0.0f, scaledDeltaTime));
            if (!IsImpactRecovering)
            {
                _impactRecoveryOutcome = default;
            }

            if (_guardHeld && canEnterGuard && _actor.CanDefend)
            {
                EnterGuard();
            }
            else
            {
                ExitGuard();
            }
        }

        public void Interrupt()
        {
            _guardHeld = false;
            ClearImpactRecovery();
            ExitGuard();
        }

        public void Dispose()
        {
            Disable();
            GuardStarted = null;
            GuardEnded = null;
            Defended = null;
        }

        bool ICombatHitReceiver.TryReceiveHit(
            in CombatHitResult proposedHit,
            out CombatHitResult resolvedHit)
        {
            if (_enabled && _guarding && _actor.CanDefend &&
                IsWithinGuardArc(in proposedHit))
            {
                CombatHitOutcome outcome = IsParryWindowOpen()
                    ? CombatHitOutcome.Parried
                    : CombatHitOutcome.Blocked;
                if (outcome == CombatHitOutcome.Parried)
                {
                    _parryAvailable = false;
                }

                _impactRecoveryOutcome = outcome;
                _impactRecoveryRemaining = outcome == CombatHitOutcome.Parried
                    ? _settings.ParryImpactRecoverySeconds
                    : _settings.BlockRecoverySeconds;

                resolvedHit = proposedHit.AsDefenseOutcome(outcome);
                var defenseResult = new PlayerDefenseResult(outcome, in resolvedHit);
                SafeEvent.Publish(Defended, defenseResult, _actor.Root);
                return true;
            }

            if (_health.TryApplyHit(in proposedHit))
            {
                resolvedHit = proposedHit;
                return true;
            }

            resolvedHit = default;
            return false;
        }

        private void OnGuardChanged(InputSignalPhase phase)
        {
            if (phase is InputSignalPhase.Started or InputSignalPhase.Performed)
            {
                _guardHeld = true;
            }
            else if (phase == InputSignalPhase.Canceled)
            {
                _guardHeld = false;
                ExitGuard();
            }
        }

        private void OnLocomotionActionAccepted()
        {
            _guardHeld = false;
            ClearImpactRecovery();
            ExitGuard();
        }

        private void ClearImpactRecovery()
        {
            _impactRecoveryRemaining = 0.0f;
            _impactRecoveryOutcome = default;
        }

        private void EnterGuard()
        {
            if (_guarding)
            {
                return;
            }

            double now = _clock.UnscaledTime;
            _guarding = true;
            _guardStartedAt = now;
            _parryAvailable = _settings.ParryWindowSeconds > 0.0 &&
                              now >= _nextParryAllowedAt;
            if (_parryAvailable)
            {
                _nextParryAllowedAt = now + _settings.ParryRecoverySeconds;
            }

            SafeEvent.Publish(GuardStarted, _actor.Root);
        }

        private void ExitGuard()
        {
            if (!_guarding)
            {
                return;
            }

            _guarding = false;
            _parryAvailable = false;
            _guardStartedAt = 0.0;
            SafeEvent.Publish(GuardEnded, _actor.Root);
        }

        private bool IsParryWindowOpen()
        {
            return _guarding && _parryAvailable &&
                   _clock.UnscaledTime - _guardStartedAt <=
                   _settings.ParryWindowSeconds;
        }

        private bool IsWithinGuardArc(in CombatHitResult hit)
        {
            Vector3 up = _actor.Up;
            if (up.sqrMagnitude <= DirectionThresholdSqr)
            {
                up = Vector3.up;
            }
            else
            {
                up.Normalize();
            }

            Vector3 toSource = hit.Source != null
                ? hit.Source.transform.position - _actor.Position
                : -hit.Direction;
            toSource = Vector3.ProjectOnPlane(toSource, up);
            Vector3 forward = Vector3.ProjectOnPlane(_actor.Forward, up);
            if (toSource.sqrMagnitude <= DirectionThresholdSqr ||
                forward.sqrMagnitude <= DirectionThresholdSqr)
            {
                return false;
            }

            return Vector3.Dot(forward.normalized, toSource.normalized) >=
                   _minimumGuardDot;
        }
    }
}
