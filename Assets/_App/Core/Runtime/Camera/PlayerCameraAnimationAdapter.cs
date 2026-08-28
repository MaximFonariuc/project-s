using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCameraAnimationAdapter : IPlayerCameraPresentation
    {
        private readonly Transform _characterTransform;
        private readonly Transform _lookOrigin;
        private readonly ICharacterLookAnimationDriver _animation;

        public PlayerCameraAnimationAdapter(
            IPlayerActorTransform actor,
            Transform lookOrigin,
            ICharacterLookAnimationDriver animation)
        {
            _characterTransform = actor?.Root != null
                ? actor.Root
                : throw new ArgumentNullException(nameof(actor));
            _lookOrigin = lookOrigin != null
                ? lookOrigin
                : throw new ArgumentNullException(nameof(lookOrigin));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Tick(bool hasTarget, Vector3 targetPosition, float deltaTime)
        {
            float yawDegrees = 0.0f;
            float pitchDegrees = 0.0f;

            if (hasTarget)
            {
                Vector3 direction = targetPosition - _lookOrigin.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Vector3 localDirection = _characterTransform.InverseTransformDirection(direction.normalized);
                    float planarMagnitude = Mathf.Sqrt(
                        localDirection.x * localDirection.x +
                        localDirection.z * localDirection.z);
                    yawDegrees = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
                    pitchDegrees = Mathf.Atan2(localDirection.y, planarMagnitude) * Mathf.Rad2Deg;
                }
            }

            var frame = new CharacterLookFrame(yawDegrees, pitchDegrees);
            _animation.ApplyLook(in frame, Mathf.Max(0.0f, deltaTime));
        }
    }
}
