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
                player.AbilitiesData == null ||
                player.PlayerAnimator == null)
            {
                throw new InvalidOperationException(
                    "Player requires Character, AbilitiesData and Animator references.");
            }

            builder.RegisterComponent(character);
            builder.Register<PlayerActorAdapter>(Lifetime.Scoped)
                .As<IPlayerActorTransform>()
                .As<IPlayerTargetingActor>()
                .As<IPlayerCombatActor>();

            builder.Register(
                _ => new AnimationController(character, player.PlayerAnimator),
                Lifetime.Scoped);
            builder.Register(
                resolver => new AbilityController(
                    player.AbilitiesData,
                    character,
                    resolver.Resolve<AnimationController>()),
                Lifetime.Scoped);

            builder.RegisterPlayerTargeting();
            builder.RegisterPlayerCamera(player);
            builder.RegisterPlayerCombat(player);
            builder.RegisterPlayerLocomotion();
            builder.RegisterComponent(player);
        }
    }
}
