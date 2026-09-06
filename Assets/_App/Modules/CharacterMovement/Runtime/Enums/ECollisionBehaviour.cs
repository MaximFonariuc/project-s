using System;

namespace ModularCharacterMovement
{
    [Flags]
    public enum ECollisionBehaviour
    {
        Default = 0,
        Walkable = 1 << 0,
        NotWalkable = 1 << 1,
        CanPerchOn = 1 << 2,
        CanNotPerchOn = 1 << 3,
        CanStepOn = 1 << 4,
        CanNotStepOn = 1 << 5,
        CanRideOn = 1 << 6,
        CanNotRideOn = 1 << 7
    }
}