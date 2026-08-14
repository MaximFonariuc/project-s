using System;
using UnityEngine;

namespace _App
{
    internal readonly struct NormalizedActionWindow
    {
        public static NormalizedActionWindow Disabled => default;

        public bool IsEnabled { get; }
        public float Start { get; }
        public float End { get; }

        public NormalizedActionWindow(float start, float end)
        {
            if (start < 0.0f || start > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (end <= start || end > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(end));
            }

            IsEnabled = true;
            Start = start;
            End = end;
        }

        public bool Contains(float normalizedTime)
        {
            return IsEnabled && normalizedTime >= Start && normalizedTime <= End;
        }
    }

    internal readonly struct CombatActionDefinition
    {
        public NormalizedActionWindow HitboxWindow { get; }
        public NormalizedActionWindow ChainWindow { get; }
        public NormalizedActionWindow CancelWindow { get; }
        public NormalizedActionWindow MovementConstraintWindow { get; }
        public NormalizedActionWindow InvulnerabilityWindow { get; }
        public float RecoveryStart { get; }
        public float CompletionTime { get; }
        public bool HasCommit { get; }
        public float CommitTime { get; }
        public float Cooldown { get; }
        public PlayerMovementIntent MovementIntent { get; }
        public RootMotionPolicy RootMotionPolicy { get; }

        public CombatActionDefinition(
            NormalizedActionWindow hitboxWindow,
            NormalizedActionWindow chainWindow,
            NormalizedActionWindow cancelWindow,
            NormalizedActionWindow movementConstraintWindow,
            NormalizedActionWindow invulnerabilityWindow,
            float recoveryStart,
            float completionTime,
            bool hasCommit,
            float commitTime,
            float cooldown,
            PlayerMovementIntent movementIntent,
            RootMotionPolicy rootMotionPolicy)
        {
            if (completionTime <= 0.0f || completionTime > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(completionTime));
            }

            if (recoveryStart < 0.0f || recoveryStart > completionTime)
            {
                throw new ArgumentOutOfRangeException(nameof(recoveryStart));
            }

            ValidateWindow(hitboxWindow, completionTime, nameof(hitboxWindow));
            ValidateWindow(chainWindow, completionTime, nameof(chainWindow));
            ValidateWindow(cancelWindow, completionTime, nameof(cancelWindow));
            ValidateWindow(movementConstraintWindow, completionTime, nameof(movementConstraintWindow));
            ValidateWindow(invulnerabilityWindow, completionTime, nameof(invulnerabilityWindow));

            if (hasCommit && (commitTime < 0.0f || commitTime > completionTime))
            {
                throw new ArgumentOutOfRangeException(nameof(commitTime));
            }

            if (cooldown < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldown));
            }

            HitboxWindow = hitboxWindow;
            ChainWindow = chainWindow;
            CancelWindow = cancelWindow;
            MovementConstraintWindow = movementConstraintWindow;
            InvulnerabilityWindow = invulnerabilityWindow;
            RecoveryStart = recoveryStart;
            CompletionTime = completionTime;
            HasCommit = hasCommit;
            CommitTime = hasCommit ? commitTime : 0.0f;
            Cooldown = cooldown;
            MovementIntent = movementIntent;
            RootMotionPolicy = rootMotionPolicy;
        }

        private static void ValidateWindow(
            NormalizedActionWindow window,
            float completionTime,
            string parameterName)
        {
            if (window.IsEnabled && window.End > completionTime)
            {
                throw new ArgumentException(
                    "Action window cannot end after action completion.",
                    parameterName);
            }
        }
    }

    internal readonly struct CombatActionFrame
    {
        public float NormalizedTime { get; }
        public bool IsHitboxOpen { get; }
        public bool CanChain { get; }
        public bool CanCancel { get; }
        public bool IsMovementConstrained { get; }
        public bool IsInvulnerable { get; }
        public bool IsInRecovery { get; }
        public bool CommitReached { get; }
        public bool IsComplete { get; }

        public CombatActionFrame(
            float normalizedTime,
            bool isHitboxOpen,
            bool canChain,
            bool canCancel,
            bool isMovementConstrained,
            bool isInvulnerable,
            bool isInRecovery,
            bool commitReached,
            bool isComplete)
        {
            NormalizedTime = normalizedTime;
            IsHitboxOpen = isHitboxOpen;
            CanChain = canChain;
            CanCancel = canCancel;
            IsMovementConstrained = isMovementConstrained;
            IsInvulnerable = isInvulnerable;
            IsInRecovery = isInRecovery;
            CommitReached = commitReached;
            IsComplete = isComplete;
        }
    }

    internal enum CombatActionDirective
    {
        Continue,
        Interrupt
    }

    internal enum CombatActionRunResult
    {
        Completed,
        Interrupted
    }

    internal interface ICombatActionObserver
    {
        CombatActionDirective Observe(in CombatActionFrame frame);
    }

}
