using VContainer;

namespace _App
{
    internal static class PlayerTargetingComposition
    {
        public static void RegisterPlayerTargeting(this IContainerBuilder builder)
        {
            builder.Register<PlayerTargetingController>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerTargetingState>()
                .As<IPlayerTargetingCommands>();
        }
    }
}
