using System;
using ModularStateMachine.VContainerIntegration;
using VContainer;

namespace _App
{
    internal static class PlayerLocomotionComposition
    {
        public static void RegisterPlayerLocomotion(
            this IContainerBuilder builder,
            CharacterFeatureSet features)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            builder.Register(
                resolver => PlayerLocomotionSettings.From(
                    resolver.Resolve<Character>(),
                    features),
                Lifetime.Scoped);
            builder.Register<CharacterMotor>(Lifetime.Scoped)
                .As<ICharacterMotor>()
                .As<ICharacterImpactMotor>()
                .As<ICharacterActionMotionMotor>();
            builder.Register<IPlayerLocomotionActions>(
                resolver => new PlayerLocomotionActions(
                    resolver.Resolve<Character>(),
                    resolver.Resolve<ICharacterMotor>(),
                    features),
                Lifetime.Scoped);
            builder.Register<IPlayerMovementReference, CameraRelativeMovementReference>(Lifetime.Scoped);
            LocomotionAnimationSettings animationSettings =
                features.GetEnabled<LocomotionAnimationSettings>();
            builder.RegisterInstance(
                animationSettings != null
                    ? animationSettings.CreatePolicy()
                    : PlayerLocomotionAnimationPolicy.CreateDefaults());
            builder.Register<PlayerLocomotionAnimationResolver>(Lifetime.Scoped);
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

            builder.Register<PlayerLocomotionController>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerLocomotionCommands>()
                .As<IPlayerLocomotionEvents>()
                .As<IPlayerLocomotionState>();
        }
    }
}
