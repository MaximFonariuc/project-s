using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatMotionController : IDisposable
    {
        private const float DirectionEpsilon = 0.0001f;

        private readonly ICharacterActionMotionMotor _motor;
        private readonly IPlayerTargetingState _targeting;
        private readonly IPlayerCombatTargetingQuery _targetingQuery;

        private CombatMotionDefinition _definition;
        private Transform _previousConfirmedHitTarget;
        private ICombatTarget _previousConfirmedCombatTarget;
        private UnityEngine.Object _previousConfirmedCombatTargetObject;
        private bool _hasPreviousConfirmedTargetContract;
        private ICombatTarget _assistedTarget;
        private UnityEngine.Object _assistedTargetObject;
        private Transform _sampledHeightTarget;
        private float _actionDuration;
        private float _planarSpeed;
        private float _previousTargetHeight;
        private float _previousNormalizedTime;
        private bool _verticalImpulseApplied;
        private bool _hasTargetHeightSample;
        private bool _isActive;

        public Transform SelectedTarget { get; private set; }

        public CombatMotionController(
            ICharacterActionMotionMotor motor,
            IPlayerTargetingState targeting,
            IPlayerCombatTargetingQuery targetingQuery)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _targetingQuery = targetingQuery ?? throw new ArgumentNullException(nameof(targetingQuery));
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
            CachePreviousConfirmedTarget();
            _actionDuration = actionDuration;
            float motionDuration = actionDuration * (definition.Window.End - definition.Window.Start);
            _planarSpeed = definition.OverridePlanarVelocity && motionDuration > 0.0f
                ? definition.PlanarDistance / motionDuration
                : 0.0f;
            _verticalImpulseApplied = false;
            _isActive = true;
            AcquireAssistedTarget();
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
            if (hasTarget && target != null)
            {
                SelectedTarget = target;
            }

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
            _previousConfirmedCombatTarget = null;
            _previousConfirmedCombatTargetObject = null;
            _hasPreviousConfirmedTargetContract = false;
            _assistedTarget = null;
            _assistedTargetObject = null;
            _sampledHeightTarget = null;
            SelectedTarget = null;
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
            if (!_definition.FaceTarget &&
                !_definition.TrackTargetHeight &&
                !_definition.OverridePlanarVelocity)
            {
                target = null;
                direction = default;
                distance = 0.0f;
                height = 0.0f;
                return false;
            }

            target = null;
            Vector3 aimPosition = default;
            bool hasPreviousTarget =
                _definition.UsesPreviousConfirmedHitTarget &&
                TryGetPreviousConfirmedTarget(out target, out aimPosition);
            if (!hasPreviousTarget && _definition.AllowsTargetFallback)
            {
                if (_targeting.IsTargetLocked && _targeting.CurrentTarget != null)
                {
                    target = _targeting.CurrentTarget;
                    aimPosition = _targeting.AimPosition;
                }
                else if (_assistedTargetObject != null &&
                         _assistedTarget != null &&
                         _assistedTarget.IsTargetable &&
                         _assistedTarget.TargetRoot != null)
                {
                    target = _assistedTarget.TargetRoot;
                    aimPosition = _assistedTarget.AimPosition;
                }
                else
                {
                    return FailTarget(out target, out direction, out distance, out height);
                }
            }
            else if (!hasPreviousTarget)
            {
                return FailTarget(out target, out direction, out distance, out height);
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

        private void AcquireAssistedTarget()
        {
            if (!_definition.AllowTargetAssist ||
                !_definition.AllowsTargetFallback ||
                HasAvailablePreviousConfirmedTarget() ||
                _targeting.IsTargetLocked ||
                !_targetingQuery.TryGetAttackAssistTarget(
                    _motor.Position,
                    _motor.Forward,
                    _definition.TargetAssistDistance,
                    _definition.TargetAssistAngle,
                    out ICombatTarget target))
            {
                return;
            }

            UnityEngine.Object targetObject = target as UnityEngine.Object;
            if (targetObject == null || target.TargetRoot == null)
            {
                return;
            }

            _assistedTarget = target;
            _assistedTargetObject = targetObject;
        }

        private void CachePreviousConfirmedTarget()
        {
            _previousConfirmedCombatTarget = null;
            _previousConfirmedCombatTargetObject = null;
            _hasPreviousConfirmedTargetContract = false;
            if (_previousConfirmedHitTarget == null)
            {
                return;
            }

            ICombatTarget target =
                _previousConfirmedHitTarget.GetComponent<ICombatTarget>() ??
                _previousConfirmedHitTarget.GetComponentInChildren<ICombatTarget>(true);
            UnityEngine.Object targetObject = target as UnityEngine.Object;
            if (targetObject == null)
            {
                return;
            }

            _previousConfirmedCombatTarget = target;
            _previousConfirmedCombatTargetObject = targetObject;
            _hasPreviousConfirmedTargetContract = true;
        }

        private bool TryGetPreviousConfirmedTarget(
            out Transform target,
            out Vector3 aimPosition)
        {
            if (!HasAvailablePreviousConfirmedTarget())
            {
                target = null;
                aimPosition = default;
                return false;
            }

            if (_hasPreviousConfirmedTargetContract)
            {
                target = _previousConfirmedCombatTarget.TargetRoot;
                aimPosition = _previousConfirmedCombatTarget.AimPosition;
                return true;
            }

            target = _previousConfirmedHitTarget;
            aimPosition = _previousConfirmedHitTarget.position;
            return true;
        }

        private bool HasAvailablePreviousConfirmedTarget()
        {
            if (_previousConfirmedHitTarget == null ||
                !_previousConfirmedHitTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            return !_hasPreviousConfirmedTargetContract ||
                   (_previousConfirmedCombatTargetObject != null &&
                    _previousConfirmedCombatTarget != null &&
                    _previousConfirmedCombatTarget.IsTargetable &&
                    _previousConfirmedCombatTarget.TargetRoot != null);
        }

        private static bool FailTarget(
            out Transform target,
            out Vector3 direction,
            out float distance,
            out float height)
        {
            target = null;
            direction = default;
            distance = 0.0f;
            height = 0.0f;
            return false;
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
