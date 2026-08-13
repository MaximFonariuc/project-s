using UnityEngine;

namespace _App
{
    public class SlopeLimitedCharacter : Character
    {
        public override float GetMaxSpeed()
        {
            float maxSpeed = base.GetMaxSpeed();

            float slopeAngle = GetSignedSlopeAngle();
            float speedModifier = slopeAngle > 0.0f
                ? 1.0f - Mathf.InverseLerp(0.0f, 90.0f, +slopeAngle)
                : 1.0f + Mathf.InverseLerp(0.0f, 90.0f, -slopeAngle);
            
            return maxSpeed * speedModifier;
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 400, 20),
                $"Slope angle: {GetSignedSlopeAngle():F2} maxSpeed: {GetMaxSpeed():F2} ");
        }
    }
}
