using VContainer;

namespace _App
{
    internal static class PlayerTraversalComposition
    {
        public static void RegisterPlayerTraversal(this IContainerBuilder builder)
        {
            builder.Register<IPlayerTraversalActions, PlayerTraversalActionAdapter>(Lifetime.Scoped);
            builder.Register<IPlayerInteractionProbe, CharacterInteractionProbe>(Lifetime.Scoped);
            builder.Register<PlayerSimulationGate>(Lifetime.Scoped)
                .As<IPlayerSimulationPause>()
                .As<IPlayerSimulationLock>();
            builder.Register<PlayerTraversalController>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerTraversalCommands>();
            builder.Register<PlayerSimulationPauseController>(Lifetime.Scoped);
        }
    }
}
