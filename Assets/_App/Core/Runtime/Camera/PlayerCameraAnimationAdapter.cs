using System;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCameraAnimationAdapter : IPlayerCameraPresentation
    {
        private const float InterpolationSpeed = 5.0f;
        private const float LookLimit = 0.5f;

        private readonly Transform _characterTransform;
        private readonly Animator _animator;
        private readonly AnimationController _animation;

        private float _headLookX;
        private float _headLookY;

        public PlayerCameraAnimationAdapter(
            IPlayerActorTransform actor,
            Animator animator,
            AnimationController animation)
        {
            _characterTransform = actor?.Root != null
                ? actor.Root
                : throw new ArgumentNullException(nameof(actor));
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Tick(bool hasTarget, Vector3 targetPosition, float deltaTime)
        {
            float targetX = 0.0f;
            float targetY = 0.0f;

            if (hasTarget)
            {
                Transform head = _animator.isHuman
                    ? _animator.GetBoneTransform(HumanBodyBones.Head)
                    : null;
                Vector3 eyePosition = head != null ? head.position : _characterTransform.position;
                Vector3 direction = targetPosition - eyePosition;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Vector3 localDirection = _characterTransform.InverseTransformDirection(direction.normalized);
                    targetX = Mathf.Clamp(localDirection.x, -LookLimit, LookLimit);
                    targetY = Mathf.Clamp(localDirection.y, -LookLimit, LookLimit);
                }
            }

            float interpolation = 1.0f - Mathf.Exp(-InterpolationSpeed * Mathf.Max(0.0f, deltaTime));
            _headLookX = Mathf.Lerp(_headLookX, targetX, interpolation);
            _headLookY = Mathf.Lerp(_headLookY, targetY, interpolation);

            _animation.SetFloat(AnimationHashes.HeadLookX, _headLookX);
            _animation.SetFloat(AnimationHashes.HeadLookY, _headLookY);
        }
    }
}
