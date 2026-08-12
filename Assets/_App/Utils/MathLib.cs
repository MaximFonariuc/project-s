using UnityEngine;

namespace _App
{
    public static class MathLib
    {
        public static float Remap(float inA, float inB, float outA, float outB, float value)
        {
            float t = Mathf.InverseLerp(inA, inB, value);

            return Mathf.Lerp(outA, outB, t);
        }
        
        public static float Square(float value)
        {
            return value * value;
        }
        
        public static Vector3 GetTangent(Vector3 direction, Vector3 normal, Vector3 up)
        {
            Vector3 right = direction.PerpendicularTo(up);

            return normal.PerpendicularTo(right);
        }

        public static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 planeOrigin, Vector3 planeNormal)
        {
            Vector3 toPoint = point - planeOrigin;
            Vector3 toPointProjected = Vector3.Project(toPoint, planeNormal);

            return point - toPointProjected;
        }
        
        public static float ClampAngle(float a, float min, float max)
        {
            while (max < min)
                max += 360.0f;

            while (a > max)
                a -= 360.0f;

            while (a < min)
                a += 360.0f;

            return a > max ? a - (max + min) * 0.5f < 180.0f ? max : min : a;
        }

        public static float ClampAngle(float angle)
        {
            angle = angle % 360.0f;

            if (angle < 0.0f)
            {
                angle += 360.0f;
            }

            return angle;
        }

        public static float NormalizeAngle(float angle)
        {
            angle = ClampAngle(angle);

            if (angle > 180.0f)
            {
                angle -= 360.0f;
            }

            return angle;
        }
        
        private static float Clamp0360(float eulerAngles)
        {
            float result = eulerAngles - Mathf.CeilToInt(eulerAngles / 360f) * 360f;
            if (result < 0) result += 360f;

            return result;
        }

        public static float FixedTurn(float current, float target, float maxDegreesDelta)
        {
            if (maxDegreesDelta == 0.0f)
                return Clamp0360(current);

            if (maxDegreesDelta >= 360.0f)
                return Clamp0360(target);

            float result = Clamp0360(current);
            current = result;
            target = Clamp0360(target);

            if (current > target)
            {
                if (current - target < 180.0f)
                    result -= Mathf.Min(current - target, Mathf.Abs(maxDegreesDelta));
                else
                    result += Mathf.Min(target + 360.0f - current, Mathf.Abs(maxDegreesDelta));
            }
            else
            {
                if (target - current < 180.0f)
                    result += Mathf.Min(target - current, Mathf.Abs(maxDegreesDelta));
                else
                    result -= Mathf.Min(current + 360.0f - target, Mathf.Abs(maxDegreesDelta));
            }

            return Clamp0360(result);
        }
        
        public static float Damp(float a, float b, float lambda, float dt)
        {
            return Mathf.Lerp(a, b, 1.0f - Mathf.Exp(-lambda * dt));
        }
        
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt)
        {
            return Vector3.Lerp(a, b, 1.0f - Mathf.Exp(-lambda * dt));
        }
    }
}