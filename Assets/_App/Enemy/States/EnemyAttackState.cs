using UnityEngine;

namespace _App
{
    public class EnemyAttackState : EnemyBaseState
    {
        private Transform _target;

        private const float ROTATION_SPEED = 10f;

        public EnemyAttackState(EnemyController controller) 
            : base(controller) { }

        public override void Enter()
        {
            _target = Controller.TargetLockSystem.CurrentTarget;
            TryAttack();
        }

        public override void Tick()
        {
            if (_target == null)
            {
                Controller.SwitchState(new EnemyIdleState(Controller));
                return;
            }

            Vector3 toTarget = _target.position - Controller.Character.transform.position;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                Controller.Character.transform.rotation = Quaternion.Slerp(
                    Controller.Character.transform.rotation,
                    targetRot,
                    Time.deltaTime * ROTATION_SPEED
                );
            }

            float dist = Vector3.Distance(Controller.Character.GetPosition(), _target.position);

            if (dist > ATTACK_DISTANCE)
            {
                Controller.SwitchState(new EnemyChaseState(Controller));
                return;
            }

            TryAttack();
        }

        public override void Exit() { }

        private void TryAttack()
        {
            if (_target == null)
                return;

            float dist = Vector3.Distance(Controller.Character.GetPosition(), _target.position);
            if (dist <= ATTACK_DISTANCE)
            {
                Controller.CombatController.TryAttack();
            }
            else
            {
                Controller.SwitchState(new EnemyChaseState(Controller));
            }
        }
    }
}