using System;
using ModularStateMachine;
using VContainer;

namespace _App
{
    internal static class PlayerReactionComposition
    {
        public static void RegisterPlayerReaction(
            this IContainerBuilder builder,
            ThirdPersonController player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            CharacterReactionProfile profile = player.ReactionProfile;
            CharacterReactionSettings settings = profile != null
                ? profile.CreateSettings()
                : CharacterReactionSettings.CreateDefaults();

            builder.RegisterInstance(settings);
            builder.Register<CharacterPoise>(Lifetime.Scoped);
            builder.Register<ICharacterReactionPresentation>(
                resolver => new CharacterReactionAnimationAdapter(
                    resolver.Resolve<PlayerAnimationCoordinator>()),
                Lifetime.Scoped);
            builder.Register<IStateMachine<CharacterReactionStateId>>(
                _ => CharacterReactionStateMachineFactory.Create(),
                Lifetime.Scoped);

            builder.Register<CharacterReactionController>(Lifetime.Scoped)
                .AsSelf()
                .As<ICharacterReactionState>();
        }
    }
}
