using System;
using ModularAnimation;
using ModularAnimation.Unity;
using VContainer;
using VContainer.Unity;

namespace _App
{
    internal static class PlayerComposition
    {
        public static void RegisterPlayer(this IContainerBuilder builder, ThirdPersonController player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (!player.TryGetComponent(out Character character) ||
                player.CharacterFeatures == null ||
                player.PlayerAnimator == null)
            {
                throw new InvalidOperationException(
                    "Player requires Character, CharacterFeatureSet and Animator references.");
            }

            player.CharacterFeatures.ValidateConfiguration();

            builder.RegisterComponent(character);
            builder.Register<PlayerActorAdapter>(Lifetime.Scoped)
                .As<IPlayerActorTransform>()
                .As<IPlayerTargetingActor>()
                .As<IPlayerCombatActor>()
                .As<IRootMotionPolicyTarget>()
                .As<IDamageImmunityTarget>()
                .As<IPlayerTraversalActor>();

            builder.Register(
                    _ => new MecanimCharacterAnimationDriver(
                        player.PlayerAnimator,
                        PlayerAnimationStates.MecanimBindings),
                    Lifetime.Scoped)
                .AsSelf()
                .As<ICharacterAnimationDriver>();
            builder.Register(
                    _ => new MecanimLookAnimationDriver(
                        player.PlayerAnimator,
                        "HeadLookX",
                        "HeadLookY"),
                    Lifetime.Scoped)
                .As<ICharacterLookAnimationDriver>();
            builder.Register<PlayerCharacterAnimationPresenter>(Lifetime.Scoped);
            builder.Register(
                resolver => new CharacterMovementPolicies(
                    character,
                    player.CharacterFeatures),
                Lifetime.Scoped)
                .As<ICharacterMovementPolicyLifecycle>();
            builder.RegisterPlayerTargeting();
            builder.RegisterPlayerCamera(player);
            builder.RegisterPlayerCombat(player);
            builder.RegisterPlayerLocomotion(player.CharacterFeatures);
            builder.RegisterPlayerTraversal(player.CharacterFeatures);
            builder.RegisterComponent(player);
        }
    }
}
