using UnityEngine;

namespace ModularCharacterMovement
{
    internal sealed class CharacterCapsuleShape
    {
        public float Radius { get; private set; }
        public float Height { get; private set; }

        public Vector3 Center { get; private set; }
        public Vector3 BottomCenter { get; private set; }
        public Vector3 TopCenter { get; private set; }

        public Vector3 TransformedCenter { get; private set; }
        public Vector3 TransformedBottomCenter { get; private set; }
        public Vector3 TransformedTopCenter { get; private set; }

        public void Resize(float radius, float height, CapsuleCollider collider)
        {
            Radius = Mathf.Max(radius, 0.0f);
            Height = Mathf.Max(height, Radius * 2.0f);

            Calculate(Radius, Height, out Vector3 center, out Vector3 bottomCenter, out Vector3 topCenter);

            Center = center;
            BottomCenter = bottomCenter;
            TopCenter = topCenter;

            if (collider == null)
                return;

            collider.radius = Radius;
            collider.height = Height;
            collider.center = Center;
        }

        public void UpdateRotation(Quaternion rotation)
        {
            TransformedCenter = rotation * Center;
            TransformedBottomCenter = rotation * BottomCenter;
            TransformedTopCenter = rotation * TopCenter;
        }

        public static void Calculate(float radius, float height, out Vector3 center, out Vector3 bottomCenter,
            out Vector3 topCenter)
        {
            radius = Mathf.Max(radius, 0.0f);
            height = Mathf.Max(height, radius * 2.0f);

            center = height * 0.5f * Vector3.up;

            float sideHeight = height - radius * 2.0f;
            bottomCenter = center - sideHeight * 0.5f * Vector3.up;
            topCenter = center + sideHeight * 0.5f * Vector3.up;
        }
    }
}
