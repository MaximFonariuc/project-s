using System;

namespace ModularStateMachine
{
    public readonly struct StateRegistration<TStateId>
        where TStateId : struct, Enum
    {
        public TStateId StateId { get; }
        public Type StateType { get; }
        public IStateNode State { get; }

        public StateRegistration(TStateId stateId, Type stateType, IStateNode state)
        {
            StateId = stateId;
            StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
            State = state ?? throw new ArgumentNullException(nameof(state));

            if (!stateType.IsInstanceOfType(state))
            {
                throw new ArgumentException(
                    $"State instance '{state.GetType().FullName}' is not assignable to '{stateType.FullName}'.",
                    nameof(state));
            }
        }

        public static StateRegistration<TStateId> Create<TState>(TStateId stateId, TState state)
            where TState : class, IStateNode
        {
            return new StateRegistration<TStateId>(stateId, typeof(TState), state);
        }
    }

    public readonly struct StateTransition<TStateId>
        where TStateId : struct, Enum
    {
        public TStateId From { get; }
        public TStateId To { get; }
        public IStateTransitionGuard Guard { get; }

        public StateTransition(TStateId from, TStateId to, IStateTransitionGuard guard = null)
        {
            From = from;
            To = to;
            Guard = guard;
        }
    }
}
