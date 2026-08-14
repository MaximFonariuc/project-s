using VContainer;

namespace _App
{
    internal static class PlayerHitStopComposition
    {
        public static void RegisterPlayerHitStop(this IContainerBuilder builder)
        {
            builder.Register<ITimeScaleBackend, UnityTimeScaleBackend>(Lifetime.Scoped);
            builder.Register<GameplayTimeScaleController>(Lifetime.Scoped)
                .AsSelf()
                .As<IGameplayTimeScaleRequests>()
                .As<IGameplayTimeScaleState>();
            builder.Register<CombatHitStopController>(Lifetime.Scoped);
        }
    }
}
