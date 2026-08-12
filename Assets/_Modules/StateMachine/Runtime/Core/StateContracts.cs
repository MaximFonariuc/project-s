using System;

namespace ModularStateMachine
{
    public interface IStateNode
    {
        void Exit();
    }

    public interface IState : IStateNode
    {
        void Enter();
    }

    public interface IState<in TContext> : IStateNode
    {
        void Enter(TContext context);
    }

    public interface IUpdateState
    {
        void Tick(float deltaTime);
    }

    public interface IFixedUpdateState
    {
        void FixedTick(float fixedDeltaTime);
    }

    public interface IStateTransitionGuard
    {
        bool CanTransition { get; }
    }

    public interface IStateMachine<TStateId>
        where TStateId : struct, Enum
    {
        event Action<TStateId, TStateId> StateChanged;

        bool IsRunning { get; }
        bool IsTransitioning { get; }
        TStateId? CurrentStateId { get; }
        IStateNode CurrentState { get; }

        void Start<TTargetState>()
            where TTargetState : class, IState;

        void Start<TTargetState, TContext>(TContext context)
            where TTargetState : class, IState<TContext>;

        void ChangeState<TTargetState>()
            where TTargetState : class, IState;

        void ChangeState<TTargetState, TContext>(TContext context)
            where TTargetState : class, IState<TContext>;

        bool CanChangeState<TTargetState>()
            where TTargetState : class, IStateNode;

        bool CanChangeState(TStateId targetStateId);
        void Tick(float deltaTime);
        void FixedTick(float fixedDeltaTime);
        void Stop();
    }
}
