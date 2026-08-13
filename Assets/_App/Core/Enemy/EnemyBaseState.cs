using UnityEngine;

namespace _App
{
    public abstract class EnemyBaseState
    {
        protected const float ATTACK_DISTANCE = 1f;
        protected readonly EnemyController Controller;
        
        protected EnemyBaseState(EnemyController controller)
        {
            Controller = controller;
        }

        public abstract void Enter();
        public abstract void Tick();
        public abstract void Exit();
    }
}