using UnityEngine;

namespace _App
{
    public enum CharacterActionPlanarMode
    {
        PreserveVelocity,
        OverrideVelocity
    }

    public enum CharacterActionVerticalMode
    {
        PreserveVelocity,
        OverrideVelocity,
        MinimumVelocity
    }

    public readonly struct CharacterActionMotionIntent
    {
        public CharacterActionPlanarMode PlanarMode { get; }
        public Vector3 PlanarVelocity { get; }
        public CharacterActionVerticalMode VerticalMode { get; }
        public float VerticalVelocity { get; }
        public Vector3 FacingDirection { get; }

        public CharacterActionMotionIntent(
            CharacterActionPlanarMode planarMode,
            Vector3 planarVelocity,
            CharacterActionVerticalMode verticalMode,
            float verticalVelocity,
            Vector3 facingDirection)
        {
            PlanarMode = planarMode;
            PlanarVelocity = planarVelocity;
            VerticalMode = verticalMode;
            VerticalVelocity = verticalVelocity;
            FacingDirection = facingDirection;
        }
    }

    public interface ICharacterActionMotionMotor
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        Vector3 Up { get; }

        void SetActionMotion(in CharacterActionMotionIntent intent);
        void QueueActionPlanarDisplacement(Vector3 worldDisplacement);
        void QueueActionVerticalImpulse(float verticalVelocity, bool overrideVerticalVelocity);
        void ClearActionMotion();
    }
}
