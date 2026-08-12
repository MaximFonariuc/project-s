using UnityEngine;

namespace _App
{
    public class CharacterWithoutGravity : Character
    {
        [Space(15f),SerializeField] private Transform _planetTransform;
        
        protected override void UpdateRotation(float deltaTime)
        {
            base.UpdateRotation(deltaTime);
            
            Vector3 toPlanet = _planetTransform.position - GetPosition();
            SetGravityVector(toPlanet.normalized * GetGravityMagnitude());
            
            Vector3 worldUp = GetGravityDirection() * -1.0f;
            Quaternion newRotation = Quaternion.FromToRotation(GetUpVector(), worldUp) * GetRotation();
            
            SetRotation(newRotation);
        }

        private void Update()
        {
            Vector2 movementInput = new Vector2
                { x = Input.GetAxisRaw("Horizontal"), y = Input.GetAxisRaw("Vertical") };

            Vector3 movementDirection = Vector3.zero;

            movementDirection += Vector3.right * movementInput.x;
            movementDirection += Vector3.forward * movementInput.y;

            movementDirection = movementDirection.relativeTo(CameraTransform, GetUpVector());

            SetMovementDirection(movementDirection);
        }
    }
}
