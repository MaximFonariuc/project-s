using UnityEngine;

namespace _App
{
    public class CharacterTwinStick : Character
    {
        private Vector3 _aimDirection;

        protected virtual Vector3 GetAimDirection()
        {
            return _aimDirection;
        }

        protected virtual void SetAimDirection(Vector3 worldDirection)
        {
            _aimDirection = worldDirection;
        }

        protected override void CustomRotationMode(float deltaTime)
        {
            base.CustomRotationMode(deltaTime);
            
            Vector3 targetDirection = _aimDirection.IsZero() ? GetMovementDirection() : GetAimDirection();

            RotateTowards(targetDirection, deltaTime);
        }

        private void Update()
        {
            Vector3 aimDirection = Vector3.zero;

            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hitResult, Mathf.Infinity))
                {
                    Vector3 toHitPoint2D = (hitResult.point - GetPosition()).OnlyXZ();
                    aimDirection = toHitPoint2D.normalized;
                }
            }
            
            SetAimDirection(aimDirection);
        }
    }
}
