using System;
using VContainer;

namespace _App
{
    internal static class PlayerDefenseComposition
    {
        public static void RegisterPlayerDefense(
            this IContainerBuilder builder,
            ThirdPersonController player,
            BodyColliderRig bodyRig)
        {
            PlayerDefenseProfile profile = player.DefenseProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a defense profile.");
            PlayerDefenseSettings settings = profile.CreateSettings();
            builder.RegisterInstance(settings);
            builder.Register(
                    resolver => new PlayerDefenseController(
                        resolver.Resolve<IPlayerInput>(),
                        resolver.Resolve<IPlayerDefenseActor>(),
                        resolver.Resolve<IPlayerLocomotionEvents>(),
                        resolver.Resolve<Health>(),
                        bodyRig,
                        resolver.Resolve<ICombatClock>(),
                        settings),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerDefenseState>();
        }
    }
}
