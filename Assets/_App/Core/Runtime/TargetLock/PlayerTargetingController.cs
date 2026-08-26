using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    public sealed class PlayerTargetingController :
        IPlayerTargetingState,
        IPlayerTargetingCommands,
        IPlayerCombatTargetingQuery,
        IDisposable
    {
        public const float MaxLockDistance = 25.0f;

        private const float MaxViewAngle = 35.0f;
        private const float TargetSwitchActuationThreshold = 0.65f;
        private const float TargetSwitchReleaseThreshold = 0.35f;
        private const int InitialBufferSize = 8;
        private const int MaxBufferSize = 128;

        private readonly IPlayerInput _input;
        private readonly IPlayerTargetingActor _actor;
        private readonly ICameraOrientation _camera;
        private readonly LayerMask _targetMask;
        private readonly int _visibilityMask;
        private readonly HashSet<int> _visitedTargets = new HashSet<int>();

        private Collider[] _overlapBuffer = new Collider[InitialBufferSize];
        private ICombatTarget _currentTarget;
        private UnityEngine.Object _currentTargetObject;
        private ERotationMode _rotationModeBeforeLock;
        private bool _hasTarget;
        private bool _targetSwitchArmed;
        private bool _enabled;

        public event Action<Transform> TargetChanged;

        public bool IsTargetLocked =>
            _hasTarget &&
            _currentTargetObject != null &&
            CurrentTarget != null &&
            _currentTarget.IsTargetable;
        public Transform CurrentTarget { get; private set; }
        public Vector3 AimPosition =>
            _currentTargetObject != null && CurrentTarget != null
                ? _currentTarget.AimPosition
                : Vector3.zero;

        public PlayerTargetingController(
            IPlayerInput input,
            IPlayerTargetingActor actor,
            ICameraOrientation camera)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _targetMask = LayerMask.GetMask("Enemy");
            _visibilityMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");

            if (_targetMask.value == 0)
            {
                Debug.LogError("Player targeting requires an Enemy physics layer.", _actor.Root);
            }
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _input.LockOnRequested += OnLockOnRequested;
            _targetSwitchArmed = false;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.LockOnRequested -= OnLockOnRequested;
            _enabled = false;
            _targetSwitchArmed = false;
            ClearTarget();
        }

        public void Tick()
        {
            UpdateTargetSwitchInput();

            if (!_hasTarget)
            {
                return;
            }

            if (_currentTargetObject == null ||
                CurrentTarget == null ||
                !_currentTarget.IsTargetable)
            {
                ClearTarget();
                return;
            }

            float maxDistanceSqr = MaxLockDistance * MaxLockDistance;
            if ((CurrentTarget.position - _actor.Position).sqrMagnitude > maxDistanceSqr)
            {
                ClearTarget();
            }
        }

        public bool ToggleLock()
        {
            if (_hasTarget)
            {
                ClearTarget();
                return false;
            }

            return TryAcquireTarget();
        }

        public bool TryAcquireTarget()
        {
            if (IsTargetLocked)
            {
                return true;
            }

            if (_hasTarget)
            {
                ClearTarget();
            }

            if (!TrySelectTarget(0, false, out ICombatTarget target))
            {
                return false;
            }

            _rotationModeBeforeLock = _actor.RotationMode;
            SetTarget(target);
            _actor.SetRotationMode(ERotationMode.OrientToViewAndMovement);
            return true;
        }

        public bool SwitchTarget(int direction)
        {
            if (!IsTargetLocked)
            {
                return TryAcquireTarget();
            }

            int normalizedDirection = Math.Sign(direction);
            if (normalizedDirection == 0 ||
                !TrySelectTarget(normalizedDirection, true, out ICombatTarget target))
            {
                return false;
            }

            SetTarget(target);
            return true;
        }

        public void ClearTarget()
        {
            if (!_hasTarget)
            {
                return;
            }

            _hasTarget = false;
            CurrentTarget = null;
            _currentTarget = null;
            _currentTargetObject = null;
            if (_actor.RotationMode == ERotationMode.OrientToViewAndMovement)
            {
                _actor.SetRotationMode(_rotationModeBeforeLock);
            }
            TargetChanged?.Invoke(null);
        }

        public bool TryGetAttackAssistTarget(
            Vector3 origin,
            Vector3 forward,
            float maxDistance,
            float maxAngle,
            out ICombatTarget target)
        {
            target = null;
            if (maxDistance <= 0.0f || maxAngle <= 0.0f)
            {
                return false;
            }

            Vector3 up = _actor.Root.up;
            Vector3 planarForward = Vector3.ProjectOnPlane(forward, up);
            if (planarForward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            planarForward.Normalize();
            int hits = OverlapTargets(origin, maxDistance);
            if (hits == 0)
            {
                return false;
            }

            _visitedTargets.Clear();
            float bestScore = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                Collider candidateCollider = _overlapBuffer[i];
                _overlapBuffer[i] = null;
                if (candidateCollider == null ||
                    !TryResolveTarget(candidateCollider, out ICombatTarget candidateTarget))
                {
                    continue;
                }

                if (!_visitedTargets.Add(candidateTarget.TargetId))
                {
                    continue;
                }

                Vector3 offset = candidateTarget.AimPosition - origin;
                Vector3 planarOffset = Vector3.ProjectOnPlane(offset, up);
                float distance = planarOffset.magnitude;
                if (distance <= 0.0001f || distance > maxDistance)
                {
                    continue;
                }

                float angle = Vector3.Angle(planarForward, planarOffset / distance);
                if (angle > maxAngle ||
                    !HasLineOfSight(origin + up, candidateTarget))
                {
                    continue;
                }

                float score = angle / maxAngle + distance / maxDistance * 0.35f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = candidateTarget;
            }

            return target != null && target.TargetRoot != null;
        }

        public void Dispose()
        {
            Disable();
            TargetChanged = null;
        }

        private void OnLockOnRequested()
        {
            ToggleLock();
        }

        private void UpdateTargetSwitchInput()
        {
            float value = _input.TargetSwitch;
            float magnitude = Mathf.Abs(value);
            if (magnitude <= TargetSwitchReleaseThreshold)
            {
                _targetSwitchArmed = true;
                return;
            }

            if (!_targetSwitchArmed || magnitude < TargetSwitchActuationThreshold)
            {
                return;
            }

            _targetSwitchArmed = false;
            if (IsTargetLocked)
            {
                SwitchTarget(value < 0.0f ? -1 : 1);
            }
        }

        private bool TrySelectTarget(
            int switchDirection,
            bool excludeCurrent,
            out ICombatTarget selectedTarget)
        {
            selectedTarget = null;

            int hits = OverlapTargets(_actor.Position, MaxLockDistance);
            if (hits == 0)
            {
                return false;
            }

            _visitedTargets.Clear();
            float bestScore = float.MaxValue;
            float currentHorizontal = IsTargetLocked
                ? HorizontalViewPosition(AimPosition)
                : 0.0f;

            for (int i = 0; i < hits; i++)
            {
                Collider candidateCollider = _overlapBuffer[i];
                _overlapBuffer[i] = null;
                if (candidateCollider == null ||
                    !TryResolveTarget(candidateCollider, out ICombatTarget candidateTarget))
                {
                    continue;
                }

                int targetId = candidateTarget.TargetId;
                if (!_visitedTargets.Add(targetId) ||
                    (excludeCurrent && candidateTarget.TargetRoot == CurrentTarget))
                {
                    continue;
                }

                Vector3 aimPosition = candidateTarget.AimPosition;
                Vector3 viewDirection = aimPosition - _camera.Position;
                float distance = viewDirection.magnitude;
                if (distance <= 0.0001f)
                {
                    continue;
                }

                float viewAngle = Vector3.Angle(_camera.Forward, viewDirection / distance);
                if (viewAngle > MaxViewAngle ||
                    !HasLineOfSight(_camera.Position, candidateTarget))
                {
                    continue;
                }

                float score;
                if (switchDirection == 0)
                {
                    score = viewAngle + distance * 0.01f;
                }
                else
                {
                    float horizontal = HorizontalViewPosition(aimPosition);
                    float signedOffset = horizontal - currentHorizontal;
                    if (Mathf.Sign(signedOffset) != switchDirection || Mathf.Abs(signedOffset) < 0.001f)
                    {
                        continue;
                    }

                    score = Mathf.Abs(signedOffset) + viewAngle * 0.01f;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                selectedTarget = candidateTarget;
            }

            return selectedTarget != null && selectedTarget.TargetRoot != null;
        }

        private int OverlapTargets(Vector3 origin, float radius)
        {
            while (true)
            {
                int hits = Physics.OverlapSphereNonAlloc(
                    origin,
                    radius,
                    _overlapBuffer,
                    _targetMask,
                    QueryTriggerInteraction.Ignore);

                if (hits < _overlapBuffer.Length || _overlapBuffer.Length >= MaxBufferSize)
                {
                    return hits;
                }

                int newSize = Mathf.Min(_overlapBuffer.Length * 2, MaxBufferSize);
                _overlapBuffer = new Collider[newSize];
            }
        }

        private bool TryResolveTarget(Collider candidateCollider, out ICombatTarget target)
        {
            target = candidateCollider.GetComponentInParent<ICombatTarget>();
            UnityEngine.Object targetObject = target as UnityEngine.Object;
            return targetObject != null &&
                   target.IsTargetable &&
                   target.TargetRoot != null &&
                   target.TargetRoot != _actor.Root;
        }

        private bool HasLineOfSight(Vector3 origin, ICombatTarget target)
        {
            Vector3 offset = target.AimPosition - origin;
            float distance = offset.magnitude;
            if (distance <= 0.0001f ||
                !Physics.Raycast(
                    origin,
                    offset / distance,
                    out RaycastHit hit,
                    distance,
                    _visibilityMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return TryResolveTarget(hit.collider, out ICombatTarget visibleTarget) &&
                   visibleTarget.TargetId == target.TargetId;
        }

        private float HorizontalViewPosition(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - _camera.Position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0.0f;
            }

            return Vector3.Dot(direction.normalized, _camera.Right);
        }

        private void SetTarget(ICombatTarget target)
        {
            _currentTarget = target;
            _currentTargetObject = target as UnityEngine.Object;
            _hasTarget = true;
            CurrentTarget = target.TargetRoot;
            TargetChanged?.Invoke(CurrentTarget);
        }
    }
}
