using UnityEngine;

namespace _App
{
    public sealed class EnemyDeadState : EnemyBaseState
    {
        public EnemyDeadState(EnemyController controller)
            : base(controller) { }

        public override void Enter()
        {
            Controller.TargetLockSystem.ClearTarget();
            Controller.Character.SetMovementDirection(Vector3.zero);
        }

        public override void Tick() { }

        public override void Exit() { }
    }
}
