using System;
using ModularAnimation;
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
            PlayerCombatLoadoutProfile combatLoadout = player.CombatLoadoutProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a combat loadout profile.");
            PlayerCombatMovesetCatalog movesets = combatLoadout.CreateMovesetCatalog();
            if (!movesets.Supports(player.StartingWeapon))
            {
                throw new InvalidOperationException(
                    $"Player combat loadout does not support starting weapon " +
                    $"'{player.StartingWeapon}'.");
            }

            builder.RegisterComponent(character);
            builder.RegisterComponent(health);
            builder.RegisterInstance(movesets);
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
                .As<ModularAnimation.ICharacterAnimationDriver>()
                .As<ICharacterLookAnimationDriver>();
            builder.Register(
                _ => new PlayerAnimatorLoadout(
                    player.PlayerAnimator,
                    movesets),
                Lifetime.Scoped);
            builder.Register<PlayerAnimationCoordinator>(Lifetime.Scoped);
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
