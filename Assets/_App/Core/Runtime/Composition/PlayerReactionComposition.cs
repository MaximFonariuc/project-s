using System;
using ModularStateMachine.VContainerIntegration;
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
                    resolver.Resolve<ModularAnimation.ICharacterAnimationDriver>(),
                    profile),
                Lifetime.Scoped);
            builder.Register<ICharacterReactionInterruption, PlayerCombatReactionInterruption>(
                Lifetime.Scoped);

            builder.RegisterStateMachine<CharacterReactionStateId>(Lifetime.Scoped)
                .State<CharacterStableState>(CharacterReactionStateId.Stable, Lifetime.Scoped)
                .State<CharacterHitStunState>(CharacterReactionStateId.HitStun, Lifetime.Scoped)
                .State<CharacterKnockdownState>(CharacterReactionStateId.Knockdown, Lifetime.Scoped)
                .State<CharacterAirborneReactionState>(CharacterReactionStateId.Airborne, Lifetime.Scoped)
                .State<CharacterDeadState>(CharacterReactionStateId.Dead, Lifetime.Scoped)
                .Transition(CharacterReactionStateId.Stable, CharacterReactionStateId.HitStun)
                .Transition(CharacterReactionStateId.Stable, CharacterReactionStateId.Knockdown)
                .Transition(CharacterReactionStateId.Stable, CharacterReactionStateId.Airborne)
                .Transition(CharacterReactionStateId.Stable, CharacterReactionStateId.Dead)
                .Transition(CharacterReactionStateId.HitStun, CharacterReactionStateId.Stable)
                .Transition(CharacterReactionStateId.HitStun, CharacterReactionStateId.HitStun)
                .Transition(CharacterReactionStateId.HitStun, CharacterReactionStateId.Knockdown)
                .Transition(CharacterReactionStateId.HitStun, CharacterReactionStateId.Airborne)
                .Transition(CharacterReactionStateId.HitStun, CharacterReactionStateId.Dead)
                .Transition(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Stable)
                .Transition(CharacterReactionStateId.Knockdown, CharacterReactionStateId.HitStun)
                .Transition(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Knockdown)
                .Transition(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Airborne)
                .Transition(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Dead)
                .Transition(CharacterReactionStateId.Airborne, CharacterReactionStateId.Stable)
                .Transition(CharacterReactionStateId.Airborne, CharacterReactionStateId.HitStun)
                .Transition(CharacterReactionStateId.Airborne, CharacterReactionStateId.Knockdown)
                .Transition(CharacterReactionStateId.Airborne, CharacterReactionStateId.Airborne)
                .Transition(CharacterReactionStateId.Airborne, CharacterReactionStateId.Dead)
                .Transition(CharacterReactionStateId.Dead, CharacterReactionStateId.Stable);

            builder.Register<CharacterReactionController>(Lifetime.Scoped)
                .AsSelf()
                .As<ICharacterReactionState>();
        }
    }
}
