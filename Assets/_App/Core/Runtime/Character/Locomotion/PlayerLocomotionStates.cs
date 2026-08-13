using System;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    internal abstract class PlayerLocomotionState : IState, IUpdateState
    {
        private readonly ICharacterMotor _motor;
        private readonly PlayerLocomotionRuntime _runtime;
        private readonly IPlayerLocomotionPresentation _presentation;

        protected abstract PlayerLocomotionStateId StateId { get; }
        protected virtual bool AppliesMovement => true;

        protected PlayerLocomotionState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }

        public void Enter()
        {
            _presentation.EnterState(StateId, _runtime.DodgeBackwards);
        }

        public void Tick(float deltaTime)
        {
            _motor.SetMovementDirection(AppliesMovement ? _runtime.EffectiveMoveDirection : Vector3.zero);
        }

        public void Exit()
        {
        }
    }

    internal sealed class PlayerIdleState : PlayerLocomotionState
    {
        protected override PlayerLocomotionStateId StateId => PlayerLocomotionStateId.Idle;
        protected override bool AppliesMovement => false;

        public PlayerIdleState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
            : base(motor, runtime, presentation)
        {
        }
    }

    internal sealed class PlayerRunState : PlayerLocomotionState
    {
        protected override PlayerLocomotionStateId StateId => PlayerLocomotionStateId.Run;

        public PlayerRunState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
            : base(motor, runtime, presentation)
        {
        }
    }

    internal sealed class PlayerJumpState : PlayerLocomotionState
    {
        protected override PlayerLocomotionStateId StateId => PlayerLocomotionStateId.Jump;

        public PlayerJumpState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
            : base(motor, runtime, presentation)
        {
        }
    }

    internal sealed class PlayerFallState : PlayerLocomotionState
    {
        protected override PlayerLocomotionStateId StateId => PlayerLocomotionStateId.Fall;

        public PlayerFallState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
            : base(motor, runtime, presentation)
        {
        }
    }

    internal sealed class PlayerDodgeState : PlayerLocomotionState
    {
        protected override PlayerLocomotionStateId StateId => PlayerLocomotionStateId.Dodge;
        protected override bool AppliesMovement => false;

        public PlayerDodgeState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime,
            IPlayerLocomotionPresentation presentation)
            : base(motor, runtime, presentation)
        {
        }
    }
}
