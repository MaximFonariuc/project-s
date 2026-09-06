using System;
using System.Collections.Generic;

namespace ModularStateMachine
{
    public sealed class StateMachine<TStateId> : IStateMachine<TStateId>, IDisposable
        where TStateId : struct, Enum
    {
        private readonly Dictionary<Type, StateRegistration<TStateId>> _statesByType;
        private readonly HashSet<TStateId> _registeredStateIds;
        private readonly Dictionary<TransitionKey, IStateTransitionGuard> _transitions;
        private bool _disposed;

        public event Action<TStateId, TStateId> StateChanged;

        public bool IsRunning { get; private set; }
        public bool IsTransitioning { get; private set; }
        public TStateId? CurrentStateId { get; private set; }
        public IStateNode CurrentState { get; private set; }

        public StateMachine(
            IEnumerable<StateRegistration<TStateId>> states,
            IEnumerable<StateTransition<TStateId>> transitions)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            _statesByType = BuildStateLookup(states, out _registeredStateIds);
            _transitions = BuildTransitionLookup(transitions, _registeredStateIds);
        }

        public void Start<TTargetState>()
            where TTargetState : class, IState
        {
            ThrowIfDisposed();
            EnsureNotRunning();
            StateRegistration<TStateId> registration = GetRegistration<TTargetState>();
            EnterInitial(registration.StateId, (TTargetState)registration.State, static state => state.Enter());
        }

        public void Start<TTargetState, TContext>(TContext context)
            where TTargetState : class, IState<TContext>
        {
            ThrowIfDisposed();
            EnsureNotRunning();
            StateRegistration<TStateId> registration = GetRegistration<TTargetState>();
            EnterInitial(registration.StateId, (TTargetState)registration.State, state => state.Enter(context));
        }

        public void ChangeState<TTargetState>()
            where TTargetState : class, IState
        {
            ThrowIfDisposed();
            StateRegistration<TStateId> registration = GetRegistration<TTargetState>();
            ChangeStateInternal(registration.StateId, (TTargetState)registration.State, static state => state.Enter());
        }

        public void ChangeState<TTargetState, TContext>(TContext context)
            where TTargetState : class, IState<TContext>
        {
            ThrowIfDisposed();
            StateRegistration<TStateId> registration = GetRegistration<TTargetState>();
            ChangeStateInternal(registration.StateId, (TTargetState)registration.State, state => state.Enter(context));
        }

        public bool CanChangeState<TTargetState>()
            where TTargetState : class, IStateNode
        {
            ThrowIfDisposed();
            return CanChangeState(GetRegistration<TTargetState>().StateId);
        }

        public bool CanChangeState(TStateId targetStateId)
        {
            ThrowIfDisposed();

            if (!IsRunning || IsTransitioning)
            {
                return false;
            }

            return _transitions.TryGetValue(new TransitionKey(CurrentStateId.Value, targetStateId),
                       out IStateTransitionGuard guard) &&
                   (guard == null || guard.CanTransition);
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();

            if (IsRunning && !IsTransitioning && CurrentState is IUpdateState state)
            {
                state.Tick(deltaTime);
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            ThrowIfDisposed();

            if (IsRunning && !IsTransitioning && CurrentState is IFixedUpdateState state)
            {
                state.FixedTick(fixedDeltaTime);
            }
        }

        public void Stop()
        {
            ThrowIfDisposed();

            if (!IsRunning)
            {
                return;
            }

            EnsureNotTransitioning();
            IsTransitioning = true;

            try
            {
                CurrentState.Exit();
            }
            catch (Exception exception)
            {
                throw new StateMachineException(
                    $"Exiting state '{CurrentStateId}' failed while stopping the machine.",
                    exception);
            }
            finally
            {
                CurrentState = null;
                CurrentStateId = null;
                IsRunning = false;
                IsTransitioning = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (IsRunning)
                {
                    Stop();
                }
            }
            finally
            {
                StateChanged = null;
                _statesByType.Clear();
                _registeredStateIds.Clear();
                _transitions.Clear();
                _disposed = true;
            }
        }

        private void EnterInitial<TState>(TStateId targetStateId, TState state, Action<TState> enter)
            where TState : class, IStateNode
        {
            EnsureNotTransitioning();
            IsTransitioning = true;

            try
            {
                enter(state);
                CurrentState = state;
                CurrentStateId = targetStateId;
                IsRunning = true;
            }
            catch (Exception exception)
            {
                Exception cleanupException = TryExitFailedEntry(state);
                CurrentState = null;
                CurrentStateId = null;
                IsRunning = false;
                throw new StateMachineException(
                    $"Failed to enter initial state '{targetStateId}'.",
                    Combine(exception, cleanupException));
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        private void ChangeStateInternal<TState>(TStateId targetStateId, TState nextState, Action<TState> enter)
            where TState : class, IStateNode
        {
            ThrowIfDisposed();
            EnsureRunning();
            EnsureNotTransitioning();

            TStateId previousStateId = CurrentStateId.Value;
            TransitionKey key = new TransitionKey(previousStateId, targetStateId);
            if (!_transitions.TryGetValue(key, out IStateTransitionGuard guard))
            {
                throw new StateTransitionNotAllowedException(previousStateId, targetStateId);
            }

            if (guard != null && !guard.CanTransition)
            {
                throw new StateTransitionGuardException(previousStateId, targetStateId);
            }

            IStateNode previousState = CurrentState;
            bool changed = false;
            bool targetEntryStarted = false;
            IsTransitioning = true;

            try
            {
                previousState.Exit();
                CurrentState = null;
                CurrentStateId = null;
                IsRunning = false;
                targetEntryStarted = true;
                enter(nextState);
                CurrentState = nextState;
                CurrentStateId = targetStateId;
                IsRunning = true;
                changed = true;
            }
            catch (Exception exception)
            {
                Exception cleanupException = targetEntryStarted ? TryExitFailedEntry(nextState) : null;
                CurrentState = null;
                CurrentStateId = null;
                IsRunning = false;
                throw new StateMachineException(
                    $"Transition from '{previousStateId}' to '{targetStateId}' failed. The machine was stopped.",
                    Combine(exception, cleanupException));
            }
            finally
            {
                IsTransitioning = false;
            }

            if (changed)
            {
                StateChanged?.Invoke(previousStateId, targetStateId);
            }
        }

        private static Exception TryExitFailedEntry(IStateNode state)
        {
            try
            {
                state.Exit();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static Exception Combine(Exception primary, Exception cleanup)
        {
            return cleanup == null
                ? primary
                : new AggregateException("State entry and cleanup both failed.", primary, cleanup);
        }

        private StateRegistration<TStateId> GetRegistration<TState>()
            where TState : class, IStateNode
        {
            Type stateType = typeof(TState);
            if (!_statesByType.TryGetValue(stateType, out StateRegistration<TStateId> registration))
            {
                throw new StateNotRegisteredException(stateType);
            }

            return registration;
        }

        private static Dictionary<Type, StateRegistration<TStateId>> BuildStateLookup(
            IEnumerable<StateRegistration<TStateId>> registrations,
            out HashSet<TStateId> stateIds)
        {
            var lookup = new Dictionary<Type, StateRegistration<TStateId>>();
            stateIds = new HashSet<TStateId>();

            foreach (StateRegistration<TStateId> registration in registrations)
            {
                if (lookup.ContainsKey(registration.StateType))
                {
                    throw new ArgumentException(
                        $"State type '{registration.StateType.FullName}' is registered more than once.",
                        nameof(registrations));
                }

                if (!stateIds.Add(registration.StateId))
                {
                    throw new ArgumentException(
                        $"State id '{registration.StateId}' is registered more than once.",
                        nameof(registrations));
                }

                lookup.Add(registration.StateType, registration);
            }

            return lookup;
        }

        private static Dictionary<TransitionKey, IStateTransitionGuard> BuildTransitionLookup(
            IEnumerable<StateTransition<TStateId>> transitions,
            HashSet<TStateId> registeredStateIds)
        {
            var lookup = new Dictionary<TransitionKey, IStateTransitionGuard>();

            foreach (StateTransition<TStateId> transition in transitions)
            {
                if (!registeredStateIds.Contains(transition.From) || !registeredStateIds.Contains(transition.To))
                {
                    throw new ArgumentException(
                        $"Transition '{transition.From}' -> '{transition.To}' references an unregistered state id.",
                        nameof(transitions));
                }

                var key = new TransitionKey(transition.From, transition.To);
                if (lookup.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"Transition from '{transition.From}' to '{transition.To}' is registered more than once.",
                        nameof(transitions));
                }

                lookup.Add(key, transition.Guard);
            }

            return lookup;
        }

        private void EnsureRunning()
        {
            if (!IsRunning)
            {
                throw new StateMachineException("The state machine has not been started.");
            }
        }

        private void EnsureNotRunning()
        {
            if (IsRunning)
            {
                throw new StateMachineException("The state machine is already running.");
            }
        }

        private void EnsureNotTransitioning()
        {
            if (IsTransitioning)
            {
                throw new StateMachineReentrancyException();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(StateMachine<TStateId>));
            }
        }

        private readonly struct TransitionKey : IEquatable<TransitionKey>
        {
            private readonly TStateId _from;
            private readonly TStateId _to;

            public TransitionKey(TStateId from, TStateId to)
            {
                _from = from;
                _to = to;
            }

            public bool Equals(TransitionKey other)
            {
                return EqualityComparer<TStateId>.Default.Equals(_from, other._from) &&
                       EqualityComparer<TStateId>.Default.Equals(_to, other._to);
            }

            public override bool Equals(object obj)
            {
                return obj is TransitionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EqualityComparer<TStateId>.Default.GetHashCode(_from) * 397) ^
                           EqualityComparer<TStateId>.Default.GetHashCode(_to);
                }
            }
        }
    }
}
