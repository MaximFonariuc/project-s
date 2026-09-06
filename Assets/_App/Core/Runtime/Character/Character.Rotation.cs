using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public partial class Character
    {
        public virtual Vector3 GetUpVector()
        {
            return Transform.up;
        }

        public virtual Vector3 GetRightVector()
        {
            return Transform.right;
        }

        public virtual Vector3 GetForwardVector()
        {
            return Transform.forward;
        }

        protected virtual void RotateTowards(Vector3 worldDirection, float deltaTime, bool updateYawOnly = true, float maxTurnAngle = 0f)
        {
            Vector3 characterUp = GetUpVector();

            if (updateYawOnly)
                worldDirection = Vector3.ProjectOnPlane(worldDirection, characterUp);

            if (worldDirection == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection, characterUp);
            CharacterMovement.Rotation = Quaternion.RotateTowards(Rotation, targetRotation, RotationRate * deltaTime);
        }

        protected virtual void HandleRotationToViewAndMovement(float deltaTime)
        {
            if (MovementMode == EMovementMode.Custom && CustomMovement == (int)ECustomMovementMode.Dodging)
            {
                return;
            }

            Vector3 characterUp = GetUpVector();
            bool shouldRemainVertical = IsWalking() || IsFalling();

            if (_movementDirection == Vector3.zero)
            {
                RotateTowards(CameraTransform.forward, deltaTime, shouldRemainVertical);
                return;
            }

            Vector3 movementDirection = Vector3.ProjectOnPlane(_movementDirection, characterUp).normalized;
            Vector3 cameraDirection = Vector3.ProjectOnPlane(CameraTransform.forward, characterUp).normalized;

            float angleBetween = Vector3.Angle(movementDirection, cameraDirection);

            if (angleBetween > 75f && angleBetween < 105f)
            {
                RotateTowards(movementDirection, deltaTime, shouldRemainVertical);
                return;
            }

            if (angleBetween > 105f)
            {
                Vector3 combinedDirection = Vector3.Slerp(cameraDirection, -movementDirection, 0.3f);
                RotateTowards(combinedDirection, deltaTime, shouldRemainVertical);
                return;
            }

            RotateTowards(movementDirection, deltaTime, shouldRemainVertical);
        }

        public void SetYaw(float value)
        {
            CharacterMovement.Rotation = Quaternion.Euler(0.0f, value, 0.0f);
        }

        public virtual void AddYawInput(float value)
        {
            _rotationInput.y += value;
        }

        protected virtual void AddPitchInput(float value)
        {
            _rotationInput.x += value;
        }

        protected virtual void AddRollInput(float value)
        {
            _rotationInput.z += value;
        }

        protected virtual void ConsumeRotationInput()
        {
            if (_rotationInput != Vector3.zero)
            {
                CharacterMovement.Rotation *= Quaternion.Euler(_rotationInput);

                _rotationInput = Vector3.zero;
            }
        }

        public ERotationMode GetRotationMode()
        {
            return _rotationMode;
        }

        public void SetRotationMode(ERotationMode rotationMode)
        {
            _rotationMode = rotationMode;
        }

        protected virtual void UpdateRotation(float deltaTime)
        {
            switch (_rotationMode)
            {
                case ERotationMode.None:
                    break;

                case ERotationMode.OrientRotationToMovement:
                {
                    bool shouldRemainVertical = IsWalking() || IsFalling();
                    RotateTowards(_movementDirection, deltaTime, shouldRemainVertical);
                    break;
                }

                case ERotationMode.OrientRotationToViewDirection when Camera != null:
                {
                    bool shouldRemainVertical = IsWalking() || IsFalling();
                    RotateTowards(CameraTransform.forward, deltaTime, shouldRemainVertical);
                    break;
                }

                case ERotationMode.OrientToViewAndMovement when Camera != null:
                {
                    HandleRotationToViewAndMovement(deltaTime);
                    break;
                }

                case ERotationMode.Custom:
                    CustomRotationMode(deltaTime);
                    break;
            }
        }


        protected virtual void CustomRotationMode(float deltaTime)
        {
            OnCustomRotationMode(deltaTime);
        }

    }
}
