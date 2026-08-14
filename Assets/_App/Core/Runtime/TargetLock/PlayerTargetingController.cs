using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    public sealed class PlayerTargetingController :
        IPlayerTargetingState,
        IPlayerTargetingCommands,
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
        private readonly HashSet<int> _visitedTargets = new HashSet<int>();

        private Collider[] _overlapBuffer = new Collider[InitialBufferSize];
        private Collider _currentTargetCollider;
        private ERotationMode _rotationModeBeforeLock;
        private bool _hasTarget;
        private bool _targetSwitchArmed;
        private bool _enabled;

        public event Action<Transform> TargetChanged;

        public bool IsTargetLocked => _hasTarget && CurrentTarget != null;
        public Transform CurrentTarget { get; private set; }
        public Vector3 AimPosition => _currentTargetCollider != null
            ? _currentTargetCollider.bounds.center
            : CurrentTarget != null
                ? CurrentTarget.position
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

            if (CurrentTarget == null)
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
            if (IsTargetLocked)
            {
                ClearTarget();
                return false;
            }

            return TryAcquireTarget();
        }

        public bool TryAcquireTarget()
        {
            if (!TrySelectTarget(0, false, out Transform target, out Collider targetCollider))
            {
                return false;
            }

            _rotationModeBeforeLock = _actor.RotationMode;
            SetTarget(target, targetCollider);
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
                !TrySelectTarget(normalizedDirection, true, out Transform target, out Collider targetCollider))
            {
                return false;
            }

            SetTarget(target, targetCollider);
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
            _currentTargetCollider = null;
            if (_actor.RotationMode == ERotationMode.OrientToViewAndMovement)
            {
                _actor.SetRotationMode(_rotationModeBeforeLock);
            }
            TargetChanged?.Invoke(null);
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
            out Transform selectedTarget,
            out Collider selectedCollider)
        {
            selectedTarget = null;
            selectedCollider = null;

            int hits = OverlapTargets();
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
                    !TryResolveTarget(
                        candidateCollider,
                        out Transform candidateTarget,
                        out Collider aimCollider))
                {
                    continue;
                }

                int targetId = candidateTarget.GetInstanceID();
                if (!_visitedTargets.Add(targetId) ||
                    (excludeCurrent && candidateTarget == CurrentTarget))
                {
                    continue;
                }

                Vector3 aimPosition = aimCollider.bounds.center;
                Vector3 viewDirection = aimPosition - _camera.Position;
                float distance = viewDirection.magnitude;
                if (distance <= 0.0001f)
                {
                    continue;
                }

                float viewAngle = Vector3.Angle(_camera.Forward, viewDirection / distance);
                if (viewAngle > MaxViewAngle)
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
                selectedCollider = aimCollider;
            }

            return selectedTarget != null;
        }

        private int OverlapTargets()
        {
            while (true)
            {
                int hits = Physics.OverlapSphereNonAlloc(
                    _actor.Position,
                    MaxLockDistance,
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

        private bool TryResolveTarget(
            Collider candidateCollider,
            out Transform target,
            out Collider aimCollider)
        {
            Character candidateCharacter = candidateCollider.GetComponentInParent<Character>();
            if (candidateCharacter != null)
            {
                target = candidateCharacter.transform;
                aimCollider = candidateCharacter.GetComponent<Collider>();
                if (aimCollider == null)
                {
                    aimCollider = candidateCollider;
                }

                return target != _actor.Root;
            }

            target = candidateCollider.transform.root;
            aimCollider = candidateCollider;
            return target != null && target != _actor.Root;
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

        private void SetTarget(Transform target, Collider targetCollider)
        {
            _hasTarget = true;
            CurrentTarget = target;
            _currentTargetCollider = targetCollider;
            TargetChanged?.Invoke(target);
        }
    }
}
