using System;
using ModularStateMachine;
using UnityEngine;

namespace _App
{
    internal abstract class PlayerLocomotionState : IState, IUpdateState
    {
        private readonly ICharacterMotor _motor;
        private readonly PlayerLocomotionRuntime _runtime;

        protected virtual bool AppliesMovement => true;

        protected PlayerLocomotionState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public void Enter()
        {
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
        protected override bool AppliesMovement => false;

        public PlayerIdleState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerRunState : PlayerLocomotionState
    {
        public PlayerRunState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerJumpState : PlayerLocomotionState
    {
        public PlayerJumpState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerFallState : PlayerLocomotionState
    {
        public PlayerFallState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerDodgeState : PlayerLocomotionState
    {
        protected override bool AppliesMovement => false;

        public PlayerDodgeState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerAirDashState : PlayerLocomotionState
    {
        protected override bool AppliesMovement => false;

        public PlayerAirDashState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }

    internal sealed class PlayerLandingState : PlayerLocomotionState
    {
        protected override bool AppliesMovement => false;

        public PlayerLandingState(
            ICharacterMotor motor,
            PlayerLocomotionRuntime runtime)
            : base(motor, runtime)
        {
        }
    }
}
