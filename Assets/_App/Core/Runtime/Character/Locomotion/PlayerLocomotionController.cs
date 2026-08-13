using System;
using ModularInput;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    public sealed class PlayerLocomotionController : IPlayerLocomotionEvents, IDisposable
    {
        private const float MoveThresholdSqr = 0.0001f;
        private const float AscendingThreshold = 0.05f;

        private readonly IPlayerInput _input;
        private readonly ICharacterMotor _motor;
        private readonly IPlayerMovementReference _movementReference;
        private readonly IPlayerLocomotionPresentation _presentation;
        private readonly PlayerLocomotionRuntime _runtime;
        private readonly IStateMachine<PlayerLocomotionStateId> _stateMachine;

        private PlayerMovementIntent _movementIntent = PlayerMovementIntent.Unrestricted;
        private bool _walkHeld;
        private bool _sprintHeld;
        private bool _enabled;

        public event Action DodgeAccepted;

        public PlayerLocomotionStateId? CurrentState => _stateMachine.CurrentStateId;

        public PlayerLocomotionController(
            IPlayerInput input,
            ICharacterMotor motor,
            IPlayerMovementReference movementReference,
            IPlayerLocomotionPresentation presentation,
            PlayerLocomotionRuntime runtime,
            IStateMachine<PlayerLocomotionStateId> stateMachine)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
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

            _input.WalkChanged += OnWalkChanged;
            _input.SprintChanged += OnSprintChanged;
            _input.JumpChanged += OnJumpChanged;
            _input.DodgeRequested += OnDodgeRequested;

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

            _walkHeld = false;
            _sprintHeld = false;
            _movementIntent = PlayerMovementIntent.Unrestricted;
            _motor.ReleaseJump();
            _motor.StopGliding();
            SetSpeedMode(PlayerSpeedMode.Run);
            _motor.SetMovementDirection(Vector3.zero);

            if (_stateMachine.IsRunning)
            {
                _stateMachine.Stop();
            }

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

            _presentation.Tick();
        }

        public bool TryInvertGravity()
        {
            return _stateMachine.IsRunning && _motor.TryInvertGravity();
        }

        public void Dispose()
        {
            Disable();
            DodgeAccepted = null;
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
            _presentation.SetSpeedMode(speedMode);
        }

        private void OnJumpChanged(InputSignalPhase phase)
        {
            switch (phase)
            {
                case InputSignalPhase.Started when _movementIntent.AllowJump && !_motor.IsDodging:
                    _motor.PressJump();
                    break;

                case InputSignalPhase.Performed when _movementIntent.AllowJump && !_motor.IsSwimming && !_motor.IsDodging:
                    _motor.StartGliding();
                    break;

                case InputSignalPhase.Canceled:
                    _motor.StopGliding();
                    _motor.ReleaseJump();
                    break;
            }
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

            if (!_motor.TryDodge(_runtime.RawMoveDirection, backwards))
            {
                return;
            }

            _runtime.DodgeBackwards = backwards;
            DodgeAccepted?.Invoke();
        }
    }
}
