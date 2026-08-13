using UnityEngine;

namespace _App
{
    public class EnemyReturnState : EnemyBaseState
    {
        private readonly Vector3 _homePosition;
        private readonly Quaternion _homeRotation;
        
        private const float STOP_DISTANCE = 0.01f;
        private const float ROTATION_SPEED = 5f;

        public EnemyReturnState(EnemyController controller, Vector3 homePosition, Quaternion homeRotation) : base(controller)
        {
            _homePosition = homePosition;
            _homeRotation = homeRotation;
        }

        public override void Enter()
        {
            Controller.TargetLockSystem.ClearTarget();
        }

        public override void Tick()
        {
            Vector3 toHome = _homePosition - Controller.Character.transform.position;
            float distance = toHome.magnitude;

            if (distance <= STOP_DISTANCE)
            {
                Controller.Character.transform.rotation = Quaternion.Lerp(
                    Controller.Character.transform.rotation,
                    _homeRotation,
                    Time.deltaTime * ROTATION_SPEED
                );

                if (Quaternion.Angle(Controller.Character.transform.rotation, _homeRotation) < 1f)
                {
                    Controller.SwitchState(new EnemyIdleState(Controller));
                }

                Controller.Character.SetMovementDirection(Vector3.zero);
                return;
            }

            Controller.Character.SetMovementDirection(toHome.normalized);
        }

        public override void Exit()
        {
            Controller.Character.SetMovementDirection(Vector3.zero);
        }
    }
}