using UnityEngine;

namespace _App
{
    public class EnemyChaseState : EnemyBaseState
    {
        private Transform _target;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        private const float MAX_CHASE_DISTANCE = 15f;

        public EnemyChaseState(EnemyController controller) : base(controller) { }


        public override void Enter()
        {
            _target = Controller.TargetLockSystem.CurrentTarget;
            _startPosition = Controller.StartPosition;
            _startRotation = Controller.StartRotation;
        }

        public override void Tick()
        {
            if (_target == null)
            {
                Controller.SwitchState(new EnemyIdleState(Controller));
                return;
            }

            Vector3 toTarget = _target.position - Controller.Character.transform.position;

            float distance = toTarget.magnitude;
			
            if (distance > MAX_CHASE_DISTANCE)
            {
                Controller.SwitchState(new EnemyReturnState(Controller, _startPosition, _startRotation));
                return;
            }
            var attacking = Controller.CombatController.IsComboAttack || Controller.CombatController.IsSkillAttack;
            
            Controller.Character.SetMovementDirection(attacking ? Vector3.zero : toTarget.normalized);

            if (distance <= ATTACK_DISTANCE)
            {
                Controller.SwitchState(new EnemyAttackState(Controller));
            }
        }

        public override void Exit()
        {
            Controller.Character.SetMovementDirection(Vector3.zero);
        }
    }
}