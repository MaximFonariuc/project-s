using System;
using UnityEngine;

namespace _App
{
    public interface IPlayerTargetingState
    {
        event Action<Transform> TargetChanged;

        bool IsTargetLocked { get; }
        Transform CurrentTarget { get; }
        Vector3 AimPosition { get; }
    }

    public interface IPlayerTargetingCommands
    {
        bool ToggleLock();
        bool TryAcquireTarget();
        bool SwitchTarget(int direction);
        void ClearTarget();
    }
}
