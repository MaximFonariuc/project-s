using System;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _App
{
    public sealed class CinemachineCameraRig : IPlayerCameraRig, ICameraOrientation, IDisposable
    {
        private readonly CinemachineThirdPersonFollow _thirdPersonFollow;
        private readonly CinemachineCamera _camera;

        private bool _disposed;

        public Transform FollowTarget { get; }
        public Vector3 Position => FollowTarget.position;
        public Vector3 Forward => FollowTarget.forward;
        public Vector3 Right => FollowTarget.right;

        public float Distance
        {
            get => _thirdPersonFollow.CameraDistance;
            set => _thirdPersonFollow.CameraDistance = Mathf.Max(0.0f, value);
        }

        public float FieldOfView
        {
            get => _camera.Lens.FieldOfView;
            set
            {
                LensSettings lens = _camera.Lens;
                lens.FieldOfView = Mathf.Clamp(value, 1.0f, 179.0f);
                _camera.Lens = lens;
            }
        }

        public float ShoulderSide
        {
            get => _thirdPersonFollow.CameraSide;
            set => _thirdPersonFollow.CameraSide = Mathf.Clamp01(value);
        }

        public CinemachineCameraRig(
            CinemachineThirdPersonFollow cameraPrefab,
            Transform followTarget,
            float initialDistance)
        {
            if (cameraPrefab == null)
            {
                throw new ArgumentNullException(nameof(cameraPrefab));
            }

            FollowTarget = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));

            _thirdPersonFollow = Object.Instantiate(cameraPrefab);
            _thirdPersonFollow.name = $"{cameraPrefab.name} (Runtime)";
            _camera = _thirdPersonFollow.GetComponent<CinemachineCamera>();
            if (_camera == null)
            {
                Object.Destroy(_thirdPersonFollow.gameObject);
                throw new InvalidOperationException(
                    $"{cameraPrefab.name} must have {nameof(CinemachineCamera)} on the same GameObject.");
            }

            _camera.Follow = FollowTarget;
            Distance = initialDistance;
        }

        public void SetOrientation(Quaternion rotation)
        {
            FollowTarget.rotation = rotation;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_thirdPersonFollow != null)
            {
                Object.Destroy(_thirdPersonFollow.gameObject);
            }
        }
    }
}
