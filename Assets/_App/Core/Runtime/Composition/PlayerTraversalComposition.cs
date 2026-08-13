using System;
using VContainer;

namespace _App
{
    internal static class PlayerTraversalComposition
    {
        public static void RegisterPlayerTraversal(
            this IContainerBuilder builder,
            CharacterFeatureSet features)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            builder.Register<IPlayerTraversalActions>(
                resolver => new PlayerTraversalActions(
                    resolver.Resolve<Character>(),
                    resolver.Resolve<AnimationController>(),
                    features),
                Lifetime.Scoped);
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
