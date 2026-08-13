using System;
using Unity.Cinemachine;
using UnityEngine;

namespace _App
{
    public class PlayerTargetLockSystem : TargetLockSystemBase
    {
        private readonly CinemachineCamera _lockOnCamera;

        private const float MAX_VIEW_ANGLE = 35f;

        public PlayerTargetLockSystem(Character character, CinemachineCamera lockOnCamera) 
            : base(character, "Enemy")
        {
            _lockOnCamera = lockOnCamera;
        }

        public override void AcquireTarget(Action<ERotationMode> onModeChanged = null)
        {
            if (IsTargetLocked)
            {
                _lockOnCamera.LookAt = null;
                ClearTarget();
                onModeChanged?.Invoke(ERotationMode.OrientRotationToMovement);
                return;
            }

            Transform bestTarget = FindBestTarget();
            if (bestTarget != null)
            {
                CurrentTargetInternal = bestTarget;
                _lockOnCamera.LookAt = CurrentTargetInternal;
                onModeChanged?.Invoke(ERotationMode.OrientToViewAndMovement);
            }
        }

        private Transform FindBestTarget()
        {
            int hits = TryOverlapWithAutoResize();
            var cam = Camera.main;
            if (hits == 0 || cam == null)
                return null;

            Transform best = null;
            float bestScore = float.MaxValue;
            Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);

            for (int i = 0; i < hits; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;
                OverlapBuffer[i] = null;

                Transform cand = col.transform;
                if (!ValidateInView(cam, cand)) continue;

                Vector3 screen = cam.WorldToScreenPoint(cand.position);
                float dist = Vector2.Distance(screen, screenCenter);
                if (dist < bestScore)
                {
                    bestScore = dist;
                    best = cand;
                }
            }
            return best;
        }

        private bool ValidateInView(Camera cam, Transform candidate)
        {
            Vector3 toTarget = (candidate.position - cam.transform.position).normalized;
            float angle = Vector3.Angle(cam.transform.forward, toTarget);
            return angle <= MAX_VIEW_ANGLE;
        }
    }
}