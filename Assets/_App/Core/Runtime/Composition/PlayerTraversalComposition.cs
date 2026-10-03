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

            builder.Register(
                resolver => new PlayerTraversalActions(
                    resolver.Resolve<Character>(),
                    resolver.Resolve<PlayerAnimationCoordinator>(),
                    resolver.Resolve<IPlayerLocomotionState>(),
                    resolver.Resolve<IPlayerLadderClimbAnimation>(),
                    features),
                Lifetime.Scoped)
                .As<IPlayerTraversalActions>()
                .As<IPlayerLadderGrip>();
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
