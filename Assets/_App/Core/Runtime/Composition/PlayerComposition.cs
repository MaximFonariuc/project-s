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
                player.AnimationProfile == null)
            {
                throw new InvalidOperationException(
                    "Player requires Character, Health, CharacterFeatureSet, Animator and animation profile references.");
            }

            player.CharacterFeatures.ValidateConfiguration();

            builder.RegisterComponent(character);
            builder.RegisterComponent(health);
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
                        player.AnimationProfile.CreateBindings()),
                    Lifetime.Scoped)
                .AsSelf()
                .As<ICharacterAnimationDriver>()
                .As<IAnimationClipActionDriver>();
            builder.Register(
                    _ => player.AnimationProfile.CreateStateCatalog(player.PlayerAnimator),
                    Lifetime.Scoped)
                .As<IAnimationStateCatalog>();
            builder.Register<PlayerAnimationCoordinator>(Lifetime.Scoped);
            builder.Register(
                    resolver => new MecanimLookAnimationDriver(
                        resolver.Resolve<MecanimCharacterAnimationDriver>(),
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
            builder.RegisterPlayerHitStop();
            builder.RegisterPlayerLocomotion(player.CharacterFeatures);
            builder.RegisterPlayerReaction(player);
            builder.RegisterPlayerTraversal(player.CharacterFeatures);
            builder.Register<PlayerActorController>(Lifetime.Scoped);
            builder.RegisterComponent(player);
        }
    }
}
