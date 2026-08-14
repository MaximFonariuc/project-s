using UnityEngine;

namespace _App
{
    public interface ICharacterActionMotionMotor
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        Vector3 Up { get; }

        void SetActionMotion(Vector3 planarVelocity, Vector3 facingDirection);
        void ClearActionMotion();
    }
}
