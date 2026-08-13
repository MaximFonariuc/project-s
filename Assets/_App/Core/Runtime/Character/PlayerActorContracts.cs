using UnityEngine;

namespace _App
{
    public interface IPlayerActorTransform
    {
        Transform Root { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }
    }

    public interface IPlayerTargetingActor : IPlayerActorTransform
    {
        ERotationMode RotationMode { get; }

        void SetRotationMode(ERotationMode rotationMode);
    }

    public interface IPlayerCombatActor : IPlayerActorTransform
    {
        bool CanAttack { get; }
        bool IsFalling { get; }
        ERotationMode RotationMode { get; }
    }

    public interface IRootMotionPolicyTarget
    {
        RootMotionPolicy RootMotionPolicy { get; }
        void SetRootMotionPolicy(RootMotionPolicy policy);
    }

    public interface IDamageImmunityTarget
    {
        bool IsDamageImmune { get; }
        void SetDamageImmune(bool immune);
    }

    public interface IPlayerTraversalActor
    {
        bool IsClimbing { get; }
    }
}
