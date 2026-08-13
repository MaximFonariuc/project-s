using System;
using System.Collections.Generic;
using VContainer;

namespace ModularStateMachine.VContainerIntegration
{
    public static class StateMachineRegistrationExtensions
    {
        public static StateMachineRegistrationBuilder<TStateId> RegisterStateMachine<TStateId>(
            this IContainerBuilder builder,
            Lifetime lifetime)
            where TStateId : struct, Enum
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return new StateMachineRegistrationBuilder<TStateId>(builder, lifetime);
        }
    }

    public sealed class StateMachineRegistrationBuilder<TStateId>
        where TStateId : struct, Enum
    {
        private readonly IContainerBuilder _containerBuilder;
        private readonly List<Func<IObjectResolver, StateRegistration<TStateId>>> _states =
            new List<Func<IObjectResolver, StateRegistration<TStateId>>>();
        private readonly List<Func<IObjectResolver, StateTransition<TStateId>>> _transitions =
            new List<Func<IObjectResolver, StateTransition<TStateId>>>();

        internal StateMachineRegistrationBuilder(IContainerBuilder containerBuilder, Lifetime lifetime)
        {
            _containerBuilder = containerBuilder;
            _containerBuilder.Register(
                    resolver => BuildStateMachine(resolver),
                    lifetime)
                .AsSelf()
                .As<IStateMachine<TStateId>>();
        }

        public StateMachineRegistrationBuilder<TStateId> State<TState>(
            TStateId stateId,
            Lifetime lifetime)
            where TState : class, IStateNode
        {
            _containerBuilder.Register<TState>(lifetime);
            _states.Add(resolver => StateRegistration<TStateId>.Create(stateId, resolver.Resolve<TState>()));
            return this;
        }

        public StateMachineRegistrationBuilder<TStateId> StateInstance<TState>(
            TStateId stateId,
            TState state)
            where TState : class, IStateNode
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _containerBuilder.RegisterInstance(state);
            _states.Add(_ => StateRegistration<TStateId>.Create(stateId, state));
            return this;
        }

        public StateMachineRegistrationBuilder<TStateId> Transition(TStateId from, TStateId to)
        {
            _transitions.Add(_ => new StateTransition<TStateId>(from, to));
            return this;
        }

        public StateMachineRegistrationBuilder<TStateId> GuardedTransition<TGuard>(
            TStateId from,
            TStateId to)
            where TGuard : class, IStateTransitionGuard
        {
            _transitions.Add(
                resolver => new StateTransition<TStateId>(from, to, resolver.Resolve<TGuard>()));
            return this;
        }

        private StateMachine<TStateId> BuildStateMachine(IObjectResolver resolver)
        {
            var states = new StateRegistration<TStateId>[_states.Count];
            for (int i = 0; i < _states.Count; i++)
            {
                states[i] = _states[i](resolver);
            }

            var transitions = new StateTransition<TStateId>[_transitions.Count];
            for (int i = 0; i < _transitions.Count; i++)
            {
                transitions[i] = _transitions[i](resolver);
            }

            return new StateMachine<TStateId>(states, transitions);
        }
    }
}
