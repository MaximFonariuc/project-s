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
        private float _planarSpeed;
        private bool _verticalImpulseApplied;
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

            bool hasTarget = TryGetTargetDirection(out Vector3 targetDirection, out float targetDistance);
            Vector3 forward = GetPlanarDirection(_motor.Forward);
            Vector3 motionForward = hasTarget ? targetDirection : forward;
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

            var intent = new CharacterActionMotionIntent(
                _definition.OverridePlanarVelocity
                    ? CharacterActionPlanarMode.OverrideVelocity
                    : CharacterActionPlanarMode.PreserveVelocity,
                planarVelocity,
                _definition.VerticalMode,
                _definition.VerticalVelocity,
                facingDirection);
            _motor.SetActionMotion(in intent);
        }

        public void End()
        {
            _motor.ClearActionMotion();
            _definition = default;
            _previousConfirmedHitTarget = null;
            _planarSpeed = 0.0f;
            _verticalImpulseApplied = false;
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

        private bool TryGetTargetDirection(out Vector3 direction, out float distance)
        {
            if (!_definition.FaceTarget)
            {
                direction = default;
                distance = 0.0f;
                return false;
            }

            Vector3 aimPosition;
            if (_definition.TargetSource == CombatMotionTargetSource.PreviousConfirmedHit)
            {
                if (_previousConfirmedHitTarget == null)
                {
                    direction = default;
                    distance = 0.0f;
                    return false;
                }

                aimPosition = _previousConfirmedHitTarget.position;
            }
            else
            {
                if (!_targeting.IsTargetLocked || _targeting.CurrentTarget == null)
                {
                    direction = default;
                    distance = 0.0f;
                    return false;
                }

                aimPosition = _targeting.AimPosition;
            }

            Vector3 offset = Vector3.ProjectOnPlane(
                aimPosition - _motor.Position,
                _motor.Up);
            distance = offset.magnitude;
            if (distance <= DirectionEpsilon)
            {
                direction = default;
                return false;
            }

            direction = offset / distance;
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
