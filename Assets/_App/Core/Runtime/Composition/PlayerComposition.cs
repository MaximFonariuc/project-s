using System;
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
                .As<IPlayerTraversalActor>();

            builder.Register(
                _ => new AnimationController(character, player.PlayerAnimator),
                Lifetime.Scoped);
            builder.Register(
                resolver => new CharacterMovementPolicies(
                    character,
                    resolver.Resolve<AnimationController>(),
                    player.CharacterFeatures),
                Lifetime.Scoped)
                .As<ICharacterMovementPolicyLifecycle>();
            builder.Register<PlayerPhysicsVolumeAnimationAdapter>(Lifetime.Scoped);

            builder.RegisterPlayerTargeting();
            builder.RegisterPlayerCamera(player);
            builder.RegisterPlayerCombat(player);
            builder.RegisterPlayerLocomotion(player.CharacterFeatures);
            builder.RegisterPlayerTraversal(player.CharacterFeatures);
            builder.RegisterComponent(player);
        }
    }
}
