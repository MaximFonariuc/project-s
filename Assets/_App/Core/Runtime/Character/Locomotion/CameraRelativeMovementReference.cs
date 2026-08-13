using System;
using UnityEngine;

namespace _App
{
    public sealed class CameraRelativeMovementReference : IPlayerMovementReference
    {
        private readonly Character _character;

        public CameraRelativeMovementReference(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
        }

        public Vector3 ToWorldDirection(Vector2 input, Vector3 up, bool cameraRelative)
        {
            Vector3 localDirection = new Vector3(input.x, 0.0f, cameraRelative ? input.y : 0.0f);
            if (!cameraRelative || _character.CameraTransform == null)
            {
                return Vector3.ClampMagnitude(localDirection, 1.0f);
            }

            return Vector3.ClampMagnitude(
                localDirection.relativeTo(_character.CameraTransform, up),
                1.0f);
        }
    }
}
