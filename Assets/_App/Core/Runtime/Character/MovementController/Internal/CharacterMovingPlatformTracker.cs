using System;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterMovingPlatformTracker
    {
        private Rigidbody _explicitPlatform;
        private MovingPlatform _state;

        public MovingPlatform State => _state;

        public Rigidbody CurrentPlatform => _state.Platform;

        public void AttachTo(Rigidbody platform)
        {
            _explicitPlatform = platform;
        }

        public void RefreshAttachment(Vector3 position, Quaternion rotation, bool isGrounded,
            Collider groundCollider, Func<Collider, bool> canRideOn)
        {
            _state.LastPlatform = _state.Platform;

            if (_explicitPlatform != null)
                _state.Platform = _explicitPlatform;
            else if (isGrounded && canRideOn(groundCollider))
                _state.Platform = groundCollider.attachedRigidbody;
            else
                _state.Platform = null;

            if (_state.Platform == null)
                return;

            Transform platformTransform = _state.Platform.transform;

            _state.Position = position;
            _state.LocalPosition = platformTransform.InverseTransformPoint(position);
            _state.Rotation = rotation;
            _state.LocalRotation = Quaternion.Inverse(platformTransform.rotation) * rotation;

        }

        public Vector3 CalculateVelocity(float deltaTime, Quaternion characterRotation, Vector3 characterUp,
            bool impartRotation, out Quaternion updatedRotation, out Vector3 previousPlatformVelocity)
        {
            previousPlatformVelocity = _state.PlatformVelocity;
            updatedRotation = characterRotation;

            if (_state.Platform == null)
            {
                _state.DeltaPosition = Vector3.zero;
                _state.DeltaRotation = Quaternion.identity;
                _state.PlatformVelocity = Vector3.zero;
                return Vector3.zero;
            }

            Transform platformTransform = _state.Platform.transform;
            Vector3 newPositionOnPlatform = platformTransform.TransformPoint(_state.LocalPosition);
            Vector3 deltaPosition = newPositionOnPlatform - _state.Position;

            _state.DeltaPosition = deltaPosition;
            _state.PlatformVelocity = deltaTime > 0.0f ? deltaPosition / deltaTime : Vector3.zero;
            _state.DeltaRotation = Quaternion.identity;

            if (!impartRotation)
                return _state.PlatformVelocity;

            Quaternion newRotationOnPlatform = platformTransform.rotation * _state.LocalRotation;
            Quaternion deltaRotation = newRotationOnPlatform * Quaternion.Inverse(_state.Rotation);
            _state.DeltaRotation = deltaRotation;

            Vector3 newForward = Vector3.ProjectOnPlane(deltaRotation * characterRotation * Vector3.forward,
                characterUp);

            if (newForward.sqrMagnitude > 0.000001f)
                updatedRotation = Quaternion.LookRotation(newForward.normalized, characterUp);

            return _state.PlatformVelocity;
        }

        public void ApplyVelocityTransition(ref Vector3 velocity, Vector3 previousPlatformVelocity,
            bool impartPlatformVelocity)
        {
            if (!impartPlatformVelocity)
                return;

            if (_state.LastPlatform != null && _state.Platform != _state.LastPlatform)
                velocity += previousPlatformVelocity;
        }
    }
}
