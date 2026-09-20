using UnityEngine;

namespace _App
{
    public sealed class PlayerLocomotionRuntime
    {
        internal Vector3 RawMoveDirection { get; set; }
        internal Vector3 EffectiveMoveDirection { get; set; }
        internal PlayerSpeedMode SpeedMode { get; set; } = PlayerSpeedMode.Run;
        internal float RequestedSpeed { get; set; }
        internal bool DodgeBackwards { get; set; }
        internal bool JumpStarting { get; set; }
        internal float JumpStartupTimeRemaining { get; set; }
    }
}
