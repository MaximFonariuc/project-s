using System;
using ModularInput;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    public sealed class PlayerLocomotionController :
        IPlayerLocomotionCommands,
        IPlayerLocomotionEvents,
        IPlayerLocomotionState,
        IDisposable
    {
        [Flags]
        private enum PendingJumpSignals : byte
        {
            None = 0,
            Started = 1 << 0,
            Performed = 1 << 1,
            Canceled = 1 << 2
        }

        private const float MoveThresholdSqr = 0.0001f;
        private const float AscendingThreshold = 0.05f;
        private const float JumpStartupSeconds = 0.12f;
        private const float JumpTakeoffTimeoutSeconds = 0.10f;

        private readonly IPlayerInput _input;
        private readonly ICharacterMotor _motor;
        private readonly IPlayerLocomotionActions _actions;
        private readonly IPlayerMovementReference _movementReference;
        private readonly IPlayerLocomotionPresentation _presentation;
        private readonly PlayerLocomotionRuntime _runtime;
        private readonly PlayerLocomotionSettings _settings;
        private readonly IStateMachine<PlayerLocomotionStateId> _stateMachine;
        private readonly bool _canJump;
        private readonly float _jumpInputBufferDuration;
        private readonly float _dodgeInputBufferDuration;

        private PlayerMovementIntent _movementIntent = PlayerMovementIntent.Unrestricted;
        private PlayerLocomotionSnapshot _snapshot = PlayerLocomotionSnapshot.Invalid;
        private bool _walkHeld;
        private bool _sprintHeld;
        private bool _jumpHeld;
        private bool _jumpTakeoffPending;
        private float _jumpTakeoffTimeRemaining;
        private PendingJumpSignals _pendingJumpSignals;
        private bool _jumpRequested;
        private float _jumpRequestTimeRemaining;
        private bool _dodgeRequested;
        private float _dodgeRequestTimeRemaining;
        private PlayerLanding _landing;
        private float _landingRecoveryRemaining;
        private bool _enabled;

        public event Action GroundJumpIntentAccepted;
        public event Action JumpAccepted;
        public event Action DodgeAccepted;
        public event Action<PlayerLanding> Landed;

        public PlayerLocomotionStateId? CurrentState => _stateMachine.CurrentStateId;
        public PlayerSpeedMode SpeedMode => _runtime.SpeedMode;
        public PlayerLocomotionSnapshot Snapshot => _snapshot;
        public bool BlocksActions => _landingRecoveryRemaining > 0.0f;

        public PlayerLocomotionController(
            IPlayerInput input,
            ICharacterMotor motor,
            IPlayerLocomotionActions actions,
            IPlayerMovementReference movementReference,
            IPlayerLocomotionPresentation presentation,
            PlayerLocomotionRuntime runtime,
            PlayerLocomotionSettings settings,
            IStateMachine<PlayerLocomotionStateId> stateMachine)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _movementReference = movementReference ?? throw new ArgumentNullException(nameof(movementReference));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _canJump = _settings.CanJump;
            _jumpInputBufferDuration = _settings.JumpInputBufferDuration;
            _dodgeInputBufferDuration = _settings.DodgeInputBufferDuration;
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _actions.Enable();
            _input.WalkChanged += OnWalkChanged;
            _input.SprintChanged += OnSprintChanged;
            _input.JumpChanged += OnJumpChanged;
            _input.DodgeRequested += OnDodgeRequested;
            _motor.Jumped += OnMotorJumped;
            _motor.Landed += OnMotorLanded;

            _enabled = true;
            ApplySpeedMode();
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.WalkChanged -= OnWalkChanged;
            _input.SprintChanged -= OnSprintChanged;
            _input.JumpChanged -= OnJumpChanged;
            _input.DodgeRequested -= OnDodgeRequested;
            _motor.Jumped -= OnMotorJumped;
            _motor.Landed -= OnMotorLanded;

            _walkHeld = false;
            _sprintHeld = false;
            _jumpHeld = false;
            _pendingJumpSignals = PendingJumpSignals.None;
            ClearJumpRequest();
            _dodgeRequested = false;
            _dodgeRequestTimeRemaining = 0.0f;
            _movementIntent = PlayerMovementIntent.Unrestricted;
            ClearLanding();
            CancelJumpStartup();
            _motor.ReleaseJump();
            _actions.StopGliding();
            SetSpeedMode(PlayerSpeedMode.Run);
            _motor.SetMovementDirection(Vector3.zero);

            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }

            _snapshot = PlayerLocomotionSnapshot.Invalid;
            _presentation.Reset();
            _actions.Disable();
            _enabled = false;
        }

        public void SetMovementIntent(PlayerMovementIntent movementIntent)
        {
            _movementIntent = movementIntent;
        }

        internal bool HasPriorityActionRequest(in PlayerMovementIntent movementIntent)
        {
            if (!_enabled)
            {
                return false;
            }

            if (_dodgeRequested && CanConsumeDodgeRequest(in movementIntent))
            {
                return true;
            }

            bool jumpRequested = _jumpRequested ||
                (_pendingJumpSignals & PendingJumpSignals.Started) != 0;
            return jumpRequested && CanConsumeJumpRequest(in movementIntent);
        }

        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            ConsumePendingActionInput(unscaledDeltaTime);
            UpdateJumpStartup(scaledDeltaTime);
            UpdateLandingRecovery(scaledDeltaTime);
            RefreshMovementDirections();
            RefreshDodgeContinuation();
            PlayerLocomotionStateId stateId = ResolveState();
            if (_stateMachine.IsRunning)
            {
                ChangeStateIfNeeded(stateId);
            }
            else
            {
                StartStateMachine(stateId);
            }

            _stateMachine.Tick(scaledDeltaTime);

            if (!BlocksActions && _movementIntent.FaceMoveDirection &&
                _runtime.RawMoveDirection.sqrMagnitude > MoveThresholdSqr)
            {
                _motor.FaceDirection(_runtime.RawMoveDirection, scaledDeltaTime);
            }

            _snapshot = CreateSnapshot(stateId);
            _presentation.Present(_snapshot, scaledDeltaTime);
            if (_landing.IsValid && !_landing.BlocksActions)
            {
                _landing = PlayerLanding.None;
            }
        }

        public bool TryInvertGravity()
        {
            return _stateMachine.IsRunning && _motor.TryInvertGravity();
        }

        internal void InterruptActions()
        {
            if (!_enabled)
            {
                return;
            }

            _jumpHeld = false;
            _pendingJumpSignals = PendingJumpSignals.None;
            ClearJumpRequest();
            ClearDodgeRequest();
            ClearLanding();
            CancelJumpStartup();
            _motor.ReleaseJump();
            _motor.SetMovementDirection(Vector3.zero);
            _runtime.DodgeBackwards = false;
            _actions.Interrupt();
        }

        public void Dispose()
        {
            Disable();
            GroundJumpIntentAccepted = null;
            JumpAccepted = null;
            DodgeAccepted = null;
            Landed = null;
        }

        private void OnMotorJumped()
        {
            CancelJumpStartup();
            SafeEvent.Publish(JumpAccepted, null);
        }

        private void OnMotorLanded(float impactSpeed)
        {
            PlayerLanding landing = _settings.ResolveLanding(impactSpeed);
            _landing = landing;
            _landingRecoveryRemaining = landing.RecoveryDuration;
            _actions.RestoreAirDashCharges();
            if (landing.BlocksActions)
            {
                CancelJumpStartup();
                _motor.ReleaseJump();
                _actions.StopGliding();
                _runtime.EffectiveMoveDirection = Vector3.zero;
                _runtime.DodgeBackwards = false;
                _motor.StopPlanarMovement();
            }

            SafeEvent.Publish(Landed, landing, null);
        }

        private void RefreshMovementDirections()
        {
            bool cameraRelative = _motor.RotationMode != ERotationMode.None;
            _runtime.RawMoveDirection = _movementReference.ToWorldDirection(_input.Move, _motor.Up, cameraRelative);

            bool traversalOwnsMovement = _motor.IsClimbing;
            _runtime.EffectiveMoveDirection = traversalOwnsMovement || BlocksActions
                ? Vector3.zero
                : _runtime.RawMoveDirection * _movementIntent.TranslationScale;
        }

        private void RefreshDodgeContinuation()
        {
            bool steering = !BlocksActions &&
                            _runtime.RawMoveDirection.sqrMagnitude > MoveThresholdSqr;
            _actions.SetDodgeContinuationSpeed(steering ? _motor.MaxSpeed : 0.0f);
        }

        private PlayerLocomotionStateId ResolveState()
        {
            if (BlocksActions)
            {
                return PlayerLocomotionStateId.Landing;
            }

            if (_motor.IsDodging)
            {
                return PlayerLocomotionStateId.Dodge;
            }

            if (_motor.IsAirDashing)
            {
                return PlayerLocomotionStateId.AirDash;
            }

            if (_motor.IsFalling)
            {
                float verticalSpeed = Vector3.Dot(_motor.Velocity, _motor.Up);
                return _motor.IsJumping || verticalSpeed > AscendingThreshold
                    ? PlayerLocomotionStateId.Jump
                    : PlayerLocomotionStateId.Fall;
            }

            return _runtime.EffectiveMoveDirection.sqrMagnitude > MoveThresholdSqr
                ? PlayerLocomotionStateId.Run
                : PlayerLocomotionStateId.Idle;
        }

        private void StartStateMachine(PlayerLocomotionStateId stateId)
        {
            switch (stateId)
            {
                case PlayerLocomotionStateId.Idle:
                    _stateMachine.Start<PlayerIdleState>();
                    break;
                case PlayerLocomotionStateId.Run:
                    _stateMachine.Start<PlayerRunState>();
                    break;
                case PlayerLocomotionStateId.Jump:
                    _stateMachine.Start<PlayerJumpState>();
                    break;
                case PlayerLocomotionStateId.Fall:
                    _stateMachine.Start<PlayerFallState>();
                    break;
                case PlayerLocomotionStateId.Dodge:
                    _stateMachine.Start<PlayerDodgeState>();
                    break;
                case PlayerLocomotionStateId.Landing:
                    _stateMachine.Start<PlayerLandingState>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stateId), stateId, null);
            }
        }

        private void ChangeStateIfNeeded(PlayerLocomotionStateId stateId)
        {
            if (_stateMachine.CurrentStateId == stateId)
            {
                return;
            }

            switch (stateId)
            {
                case PlayerLocomotionStateId.Idle:
                    _stateMachine.ChangeState<PlayerIdleState>();
                    break;
                case PlayerLocomotionStateId.Run:
                    _stateMachine.ChangeState<PlayerRunState>();
                    break;
                case PlayerLocomotionStateId.Jump:
                    _stateMachine.ChangeState<PlayerJumpState>();
                    break;
                case PlayerLocomotionStateId.Fall:
                    _stateMachine.ChangeState<PlayerFallState>();
                    break;
                case PlayerLocomotionStateId.Dodge:
                    _stateMachine.ChangeState<PlayerDodgeState>();
                    break;
                case PlayerLocomotionStateId.Landing:
                    _stateMachine.ChangeState<PlayerLandingState>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stateId), stateId, null);
            }
        }

        private void OnWalkChanged(InputSignalPhase phase)
        {
            if (phase == InputSignalPhase.Started)
            {
                _walkHeld = true;
            }
            else if (phase == InputSignalPhase.Canceled)
            {
                _walkHeld = false;
            }

            ApplySpeedMode();
        }

        private void OnSprintChanged(InputSignalPhase phase)
        {
            if (phase == InputSignalPhase.Started)
            {
                _sprintHeld = true;
            }
            else if (phase == InputSignalPhase.Canceled)
            {
                _sprintHeld = false;
            }

            ApplySpeedMode();
        }

        private void ApplySpeedMode()
        {
            PlayerSpeedMode speedMode = _sprintHeld
                ? PlayerSpeedMode.Sprint
                : _walkHeld
                    ? PlayerSpeedMode.Walk
                    : PlayerSpeedMode.Run;

            SetSpeedMode(speedMode);
        }

        private void SetSpeedMode(PlayerSpeedMode speedMode)
        {
            _runtime.SpeedMode = speedMode;
            _motor.SetSpeedMode(speedMode);
        }

        private PlayerLocomotionSnapshot CreateSnapshot(PlayerLocomotionStateId stateId)
        {
            Vector3 up = _motor.Up.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(_motor.Forward, up).normalized;
            if (forward.sqrMagnitude <= MoveThresholdSqr)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            }

            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 velocity = _motor.Velocity;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
            float maxSpeed = Mathf.Max(_motor.MaxSpeed, 0.0001f);

            return new PlayerLocomotionSnapshot(
                stateId,
                _runtime.SpeedMode,
                ResolveMode(),
                velocity,
                _runtime.EffectiveMoveDirection,
                Vector3.Dot(planarVelocity, forward),
                Vector3.Dot(planarVelocity, right),
                Vector3.Dot(velocity, up),
                planarVelocity.magnitude / maxSpeed,
                _motor.IsGrounded,
                _runtime.JumpStarting,
                _actions.IsDodgeStarting,
                _motor.IsCrouched,
                stateId == PlayerLocomotionStateId.Dodge && _runtime.DodgeBackwards,
                _landing);
        }

        private PlayerLocomotionMode ResolveMode()
        {
            if (_motor.IsClimbing)
            {
                return PlayerLocomotionMode.Climbing;
            }

            if (_motor.IsSwimming)
            {
                return PlayerLocomotionMode.Swimming;
            }

            if (_motor.IsFlying)
            {
                return PlayerLocomotionMode.Flying;
            }

            return _motor.IsGrounded
                ? PlayerLocomotionMode.Grounded
                : PlayerLocomotionMode.Airborne;
        }

        private void OnJumpChanged(InputSignalPhase phase)
        {
            _pendingJumpSignals |= phase switch
            {
                InputSignalPhase.Started => PendingJumpSignals.Started,
                InputSignalPhase.Performed => PendingJumpSignals.Performed,
                InputSignalPhase.Canceled => PendingJumpSignals.Canceled,
                _ => PendingJumpSignals.None
            };
        }

        private void ConsumePendingActionInput(float unscaledDeltaTime)
        {
            PendingJumpSignals jumpSignals = _pendingJumpSignals;
            bool dodgeRequested = _dodgeRequested;
            _pendingJumpSignals = PendingJumpSignals.None;

            if ((jumpSignals & PendingJumpSignals.Started) != 0)
            {
                _jumpHeld = true;
                QueueJumpRequest();
            }

            if ((jumpSignals & PendingJumpSignals.Canceled) != 0)
            {
                _jumpHeld = false;
            }

            if (dodgeRequested && TryConsumeDodgeRequest())
            {
                ClearDodgeRequest();
                ClearJumpRequest();
                // A release still has to terminate an existing jump/glide hold,
                // but a dodge accepted in this tick owns action priority over a
                // simultaneous jump press.
                if ((jumpSignals & PendingJumpSignals.Canceled) != 0)
                {
                    ApplyJumpSignal(InputSignalPhase.Canceled);
                }

                return;
            }

            if (dodgeRequested)
            {
                _dodgeRequestTimeRemaining = Mathf.Max(
                    0.0f,
                    _dodgeRequestTimeRemaining - Mathf.Max(0.0f, unscaledDeltaTime));
                if (_dodgeRequestTimeRemaining <= 0.0f)
                {
                    ClearDodgeRequest();
                }
            }

            if (_jumpRequested && TryConsumeJumpRequest())
            {
                ClearJumpRequest();
            }
            else if (_jumpRequested)
            {
                _jumpRequestTimeRemaining = Mathf.Max(
                    0.0f,
                    _jumpRequestTimeRemaining - Mathf.Max(0.0f, unscaledDeltaTime));
                if (_jumpRequestTimeRemaining <= 0.0f)
                {
                    ClearJumpRequest();
                }
            }

            if ((jumpSignals & PendingJumpSignals.Performed) != 0)
            {
                ApplyJumpSignal(InputSignalPhase.Performed);
            }

            if ((jumpSignals & PendingJumpSignals.Canceled) != 0)
            {
                ApplyJumpSignal(InputSignalPhase.Canceled);
            }
        }

        private void ApplyJumpSignal(InputSignalPhase phase)
        {
            switch (phase)
            {
                case InputSignalPhase.Performed when !BlocksActions &&
                    _movementIntent.AllowJump && !_motor.IsSwimming && !_motor.IsDodging:
                    _actions.StartGliding();
                    break;

                case InputSignalPhase.Canceled:
                    _actions.StopGliding();
                    if (!_runtime.JumpStarting)
                    {
                        _motor.ReleaseJump();
                    }
                    break;
            }
        }

        private bool TryConsumeJumpRequest()
        {
            if (!CanConsumeJumpRequest(in _movementIntent))
            {
                return false;
            }

            if (_motor.IsGrounded)
            {
                if (!BeginJumpStartup())
                {
                    return false;
                }

                SafeEvent.Publish(GroundJumpIntentAccepted, null);
            }
            else
            {
                _motor.PressJump(false);
            }

            return true;
        }

        private void QueueJumpRequest()
        {
            _jumpRequested = true;
            _jumpRequestTimeRemaining = _jumpInputBufferDuration;
        }

        private void ClearJumpRequest()
        {
            _jumpRequested = false;
            _jumpRequestTimeRemaining = 0.0f;
        }

        private bool BeginJumpStartup()
        {
            if (_runtime.JumpStarting)
            {
                return false;
            }

            _runtime.JumpStarting = true;
            _runtime.JumpStartupTimeRemaining = JumpStartupSeconds;
            return true;
        }

        private void UpdateJumpStartup(float deltaTime)
        {
            if (!_runtime.JumpStarting)
            {
                return;
            }

            if (!_movementIntent.AllowJump || _motor.IsDodging || _motor.IsSwimming || _motor.IsCrouched)
            {
                CancelJumpStartup();
                return;
            }

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            if (_jumpTakeoffPending)
            {
                _jumpTakeoffTimeRemaining -= safeDeltaTime;
                if (_jumpTakeoffTimeRemaining <= 0.0f)
                {
                    CancelJumpStartup();
                    _motor.ReleaseJump();
                }

                return;
            }

            _runtime.JumpStartupTimeRemaining -= safeDeltaTime;
            if (_runtime.JumpStartupTimeRemaining > 0.0f)
            {
                return;
            }

            bool releaseAfterSimulation = !_jumpHeld;
            _runtime.JumpStartupTimeRemaining = 0.0f;
            _jumpTakeoffPending = true;
            _jumpTakeoffTimeRemaining = JumpTakeoffTimeoutSeconds;
            _motor.PressJump(releaseAfterSimulation);
        }

        private void CancelJumpStartup()
        {
            _runtime.JumpStarting = false;
            _runtime.JumpStartupTimeRemaining = 0.0f;
            _jumpTakeoffPending = false;
            _jumpTakeoffTimeRemaining = 0.0f;
        }

        private void OnDodgeRequested()
        {
            _dodgeRequested = true;
            _dodgeRequestTimeRemaining = _dodgeInputBufferDuration;
        }

        private void ClearDodgeRequest()
        {
            _dodgeRequested = false;
            _dodgeRequestTimeRemaining = 0.0f;
        }

        private bool TryConsumeDodgeRequest()
        {
            bool airborne = CanConsumeAirDashRequest(in _movementIntent);
            if (!airborne && !CanConsumeDodgeRequest(in _movementIntent))
            {
                return false;
            }

            RefreshMovementDirections();
            bool backwards = _input.Move.y < -0.5f &&
                             _motor.RotationMode == ERotationMode.OrientToViewAndMovement;

            bool accepted = airborne
                ? _actions.TryAirDash(_runtime.RawMoveDirection, backwards)
                : _actions.TryDodge(_runtime.RawMoveDirection, backwards);
            if (!accepted)
            {
                return false;
            }

            _runtime.DodgeBackwards = backwards;
            SafeEvent.Publish(DodgeAccepted, null);
            return true;
        }

        private bool CanConsumeJumpRequest(in PlayerMovementIntent movementIntent)
        {
            return !BlocksActions && _canJump && movementIntent.AllowJump &&
                   !_motor.IsDodging &&
                   !_motor.IsSwimming && !_motor.IsCrouched &&
                   (!_motor.IsGrounded || !_runtime.JumpStarting);
        }

        private bool CanConsumeDodgeRequest(in PlayerMovementIntent movementIntent)
        {
            return !BlocksActions && movementIntent.AllowDodge && !_motor.IsFalling &&
                   !_motor.IsSwimming && !_motor.IsDodging &&
                   _actions.CanDodge;
        }

        private bool CanConsumeAirDashRequest(in PlayerMovementIntent movementIntent)
        {
            return !BlocksActions && movementIntent.AllowDodge && _motor.IsFalling &&
                   !_motor.IsSwimming && !_motor.IsDodging &&
                   _actions.CanAirDash;
        }

        private void UpdateLandingRecovery(float deltaTime)
        {
            if (!BlocksActions)
            {
                return;
            }

            if (!_motor.IsGrounded || _motor.IsFalling)
            {
                ClearLanding();
                return;
            }

            _landingRecoveryRemaining = Mathf.Max(
                0.0f,
                _landingRecoveryRemaining - Mathf.Max(0.0f, deltaTime));
            if (_landingRecoveryRemaining <= 0.0f)
            {
                _landing = PlayerLanding.None;
            }
        }

        private void ClearLanding()
        {
            _landing = PlayerLanding.None;
            _landingRecoveryRemaining = 0.0f;
        }
    }
}
