using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCameraAnimationAdapter : IPlayerCameraPresentation
    {
        private readonly Transform _characterTransform;
        private readonly ICharacterLookAnimationDriver _animation;
        private readonly Transform _head;

        public PlayerCameraAnimationAdapter(
            IPlayerActorTransform actor,
            Animator animator,
            ICharacterLookAnimationDriver animation)
        {
            _characterTransform = actor?.Root != null
                ? actor.Root
                : throw new ArgumentNullException(nameof(actor));
            Animator safeAnimator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _head = safeAnimator.isHuman
                ? safeAnimator.GetBoneTransform(HumanBodyBones.Head)
                : null;
        }

        public void Tick(bool hasTarget, Vector3 targetPosition, float deltaTime)
        {
            float yawDegrees = 0.0f;
            float pitchDegrees = 0.0f;

            if (hasTarget)
            {
                Vector3 eyePosition = _head != null ? _head.position : _characterTransform.position;
                Vector3 direction = targetPosition - eyePosition;
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
