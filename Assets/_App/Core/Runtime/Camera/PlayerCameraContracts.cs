using System;
using UnityEngine;

namespace _App
{
    public enum PlayerCameraMode
    {
        FreeLook,
        TargetLock
    }

    public readonly struct PlayerCameraSettings
    {
        public float DefaultDistance { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public bool InvertLook { get; }
        public Vector2 LookSensitivity { get; }
        public float MinPitch { get; }
        public float MaxPitch { get; }

        public PlayerCameraSettings(
            float defaultDistance,
            float minDistance,
            float maxDistance,
            bool invertLook,
            Vector2 lookSensitivity,
            float minPitch,
            float maxPitch)
        {
            MinDistance = Mathf.Max(0.0f, Mathf.Min(minDistance, maxDistance));
            MaxDistance = Mathf.Max(MinDistance, Mathf.Max(minDistance, maxDistance));
            DefaultDistance = Mathf.Clamp(defaultDistance, MinDistance, MaxDistance);
            InvertLook = invertLook;
            LookSensitivity = lookSensitivity;
            MinPitch = Mathf.Min(minPitch, maxPitch);
            MaxPitch = Mathf.Max(minPitch, maxPitch);
        }
    }

    public interface ICameraOrientation
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        Vector3 Right { get; }
    }

    public interface IPlayerCameraRig : ICameraOrientation
    {
        Transform FollowTarget { get; }
        float Distance { get; set; }
        float FieldOfView { get; set; }
        float ShoulderSide { get; set; }

        void SetOrientation(Quaternion rotation);
    }

    public interface IPlayerCameraState
    {
        event Action<PlayerCameraMode> ModeChanged;

        PlayerCameraMode Mode { get; }
        bool IsCursorActive { get; }
        bool IsGravityInverted { get; }
    }

    public interface IPlayerCameraCommands
    {
        void Recenter();
        void SwapShoulder();
        void SetFraming(float distanceOffset, float fieldOfViewOffset);
        void ClearFraming();
        void ToggleGravityOrientation();
    }

    public interface IPlayerCameraPresentation
    {
        void Tick(bool hasTarget, Vector3 targetPosition, float deltaTime);
    }

    internal interface ICameraImpactFeedback
    {
        void EmitImpact(Vector3 worldPoint, Vector3 impactDirection, float strength);
    }
}
