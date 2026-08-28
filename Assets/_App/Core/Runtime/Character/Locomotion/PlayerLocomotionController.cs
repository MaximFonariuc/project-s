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
        private const float MoveThresholdSqr = 0.0001f;
        private const float AscendingThreshold = 0.05f;
        private const float JumpStartupSeconds = 0.12f;

        private readonly IPlayerInput _input;
        private readonly ICharacterMotor _motor;
        private readonly IPlayerLocomotionActions _actions;
        private readonly IPlayerMovementReference _movementReference;
        private readonly IPlayerLocomotionPresentation _presentation;
        private readonly PlayerLocomotionRuntime _runtime;
        private readonly IStateMachine<PlayerLocomotionStateId> _stateMachine;

        private PlayerMovementIntent _movementIntent = PlayerMovementIntent.Unrestricted;
        private PlayerLocomotionSnapshot _snapshot = PlayerLocomotionSnapshot.Invalid;
        private bool _walkHeld;
        private bool _sprintHeld;
        private bool _jumpHeld;
        private bool _enabled;

        public event Action JumpAccepted;
        public event Action DodgeAccepted;

        public PlayerLocomotionStateId? CurrentState => _stateMachine.CurrentStateId;
        public PlayerSpeedMode SpeedMode => _runtime.SpeedMode;
        public PlayerLocomotionSnapshot Snapshot => _snapshot;

        public PlayerLocomotionController(
            IPlayerInput input,
            ICharacterMotor motor,
            IPlayerLocomotionActions actions,
            IPlayerMovementReference movementReference,
            IPlayerLocomotionPresentation presentation,
            PlayerLocomotionRuntime runtime,
            IStateMachine<PlayerLocomotionStateId> stateMachine)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _movementReference = movementReference ?? throw new ArgumentNullException(nameof(movementReference));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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

            _walkHeld = false;
            _sprintHeld = false;
            _jumpHeld = false;
            _movementIntent = PlayerMovementIntent.Unrestricted;
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

        public void Tick(float deltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            UpdateJumpStartup(deltaTime);
            RefreshMovementDirections();
            PlayerLocomotionStateId stateId = ResolveState();
            if (_stateMachine.IsRunning)
            {
                ChangeStateIfNeeded(stateId);
            }
            else
            {
                StartStateMachine(stateId);
            }

            _stateMachine.Tick(deltaTime);

            if (_movementIntent.FaceMoveDirection && _runtime.RawMoveDirection.sqrMagnitude > MoveThresholdSqr)
            {
                _motor.FaceDirection(_runtime.RawMoveDirection, deltaTime);
            }

            _snapshot = CreateSnapshot(stateId);
            _presentation.Present(_snapshot, deltaTime);
        }

        public bool TryInvertGravity()
        {
            return _stateMachine.IsRunning && _motor.TryInvertGravity();
        }

        public void Dispose()
        {
            Disable();
            JumpAccepted = null;
            DodgeAccepted = null;
        }

        private void OnMotorJumped()
        {
            JumpAccepted?.Invoke();
        }

        private void RefreshMovementDirections()
        {
            bool cameraRelative = _motor.RotationMode != ERotationMode.None;
            _runtime.RawMoveDirection = _movementReference.ToWorldDirection(_input.Move, _motor.Up, cameraRelative);

            bool traversalOwnsMovement = _motor.IsClimbing;
            _runtime.EffectiveMoveDirection = traversalOwnsMovement
                ? Vector3.zero
                : _runtime.RawMoveDirection * _movementIntent.TranslationScale;
        }

        private PlayerLocomotionStateId ResolveState()
        {
            if (_motor.IsDodging)
            {
                return PlayerLocomotionStateId.Dodge;
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
                _motor.IsCrouched,
                stateId == PlayerLocomotionStateId.Dodge && _runtime.DodgeBackwards);
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
            switch (phase)
            {
                case InputSignalPhase.Started when _movementIntent.AllowJump && !_motor.IsDodging:
                    _jumpHeld = true;
                    if (_motor.IsGrounded && !_motor.IsSwimming && !_motor.IsCrouched)
                    {
                        BeginJumpStartup();
                    }
                    else
                    {
                        _motor.PressJump(false);
                    }
                    break;

                case InputSignalPhase.Performed when _movementIntent.AllowJump && !_motor.IsSwimming && !_motor.IsDodging:
                    _actions.StartGliding();
                    break;

                case InputSignalPhase.Canceled:
                    _jumpHeld = false;
                    _actions.StopGliding();
                    if (!_runtime.JumpStarting)
                    {
                        _motor.ReleaseJump();
                    }
                    break;
            }
        }

        private void BeginJumpStartup()
        {
            if (_runtime.JumpStarting)
            {
                return;
            }

            _runtime.JumpStarting = true;
            _runtime.JumpStartupTimeRemaining = JumpStartupSeconds;
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

            _runtime.JumpStartupTimeRemaining -= Mathf.Max(0.0f, deltaTime);
            if (_runtime.JumpStartupTimeRemaining > 0.0f)
            {
                return;
            }

            bool releaseAfterSimulation = !_jumpHeld;
            CancelJumpStartup();
            _motor.PressJump(releaseAfterSimulation);
        }

        private void CancelJumpStartup()
        {
            _runtime.JumpStarting = false;
            _runtime.JumpStartupTimeRemaining = 0.0f;
        }

        private void OnDodgeRequested()
        {
            if (!_stateMachine.IsRunning || !_movementIntent.AllowDodge || _motor.IsFalling ||
                _motor.IsSwimming || _motor.IsDodging)
            {
                return;
            }

            RefreshMovementDirections();
            bool backwards = _input.Move.y < -0.5f &&
                             _motor.RotationMode == ERotationMode.OrientToViewAndMovement;

            if (!_actions.TryDodge(_runtime.RawMoveDirection, backwards))
            {
                return;
            }

            _runtime.DodgeBackwards = backwards;
            DodgeAccepted?.Invoke();
        }
    }
}
