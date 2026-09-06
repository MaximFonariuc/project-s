#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed partial class CharacterMovement
    {
        private static void DrawDisc(Vector3 pos, Quaternion rot, float radius, Color color = default,
            bool solid = true)
        {
            if (color != default)
                Handles.color = color;

            Matrix4x4 mtx = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);

            using (new Handles.DrawingScope(mtx))
            {
                if (solid)
                    Handles.DrawSolidDisc(Vector3.zero, Vector3.up, radius);
                else
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
            }
        }


        private void OnDrawGizmosSelected()
        {
            float skinRadius = _radius;
            Vector3 footPosition = GetFootPosition();

            Gizmos.color = new Color(0.569f, 0.957f, 0.545f, 0.5f);
            Gizmos.DrawLine(footPosition + Vector3.left * skinRadius, footPosition + Vector3.right * skinRadius);
            Gizmos.DrawLine(footPosition + Vector3.back * skinRadius, footPosition + Vector3.forward * skinRadius);

            if (PerchOffset > 0.0f && PerchOffset < Radius)
            {
                DrawDisc(footPosition, Rotation, _perchOffset, new Color(0.569f, 0.957f, 0.545f, 0.15f));
                DrawDisc(footPosition, Rotation, _perchOffset, new Color(0.569f, 0.957f, 0.545f, 0.75f), false);
            }

            if (StepOffset > 0.0f)
            {
                DrawDisc(footPosition + Transform.up * StepOffset, Rotation, Radius * 1.15f,
                    new Color(0.569f, 0.957f, 0.545f, 0.75f), false);
            }
        }

    }
}
#endif
