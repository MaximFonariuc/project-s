using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatMotionController : IDisposable
    {
        private const float DirectionEpsilon = 0.0001f;

        private readonly ICharacterActionMotionMotor _motor;
        private readonly IPlayerTargetingState _targeting;

        private CombatMotionDefinition _definition;
        private Transform _previousConfirmedHitTarget;
        private Transform _sampledHeightTarget;
        private float _actionDuration;
        private float _planarSpeed;
        private float _previousTargetHeight;
        private float _previousNormalizedTime;
        private bool _verticalImpulseApplied;
        private bool _hasTargetHeightSample;
        private bool _isActive;

        public CombatMotionController(
            ICharacterActionMotionMotor motor,
            IPlayerTargetingState targeting)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        }

        public void Begin(
            CombatMotionDefinition definition,
            float actionDuration,
            Transform previousConfirmedHitTarget)
        {
            End();
            if (!definition.IsEnabled)
            {
                return;
            }

            if (actionDuration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(actionDuration));
            }

            _definition = definition;
            _previousConfirmedHitTarget = previousConfirmedHitTarget;
            _actionDuration = actionDuration;
            float motionDuration = actionDuration * (definition.Window.End - definition.Window.Start);
            _planarSpeed = definition.OverridePlanarVelocity && motionDuration > 0.0f
                ? definition.PlanarDistance / motionDuration
                : 0.0f;
            _verticalImpulseApplied = false;
            _isActive = true;
            Advance(0.0f);
        }

        public void Advance(float normalizedTime)
        {
            if (!_isActive)
            {
                return;
            }

            if (!_definition.Window.Contains(normalizedTime))
            {
                _motor.ClearActionMotion();
                return;
            }

            bool hasTarget = TryGetTarget(
                out Transform target,
                out Vector3 targetDirection,
                out float targetDistance,
                out float targetHeight);
            Vector3 forward = GetPlanarDirection(_motor.Forward);
            Vector3 motionForward = hasTarget && targetDirection.sqrMagnitude > DirectionEpsilon
                ? targetDirection
                : forward;
            Vector3 facingDirection = _definition.FaceTarget && hasTarget
                ? targetDirection
                : Vector3.zero;

            ApplyVerticalImpulse();

            Vector3 planarVelocity = motionForward * _planarSpeed;
            if (hasTarget && planarVelocity.sqrMagnitude > 0.0f &&
                targetDistance <= _definition.TargetStoppingDistance)
            {
                planarVelocity = Vector3.zero;
            }

            ResolveVerticalMotion(
                target,
                targetHeight,
                normalizedTime,
                hasTarget,
                out CharacterActionVerticalMode verticalMode,
                out float verticalVelocity);

            var intent = new CharacterActionMotionIntent(
                _definition.OverridePlanarVelocity
                    ? CharacterActionPlanarMode.OverrideVelocity
                    : CharacterActionPlanarMode.PreserveVelocity,
                planarVelocity,
                verticalMode,
                verticalVelocity,
                facingDirection);
            _motor.SetActionMotion(in intent);
        }

        public void End()
        {
            _motor.ClearActionMotion();
            _definition = default;
            _previousConfirmedHitTarget = null;
            _sampledHeightTarget = null;
            _actionDuration = 0.0f;
            _planarSpeed = 0.0f;
            _previousTargetHeight = 0.0f;
            _previousNormalizedTime = 0.0f;
            _verticalImpulseApplied = false;
            _hasTargetHeightSample = false;
            _isActive = false;
        }

        public void Dispose()
        {
            End();
        }

        private void ApplyVerticalImpulse()
        {
            if (_verticalImpulseApplied || Mathf.Approximately(_definition.VerticalImpulse, 0.0f))
            {
                return;
            }

            _motor.QueueActionVerticalImpulse(
                _definition.VerticalImpulse,
                _definition.OverrideVerticalVelocityOnImpulse);
            _verticalImpulseApplied = true;
        }

        private void ResolveVerticalMotion(
            Transform target,
            float targetHeight,
            float normalizedTime,
            bool hasTarget,
            out CharacterActionVerticalMode mode,
            out float velocity)
        {
            mode = _definition.VerticalMode;
            velocity = _definition.VerticalVelocity;
            if (!_definition.TrackTargetHeight || !hasTarget || target == null)
            {
                ResetTargetHeightSample();
                return;
            }

            if (!_hasTargetHeightSample || _sampledHeightTarget != target)
            {
                _sampledHeightTarget = target;
                _previousTargetHeight = Vector3.Dot(target.position, _motor.Up);
                _previousNormalizedTime = normalizedTime;
                _hasTargetHeightSample = true;
                return;
            }

            float deltaTime = (normalizedTime - _previousNormalizedTime) * _actionDuration;
            float sampledTargetHeight = Vector3.Dot(target.position, _motor.Up);
            float targetVerticalSpeed = deltaTime > 0.0001f
                ? (sampledTargetHeight - _previousTargetHeight) / deltaTime
                : 0.0f;
            float heightError = targetHeight + _definition.TargetHeightOffset;
            if (Mathf.Abs(heightError) <= _definition.TargetHeightTolerance)
            {
                heightError = 0.0f;
            }

            mode = CharacterActionVerticalMode.OverrideVelocity;
            velocity = Mathf.Clamp(
                targetVerticalSpeed + heightError * _definition.TargetHeightCorrectionSpeed,
                -_definition.MaxTargetVerticalSpeed,
                _definition.MaxTargetVerticalSpeed);
            _previousTargetHeight = sampledTargetHeight;
            _previousNormalizedTime = normalizedTime;
        }

        private void ResetTargetHeightSample()
        {
            _sampledHeightTarget = null;
            _previousTargetHeight = 0.0f;
            _previousNormalizedTime = 0.0f;
            _hasTargetHeightSample = false;
        }

        private bool TryGetTarget(
            out Transform target,
            out Vector3 direction,
            out float distance,
            out float height)
        {
            if (!_definition.FaceTarget && !_definition.TrackTargetHeight)
            {
                target = null;
                direction = default;
                distance = 0.0f;
                height = 0.0f;
                return false;
            }

            Vector3 aimPosition;
            if (_definition.UsesPreviousConfirmedHitTarget &&
                _previousConfirmedHitTarget != null)
            {
                target = _previousConfirmedHitTarget;
                aimPosition = _previousConfirmedHitTarget.position;
            }
            else
            {
                if (_definition.TargetSource == CombatMotionTargetSource.PreviousConfirmedHit)
                {
                    target = null;
                    direction = default;
                    distance = 0.0f;
                    height = 0.0f;
                    return false;
                }

                if (!_targeting.IsTargetLocked || _targeting.CurrentTarget == null)
                {
                    target = null;
                    direction = default;
                    distance = 0.0f;
                    height = 0.0f;
                    return false;
                }

                target = _targeting.CurrentTarget;
                aimPosition = _targeting.AimPosition;
            }

            Vector3 offset = aimPosition - _motor.Position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(offset, _motor.Up);
            distance = planarOffset.magnitude;
            direction = distance > DirectionEpsilon
                ? planarOffset / distance
                : Vector3.zero;
            height = Vector3.Dot(offset, _motor.Up);
            return true;
        }

        private Vector3 GetPlanarDirection(Vector3 worldDirection)
        {
            Vector3 direction = Vector3.ProjectOnPlane(worldDirection, _motor.Up);
            return direction.sqrMagnitude > DirectionEpsilon
                ? direction.normalized
                : Vector3.zero;
        }

    }
}
