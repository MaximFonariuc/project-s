using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace _App
{
    public static partial class Extensions
    {
        public static bool IsNullOrEmpty(this IEnumerable source)
        {
            if (source == null)
                return true;

            var enumerator = source.GetEnumerator();
            try
            {
                return !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
        
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            if (source == null)
                return true;

            using var enumerator = source.GetEnumerator();
            return !enumerator.MoveNext();
        }

        public static bool IsEmpty(this string target)
        {
            return string.IsNullOrEmpty(target);
        }

        public static T ToEnum<T>(this string value) where T : Enum
        {
            if (string.IsNullOrEmpty(value)) return (T) Enum.GetValues(typeof(T)).GetValue(0);

            return (T) Enum.Parse(typeof(T), value);
        }
		
        public static string FormatTimeToHours(this int totalSeconds)
        {
            var time = TimeSpan.FromSeconds(totalSeconds);
			
            return (int)time.TotalHours > 0 
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" 
                : FormatTimeToMinutes(totalSeconds);
        }
		
        public static string FormatTimeToMinutes(this int totalSeconds)
        {
            var time = TimeSpan.FromSeconds(totalSeconds);
            return $"{time.Minutes:00}:{time.Seconds:00}";
        }
		
        public static DateTime? ParseDateTime(this string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result))
            {
                return result;
            }
			
            return null;
        }
		
        public static DateTime GetDateFromValue(this string value, string format = "dd.MM.yyyy")
        {
            return DateTime.TryParseExact(value, format,
                null, DateTimeStyles.None, out DateTime result) ? result : DateTime.MinValue;
        }
        
        public static int square(this int value)
        {
            return value * value;
        }
        
        public static float Square(this float value)
        {
            return value * value;
        }

        public static bool isZero(this float value)
        {
            const float tolerance = 0.0000000001f;

            return Mathf.Abs(value) < tolerance;
        }

        public static Vector3 OnlyX(this Vector3 vector3)
        {
            vector3.y = 0.0f;
            vector3.z = 0.0f;

            return vector3;
        }

        public static Vector3 OnlyY(this Vector3 vector3)
        {
            vector3.x = 0.0f;
            vector3.z = 0.0f;

            return vector3;
        }

        public static Vector3 OnlyZ(this Vector3 vector3)
        {
            vector3.x = 0.0f;
            vector3.y = 0.0f;

            return vector3;
        }

        public static Vector3 OnlyXY(this Vector3 vector3)
        {
            vector3.z = 0.0f;

            return vector3;
        }

        public static Vector3 OnlyXZ(this Vector3 vector3)
        {
            vector3.y = 0.0f;

            return vector3;
        }

        public static bool isZero(this Vector2 vector2)
        {
            return vector2.sqrMagnitude < 9.99999943962493E-11;
        }

        public static bool IsZero(this Vector3 vector3)
        {
            return vector3.sqrMagnitude < 9.99999943962493E-11;
        }

        public static bool IsExceeding(this Vector3 vector3, float magnitude)
        {
            const float errorTolerance = 1.01f;

            return vector3.sqrMagnitude > magnitude * magnitude * errorTolerance;
        }

        public static Vector3 Normalized(this Vector3 vector3, out float magnitude)
        {
            magnitude = vector3.magnitude;
            if (magnitude > 9.99999974737875E-06)
                return vector3 / magnitude;

            magnitude = 0.0f;

            return Vector3.zero;
        }     
        
        public static float Dot(this Vector3 vector3, Vector3 otherVector3)
        {
            return Vector3.Dot(vector3, otherVector3);
        }

        public static Vector3 ProjectedOn(this Vector3 thisVector, Vector3 normal)
        {
            return Vector3.Project(thisVector, normal);
        }

        public static Vector3 ProjectedOnPlane(this Vector3 thisVector, Vector3 planeNormal)
        {
            return Vector3.ProjectOnPlane(thisVector, planeNormal);
        }

        public static Vector3 ClampedTo(this Vector3 vector3, float maxLength)
        {
            return Vector3.ClampMagnitude(vector3, maxLength);
        }

        public static Vector3 PerpendicularTo(this Vector3 thisVector, Vector3 otherVector)
        {
            return Vector3.Cross(thisVector, otherVector).normalized;
        }

        public static Vector3 TangentTo(this Vector3 thisVector, Vector3 normal, Vector3 up)
        {
            Vector3 r = thisVector.PerpendicularTo(up);
            Vector3 t = normal.PerpendicularTo(r);

            return t * thisVector.magnitude;
        }

        public static Vector3 relativeTo(this Vector3 vector3, Transform relativeToThis, bool isPlanar = true)
        {
            Vector3 forward = relativeToThis.forward;

            if (isPlanar)
            {
                Vector3 upAxis = Vector3.up;
                forward = forward.ProjectedOnPlane(upAxis);

                if (forward.IsZero())
                    forward = Vector3.ProjectOnPlane(relativeToThis.up, upAxis);
            }
            
            Quaternion q = Quaternion.LookRotation(forward);

            return q * vector3;
        }

        public static Vector3 relativeTo(this Vector3 vector3, Transform relativeToThis, Vector3 upAxis, bool isPlanar = true)
        {
            Vector3 forward = relativeToThis.forward;

            if (isPlanar)
            {
                forward = Vector3.ProjectOnPlane(forward, upAxis);

                if (forward.IsZero())
                    forward = Vector3.ProjectOnPlane(relativeToThis.up, upAxis);
            }

            Quaternion q = Quaternion.LookRotation(forward, upAxis);

            return q * vector3;
        }

        public static Quaternion ClampPitch(this Quaternion quaternion, float minPitchAngle, float maxPitchAngle)
        {
            quaternion.x /= quaternion.w;
            quaternion.y /= quaternion.w;
            quaternion.z /= quaternion.w;
            quaternion.w = 1.0f;

            float pitch = Mathf.Clamp(2.0f * Mathf.Rad2Deg * Mathf.Atan(quaternion.x), minPitchAngle, maxPitchAngle);

            quaternion.x = Mathf.Tan(pitch * 0.5f * Mathf.Deg2Rad);

            return quaternion;
        }
    }
}