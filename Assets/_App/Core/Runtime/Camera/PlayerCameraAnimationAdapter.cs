using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCameraAnimationAdapter : IPlayerCameraPresentation
    {
        private const float InterpolationSpeed = 5.0f;
        private const float LookLimit = 0.5f;

        private readonly Transform _characterTransform;
        private readonly Animator _animator;
        private readonly ICharacterLookAnimationDriver _animation;
        private readonly Transform _head;

        private float _headLookX;
        private float _headLookY;

        public PlayerCameraAnimationAdapter(
            IPlayerActorTransform actor,
            Animator animator,
            ICharacterLookAnimationDriver animation)
        {
            _characterTransform = actor?.Root != null
                ? actor.Root
                : throw new ArgumentNullException(nameof(actor));
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _head = _animator.isHuman
                ? _animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
        }

        public void Tick(bool hasTarget, Vector3 targetPosition, float deltaTime)
        {
            float targetX = 0.0f;
            float targetY = 0.0f;

            if (hasTarget)
            {
                Vector3 eyePosition = _head != null ? _head.position : _characterTransform.position;
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

            _animation.SetLook(_headLookX, _headLookY);
        }
    }
}
