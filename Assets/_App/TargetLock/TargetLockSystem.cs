using System;
using Unity.Cinemachine;
using UnityEngine;

namespace _App
{
    public class TargetLockSystem
    {
        public const float MAX_LOCK_DISTANCE = 25f;
        private const int INITIAL_BUFFER_SIZE = 5;
        private const float MAX_VIEW_ANGLE = 35f;
        private const float SMOOTH_SPEED = 10f;

        private readonly CinemachineCamera _lockOnCamera;
        private readonly LayerMask _targetLayerMask;
        private readonly Character _character;

        private Transform _currentTarget;
        private Collider[] _overlapBuffer;
        
        public Transform CurrentTarget => _currentTarget;
        public bool IsTargetLocked => _currentTarget != null;
        
        public TargetLockSystem(Character character, CinemachineCamera lockOnCamera)
        {
            _character = character;
            _lockOnCamera = lockOnCamera;

            _targetLayerMask = LayerMask.GetMask("Enemy");
            _overlapBuffer = new Collider[INITIAL_BUFFER_SIZE];
        }

        public void TargetLock(Action<ERotationMode> onModeChanged)
        {
            if (IsTargetLocked)
            {
                DisableLockOn();
            }
            else
            {
                FindNewTarget();
            }

            ERotationMode mode = IsTargetLocked ? ERotationMode.OrientToViewAndMovement : ERotationMode.OrientRotationToMovement;
            onModeChanged?.Invoke(mode);
        }
        
        public void SwitchTarget()
        {
            if (_currentTarget == null)
            {
                FindNewTarget();
                return;
            }

            _currentTarget = SelectBestTarget(TargetSelectionMode.NextFromCurrent);

            if (_currentTarget != null)
                _lockOnCamera.LookAt = _currentTarget;
        }
        
        private void FindNewTarget()
        {
            _currentTarget = SelectBestTarget(TargetSelectionMode.ClosestToCenter);

            if (_currentTarget != null)
            {
                EnableLockOn();
            }
        }
        
        private Transform SelectBestTarget(TargetSelectionMode mode)
        {
            int hits = TryOverlapWithAutoResize();
            var cam = Camera.main;

            if (hits == 0 || cam == null)
            {
                return null;
            }

            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 currentDir = Vector2.zero;

            if (_currentTarget != null)
            {
                Vector3 curScreenPos = cam.WorldToScreenPoint(_currentTarget.position);
                currentDir = curScreenPos - (Vector3)screenCenter;
            }

            for (int i = 0; i < hits; i++)
            {
                Collider col = _overlapBuffer[i];
                _overlapBuffer[i] = null;

                Transform candidate = col.transform;
                
                if (candidate == _currentTarget)
                {
                    continue;
                }

                if (!ValidateInView(cam, candidate))
                {
                    continue;
                }

                Vector3 screenPos = cam.WorldToScreenPoint(candidate.position);
                Vector2 dirFromCenter = screenPos - (Vector3)screenCenter;

                switch (mode)
                {
                    case TargetSelectionMode.ClosestToCenter:
                        float distance = Vector2.Distance(screenPos, screenCenter);
                        if (distance < bestScore)
                        {
                            bestScore = distance;
                            bestTarget = candidate;
                        }
                        break;

                    case TargetSelectionMode.NextFromCurrent:
                        if (_currentTarget == null)
                        {
                            continue;
                        }

                        float angleBetween = Vector2.SignedAngle(currentDir, dirFromCenter);
                        if (angleBetween > 0 && angleBetween < bestScore)
                        {
                            bestScore = angleBetween;
                            bestTarget = candidate;
                        }
                        break;
                }
            }

            return bestTarget;
        }

        private int TryOverlapWithAutoResize()
        {
            int hits;
            while (true)
            {
                hits = Physics.OverlapSphereNonAlloc(
                    _character.transform.position,
                    MAX_LOCK_DISTANCE,
                    _overlapBuffer,
                    _targetLayerMask,
                    QueryTriggerInteraction.Ignore
                );

                if (hits < _overlapBuffer.Length)
                {
                    break;
                }

                int newSize = _overlapBuffer.Length * 2;
                _overlapBuffer = new Collider[newSize];
            }
            return hits;
        }
        
        private bool ValidateInView(Camera cam, Transform candidate)
        {
            Vector3 toTarget = (candidate.position - cam.transform.position).normalized;
            float angle = Vector3.Angle(cam.transform.forward, toTarget);
            return angle <= MAX_VIEW_ANGLE;
        }

        private void EnableLockOn()
        {
            _lockOnCamera.LookAt = _currentTarget;
        }

        private void DisableLockOn()
        {
            _lockOnCamera.LookAt = null;
            _currentTarget = null;
        }
        
        private enum TargetSelectionMode
        {
            ClosestToCenter,
            NextFromCurrent
        }
    }
}
