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
                !player.TryGetComponent(out Health health) ||
                player.CharacterFeatures == null ||
                player.PlayerAnimator == null ||
                player.PlayerAnimator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException(
                    "Player requires Character, Health, CharacterFeatureSet and a configured Animator Controller.");
            }

            player.CharacterFeatures.ValidateConfiguration();

            builder.RegisterComponent(character);
            builder.RegisterComponent(health);
            builder.Register<PlayerActorAdapter>(Lifetime.Scoped)
                .As<IPlayerActorTransform>()
                .As<IPlayerTargetingActor>()
                .As<IPlayerCombatActor>()
                .As<IDamageImmunityTarget>()
                .As<IPlayerTraversalActor>();

            builder.Register(
                    _ => new XControllerCharacterAnimationDriver(player.PlayerAnimator),
                    Lifetime.Scoped)
                .AsSelf()
                .As<ModularAnimation.ICharacterAnimationDriver>();
            builder.Register<PlayerAnimationCoordinator>(Lifetime.Scoped);
            builder.RegisterInstance(NullCharacterLookAnimationDriver.Instance)
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
            builder.RegisterPlayerHitStop();
            builder.RegisterPlayerLocomotion(player.CharacterFeatures);
            builder.RegisterPlayerReaction(player);
            builder.RegisterPlayerTraversal(player.CharacterFeatures);
            builder.Register<PlayerActorController>(Lifetime.Scoped);
            builder.RegisterComponent(player);
        }
    }
}
