using System;
using UnityEngine;

namespace _App
{
    public sealed class CameraRelativeMovementReference : IPlayerMovementReference
    {
        private readonly ICameraOrientation _camera;

        public CameraRelativeMovementReference(ICameraOrientation camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public Vector3 ToWorldDirection(Vector2 input, Vector3 up, bool cameraRelative)
        {
            Vector3 localDirection = new Vector3(input.x, 0.0f, cameraRelative ? input.y : 0.0f);
            if (!cameraRelative)
            {
                return Vector3.ClampMagnitude(localDirection, 1.0f);
            }

            Vector3 forward = Vector3.ProjectOnPlane(_camera.Forward, up).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(_camera.Right, up).normalized;
            }

            Vector3 right = Vector3.Cross(up, forward).normalized;
            return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1.0f);
        }
    }
}
