using ModularStateMachine;

namespace _App
{
    internal sealed class CharacterStableState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }
    }

    internal abstract class CharacterTimedReactionState : IState<CharacterReactionContext>
    {
        public CharacterReactionContext Context { get; private set; }

        public void Enter(CharacterReactionContext context)
        {
            Context = context;
        }

        public void Exit()
        {
            Context = default;
        }
    }

    internal sealed class CharacterHitStunState : CharacterTimedReactionState
    {
    }

    internal sealed class CharacterKnockdownState : CharacterTimedReactionState
    {
    }

    internal sealed class CharacterAirborneReactionState : CharacterTimedReactionState
    {
    }

    internal sealed class CharacterDeadState : IState<CombatHitResult>
    {
        public CombatHitResult LethalHit { get; private set; }

        public void Enter(CombatHitResult lethalHit)
        {
            LethalHit = lethalHit;
        }

        public void Exit()
        {
            LethalHit = default;
        }
    }
}
