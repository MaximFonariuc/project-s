using UnityEngine;

namespace ModularCharacterMovement
{
    internal static class CharacterMovementMath
    {
        private const float ZeroLengthSqrThreshold = 9.99999943962493E-11f;
        private const float MagnitudeErrorTolerance = 1.01f;

        public static float Dot(this Vector3 vector, Vector3 otherVector)
        {
            return Vector3.Dot(vector, otherVector);
        }

        public static Vector3 ProjectedOn(this Vector3 vector, Vector3 normal)
        {
            return Vector3.Project(vector, normal);
        }

        public static Vector3 ProjectedOnPlane(this Vector3 vector, Vector3 planeNormal)
        {
            return Vector3.ProjectOnPlane(vector, planeNormal);
        }

        public static Vector3 PerpendicularTo(this Vector3 vector, Vector3 otherVector)
        {
            return Vector3.Cross(vector, otherVector).normalized;
        }

        public static Vector3 TangentTo(this Vector3 vector, Vector3 normal, Vector3 up)
        {
            Vector3 right = vector.PerpendicularTo(up);
            Vector3 tangent = normal.PerpendicularTo(right);

            return tangent * vector.magnitude;
        }

        public static Vector3 ClampedTo(this Vector3 vector, float maxLength)
        {
            return Vector3.ClampMagnitude(vector, maxLength);
        }

        public static bool IsZero(this Vector3 vector)
        {
            return vector.sqrMagnitude < ZeroLengthSqrThreshold;
        }

        public static bool IsExceeding(this Vector3 vector, float magnitude)
        {
            return vector.sqrMagnitude > magnitude * magnitude * MagnitudeErrorTolerance;
        }

        public static float Square(this float value)
        {
            return value * value;
        }
    }
}
