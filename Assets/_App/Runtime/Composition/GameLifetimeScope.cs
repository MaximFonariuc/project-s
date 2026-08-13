using System;
using ModularInput;
using ModularInput.Unity;
using ModularStateMachine.VContainerIntegration;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _App
{
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private ThirdPersonController _player;
        [SerializeField] private InputActionAsset _inputActions;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_player == null)
            {
                throw new InvalidOperationException($"{nameof(GameLifetimeScope)} requires a Player reference.");
            }

            if (_inputActions == null)
            {
                throw new InvalidOperationException($"{nameof(GameLifetimeScope)} requires an InputActionAsset.");
            }

            Character character = _player.GetComponent<Character>();
            if (character == null || _player.AbilitiesData == null || _player.PlayerAnimator == null)
            {
                throw new InvalidOperationException(
                    "Player locomotion requires Character, AbilitiesData and Animator references.");
            }

            builder.Register<IInputSource>(_ => new UnityInputSource(_inputActions), Lifetime.Scoped);
            builder.Register<IInputService, InputRouter>(Lifetime.Scoped);
            builder.Register<IPlayerInput, PlayerInputAdapter>(Lifetime.Scoped);

            builder.RegisterComponent(character);
            builder.Register(
                _ => new AnimationController(character, _player.PlayerAnimator),
                Lifetime.Scoped);
            builder.Register(
                resolver => new AbilityController(
                    _player.AbilitiesData,
                    character,
                    resolver.Resolve<AnimationController>()),
                Lifetime.Scoped);

            builder.Register<ICharacterMotor, CharacterMotor>(Lifetime.Scoped);
            builder.Register<IPlayerMovementReference, CameraRelativeMovementReference>(Lifetime.Scoped);
            builder.Register<IPlayerLocomotionPresentation, PlayerLocomotionAnimationAdapter>(Lifetime.Scoped);
            builder.Register<PlayerLocomotionRuntime>(Lifetime.Scoped);

            builder.RegisterStateMachine<PlayerLocomotionStateId>(Lifetime.Scoped)
                .State<PlayerIdleState>(PlayerLocomotionStateId.Idle, Lifetime.Scoped)
                .State<PlayerRunState>(PlayerLocomotionStateId.Run, Lifetime.Scoped)
                .State<PlayerJumpState>(PlayerLocomotionStateId.Jump, Lifetime.Scoped)
                .State<PlayerFallState>(PlayerLocomotionStateId.Fall, Lifetime.Scoped)
                .State<PlayerDodgeState>(PlayerLocomotionStateId.Dodge, Lifetime.Scoped)
                .Transition(PlayerLocomotionStateId.Idle, PlayerLocomotionStateId.Run)
                .Transition(PlayerLocomotionStateId.Idle, PlayerLocomotionStateId.Jump)
                .Transition(PlayerLocomotionStateId.Idle, PlayerLocomotionStateId.Fall)
                .Transition(PlayerLocomotionStateId.Idle, PlayerLocomotionStateId.Dodge)
                .Transition(PlayerLocomotionStateId.Run, PlayerLocomotionStateId.Idle)
                .Transition(PlayerLocomotionStateId.Run, PlayerLocomotionStateId.Jump)
                .Transition(PlayerLocomotionStateId.Run, PlayerLocomotionStateId.Fall)
                .Transition(PlayerLocomotionStateId.Run, PlayerLocomotionStateId.Dodge)
                .Transition(PlayerLocomotionStateId.Jump, PlayerLocomotionStateId.Idle)
                .Transition(PlayerLocomotionStateId.Jump, PlayerLocomotionStateId.Run)
                .Transition(PlayerLocomotionStateId.Jump, PlayerLocomotionStateId.Fall)
                .Transition(PlayerLocomotionStateId.Fall, PlayerLocomotionStateId.Idle)
                .Transition(PlayerLocomotionStateId.Fall, PlayerLocomotionStateId.Run)
                .Transition(PlayerLocomotionStateId.Fall, PlayerLocomotionStateId.Jump)
                .Transition(PlayerLocomotionStateId.Dodge, PlayerLocomotionStateId.Idle)
                .Transition(PlayerLocomotionStateId.Dodge, PlayerLocomotionStateId.Run)
                .Transition(PlayerLocomotionStateId.Dodge, PlayerLocomotionStateId.Fall);

            builder.Register<PlayerLocomotionController>(Lifetime.Scoped);
            builder.RegisterComponent(_player);
        }
    }
}
