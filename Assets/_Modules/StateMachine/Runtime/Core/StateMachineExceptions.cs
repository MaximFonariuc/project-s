using System;

namespace ModularStateMachine
{
    public class StateMachineException : InvalidOperationException
    {
        public StateMachineException(string message)
            : base(message) { }

        public StateMachineException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class StateNotRegisteredException : StateMachineException
    {
        public StateNotRegisteredException(Type stateType)
            : base($"State '{stateType.FullName}' is not registered in this state machine.") { }
    }

    public sealed class StateTransitionNotAllowedException : StateMachineException
    {
        public StateTransitionNotAllowedException(object from, object to)
            : base($"Transition from '{from}' to '{to}' is not allowed.") { }
    }

    public sealed class StateTransitionGuardException : StateMachineException
    {
        public StateTransitionGuardException(object from, object to)
            : base($"Transition from '{from}' to '{to}' was rejected by a guard.") { }
    }

    public sealed class StateMachineReentrancyException : StateMachineException
    {
        public StateMachineReentrancyException()
            : base("A state transition cannot be requested while another transition is in progress.") { }
    }
}
