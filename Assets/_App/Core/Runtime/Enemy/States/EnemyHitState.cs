using UnityEngine;

namespace _App
{
    public class EnemyHitState : EnemyBaseState
    {
        private float _hitDuration;
        private float _enterTime;

        public EnemyHitState(EnemyController controller) 
            : base(controller) { }

        public override void Enter()
        {
            Controller.AnimationController.CrossFade(AnimationHashes.HitFront, 0.05f, AnimationController.BASE_LAYER_INDEX);
            _hitDuration = Controller.AnimationController.GetCurrentStateInfo(0).length / 2;
            _enterTime = Time.time;
            Controller.Character.SetMovementDirection(Vector3.zero);
        }

        public override void Tick()
        {
            if (Time.time - _enterTime >= _hitDuration)
            {
                DetermineNextState();
            }
        }

        public override void Exit()
        {
            Controller.Character.SetMovementDirection(Vector3.zero);
        }

        private void DetermineNextState()
        {
            if (Controller.TargetLockSystem.IsTargetLocked)
            {
                Controller.SwitchState(new EnemyChaseState(Controller));
            }
            else
            {
                Controller.SwitchState(new EnemyIdleState(Controller));
            }
        }
    }
}