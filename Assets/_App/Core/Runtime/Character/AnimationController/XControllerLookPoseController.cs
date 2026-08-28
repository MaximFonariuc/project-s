using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal sealed class XControllerLookPoseController
    {
        public const float AirborneBodyWeight = 0.0f;
        public const float AirborneHeadWeight = 0.45f;

        private const float BodyHorizontalPoseDegrees = 52.0f;
        private const float BodyVerticalPoseDegrees = 50.0f;
        private const float HeadHorizontalPoseDegrees = 90.0f;
        private const float HeadVerticalPoseDegrees = 90.0f;
        private const float BodyHorizontalLimit = 0.45f;
        private const float BodyVerticalLimit = 0.30f;
        private const float HeadHorizontalLimit = 0.60f;
        private const float HeadVerticalLimit = 0.45f;
        private const float BodyHorizontalShare = 0.35f;
        private const float BodyVerticalShare = 0.30f;
        private const float BodyResponseSpeed = 3.5f;
        private const float HeadResponseSpeed = 8.0f;
        private const float LayerWeightResponseSpeed = 10.0f;

        private static readonly int BodyLookXHash = Animator.StringToHash(
            XControllerAnimationContract.BodyLookXParameterName);
        private static readonly int BodyLookYHash = Animator.StringToHash(
            XControllerAnimationContract.BodyLookYParameterName);
        private static readonly int HeadLookXHash = Animator.StringToHash(
            XControllerAnimationContract.HeadLookXParameterName);
        private static readonly int HeadLookYHash = Animator.StringToHash(
            XControllerAnimationContract.HeadLookYParameterName);

        private readonly Animator _animator;
        private readonly int _bodyLayerIndex;
        private readonly int _headLayerIndex;

        private float _bodyLookX;
        private float _bodyLookY;
        private float _headLookX;
        private float _headLookY;
        private float _bodyLayerWeight = 1.0f;
        private float _headLayerWeight = 1.0f;
        private float _actionBodyWeight = 1.0f;
        private float _actionHeadWeight = 1.0f;
        private bool _isGrounded = true;

        public XControllerLookPoseController(Animator animator)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _bodyLayerIndex = _animator.GetLayerIndex(
                XControllerAnimationContract.BodyLookLayerName);
            _headLayerIndex = _animator.GetLayerIndex(
                XControllerAnimationContract.HeadLookLayerName);
        }

        public void Apply(in CharacterLookFrame frame, float deltaTime)
        {
            float bodyWeight = _actionBodyWeight *
                               (_isGrounded ? 1.0f : AirborneBodyWeight);
            float headWeight = _actionHeadWeight *
                               (_isGrounded ? 1.0f : AirborneHeadWeight);
            ResolveTargets(
                in frame,
                out float bodyTargetX,
                out float bodyTargetY,
                out float headTargetX,
                out float headTargetY);

            float safeDeltaTime = Mathf.Max(0.0f, deltaTime);
            _bodyLookX = Damp(
                _bodyLookX,
                bodyTargetX,
                BodyResponseSpeed,
                safeDeltaTime);
            _bodyLookY = Damp(
                _bodyLookY,
                bodyTargetY,
                BodyResponseSpeed,
                safeDeltaTime);
            _headLookX = Damp(
                _headLookX,
                headTargetX,
                HeadResponseSpeed,
                safeDeltaTime);
            _headLookY = Damp(
                _headLookY,
                headTargetY,
                HeadResponseSpeed,
                safeDeltaTime);
            _bodyLayerWeight = Damp(
                _bodyLayerWeight,
                bodyWeight,
                LayerWeightResponseSpeed,
                safeDeltaTime);
            _headLayerWeight = Damp(
                _headLayerWeight,
                headWeight,
                LayerWeightResponseSpeed,
                safeDeltaTime);

            WriteParameters();
            WriteLayerWeights();
        }

        public void SetGrounded(bool grounded)
        {
            _isGrounded = grounded;
        }

        public void SetActionWeights(float bodyWeight, float headWeight)
        {
            _actionBodyWeight = Mathf.Clamp01(bodyWeight);
            _actionHeadWeight = Mathf.Clamp01(headWeight);
        }

        public void Reset()
        {
            _bodyLookX = 0.0f;
            _bodyLookY = 0.0f;
            _headLookX = 0.0f;
            _headLookY = 0.0f;
            _bodyLayerWeight = 1.0f;
            _headLayerWeight = 1.0f;
            _actionBodyWeight = 1.0f;
            _actionHeadWeight = 1.0f;
            _isGrounded = true;
            WriteParameters();
            WriteLayerWeights();
        }

        private void WriteParameters()
        {
            _animator.SetFloat(BodyLookXHash, _bodyLookX);
            _animator.SetFloat(BodyLookYHash, _bodyLookY);
            _animator.SetFloat(HeadLookXHash, _headLookX);
            _animator.SetFloat(HeadLookYHash, _headLookY);
        }

        private void WriteLayerWeights()
        {
            if (_bodyLayerIndex >= 0)
            {
                _animator.SetLayerWeight(_bodyLayerIndex, _bodyLayerWeight);
            }

            if (_headLayerIndex >= 0)
            {
                _animator.SetLayerWeight(_headLayerIndex, _headLayerWeight);
            }
        }

        private static void ResolveTargets(
            in CharacterLookFrame frame,
            out float bodyX,
            out float bodyY,
            out float headX,
            out float headY)
        {
            float yawDegrees = Mathf.Clamp(frame.YawDegrees, -180.0f, 180.0f);
            float pitchDegrees = Mathf.Clamp(frame.PitchDegrees, -89.0f, 89.0f);

            float bodyYawDegrees = Mathf.Clamp(
                yawDegrees * BodyHorizontalShare,
                -BodyHorizontalPoseDegrees * BodyHorizontalLimit,
                BodyHorizontalPoseDegrees * BodyHorizontalLimit);
            float bodyPitchDegrees = Mathf.Clamp(
                pitchDegrees * BodyVerticalShare,
                -BodyVerticalPoseDegrees * BodyVerticalLimit,
                BodyVerticalPoseDegrees * BodyVerticalLimit);
            float headYawDegrees = Mathf.Clamp(
                yawDegrees - bodyYawDegrees,
                -HeadHorizontalPoseDegrees * HeadHorizontalLimit,
                HeadHorizontalPoseDegrees * HeadHorizontalLimit);
            float headPitchDegrees = Mathf.Clamp(
                pitchDegrees - bodyPitchDegrees,
                -HeadVerticalPoseDegrees * HeadVerticalLimit,
                HeadVerticalPoseDegrees * HeadVerticalLimit);

            bodyX = bodyYawDegrees / BodyHorizontalPoseDegrees;
            bodyY = bodyPitchDegrees / BodyVerticalPoseDegrees;
            headX = headYawDegrees / HeadHorizontalPoseDegrees;
            headY = headPitchDegrees / HeadVerticalPoseDegrees;
        }

        private static float Damp(
            float current,
            float target,
            float responseSpeed,
            float deltaTime)
        {
            float interpolation = 1.0f - Mathf.Exp(-responseSpeed * deltaTime);
            return Mathf.Lerp(current, target, interpolation);
        }
    }
}
