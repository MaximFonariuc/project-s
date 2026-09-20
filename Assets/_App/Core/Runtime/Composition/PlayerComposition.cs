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
                !player.TryGetComponent(out BodyColliderRig bodyColliderRig) ||
                player.CharacterFeatures == null ||
                player.PlayerAnimator == null ||
                player.PlayerAnimator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException(
                    "Player requires Character, Health, BodyColliderRig, " +
                    "CharacterFeatureSet and a configured Animator Controller.");
            }

            player.CharacterFeatures.ValidateConfiguration();
            PlayerCombatLoadoutProfile combatLoadout = player.CombatLoadoutProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a combat loadout profile.");
            CombatFeedbackProfile combatFeedback = player.CombatFeedbackProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a combat feedback profile.");
            CombatStyleProfile combatStyle = player.CombatStyleProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a combat style profile.");
            PlayerCombatMovesetCatalog movesets = combatLoadout.CreateMovesetCatalog();
            CombatFeedbackSettings feedbackSettings = combatFeedback.CreateSettings();
            CombatStyleSettings styleSettings = combatStyle.CreateSettings();
            if (!movesets.Supports(player.StartingWeapon))
            {
                throw new InvalidOperationException(
                    $"Player combat loadout does not support starting weapon " +
                    $"'{player.StartingWeapon}'.");
            }

            builder.RegisterComponent(character);
            builder.RegisterComponent(health);
            if (player.PlayerAnimator.TryGetComponent(out LadderGripIkAdapter ladderGripIk))
            {
                builder.RegisterComponent(ladderGripIk);
            }
            builder.RegisterInstance(movesets);
            builder.RegisterInstance(feedbackSettings);
            builder.RegisterInstance(styleSettings);
            builder.Register<PlayerActorAdapter>(Lifetime.Scoped)
                .As<IPlayerActorTransform>()
                .As<IPlayerTargetingActor>()
                .As<IPlayerCombatActor>()
                .As<IPlayerDefenseActor>()
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
            builder.RegisterPlayerCombatFeedback();
            builder.RegisterPlayerCombatStyle();
            builder.RegisterPlayerLocomotion(player.CharacterFeatures);
            builder.RegisterPlayerReaction(player);
            builder.RegisterPlayerDefense(player, bodyColliderRig);
            builder.RegisterPlayerTraversal(player.CharacterFeatures);
            builder.Register<PlayerActorController>(Lifetime.Scoped);
            builder.RegisterComponent(player);
        }
    }
}
