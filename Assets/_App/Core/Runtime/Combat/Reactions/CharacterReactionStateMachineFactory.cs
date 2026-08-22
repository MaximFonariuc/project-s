using ModularStateMachine;

namespace _App
{
    internal static class CharacterReactionStateMachineFactory
    {
        private static readonly StateTransition<CharacterReactionStateId>[] Transitions =
        {
            new(CharacterReactionStateId.Stable, CharacterReactionStateId.HitStun),
            new(CharacterReactionStateId.Stable, CharacterReactionStateId.Knockdown),
            new(CharacterReactionStateId.Stable, CharacterReactionStateId.Airborne),
            new(CharacterReactionStateId.Stable, CharacterReactionStateId.Dead),
            new(CharacterReactionStateId.HitStun, CharacterReactionStateId.Stable),
            new(CharacterReactionStateId.HitStun, CharacterReactionStateId.HitStun),
            new(CharacterReactionStateId.HitStun, CharacterReactionStateId.Knockdown),
            new(CharacterReactionStateId.HitStun, CharacterReactionStateId.Airborne),
            new(CharacterReactionStateId.HitStun, CharacterReactionStateId.Dead),
            new(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Stable),
            new(CharacterReactionStateId.Knockdown, CharacterReactionStateId.HitStun),
            new(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Knockdown),
            new(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Airborne),
            new(CharacterReactionStateId.Knockdown, CharacterReactionStateId.Dead),
            new(CharacterReactionStateId.Airborne, CharacterReactionStateId.Stable),
            new(CharacterReactionStateId.Airborne, CharacterReactionStateId.HitStun),
            new(CharacterReactionStateId.Airborne, CharacterReactionStateId.Knockdown),
            new(CharacterReactionStateId.Airborne, CharacterReactionStateId.Airborne),
            new(CharacterReactionStateId.Airborne, CharacterReactionStateId.Dead),
            new(CharacterReactionStateId.Dead, CharacterReactionStateId.Stable)
        };

        public static IStateMachine<CharacterReactionStateId> Create()
        {
            var states = new[]
            {
                StateRegistration<CharacterReactionStateId>.Create(
                    CharacterReactionStateId.Stable,
                    new CharacterStableState()),
                StateRegistration<CharacterReactionStateId>.Create(
                    CharacterReactionStateId.HitStun,
                    new CharacterHitStunState()),
                StateRegistration<CharacterReactionStateId>.Create(
                    CharacterReactionStateId.Knockdown,
                    new CharacterKnockdownState()),
                StateRegistration<CharacterReactionStateId>.Create(
                    CharacterReactionStateId.Airborne,
                    new CharacterAirborneReactionState()),
                StateRegistration<CharacterReactionStateId>.Create(
                    CharacterReactionStateId.Dead,
                    new CharacterDeadState())
            };

            return new StateMachine<CharacterReactionStateId>(states, Transitions);
        }
    }
}
