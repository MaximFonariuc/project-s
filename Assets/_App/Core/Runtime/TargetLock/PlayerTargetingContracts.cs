using System;
using UnityEngine;

namespace _App
{
    public interface ICombatTarget
    {
        int TargetId { get; }
        bool IsTargetable { get; }
        Transform TargetRoot { get; }
        Vector3 AimPosition { get; }
    }

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

    public interface IPlayerCombatTargetingQuery
    {
        bool TryGetAttackAssistTarget(
            Vector3 origin,
            Vector3 forward,
            float maxDistance,
            float maxAngle,
            out ICombatTarget target);
    }
}
