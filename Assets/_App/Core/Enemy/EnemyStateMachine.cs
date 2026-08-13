namespace _App
{
    public class EnemyStateMachine
    {
        public EnemyBaseState CurrentState { get; private set; }

        public void Initialize(EnemyBaseState startState)
        {
            CurrentState = startState;
            CurrentState.Enter();
        }

        public void ChangeState(EnemyBaseState newState)
        {
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState?.Enter();
        }
    }
}
