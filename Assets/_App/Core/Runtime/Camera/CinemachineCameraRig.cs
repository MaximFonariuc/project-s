using System;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _App
{
    internal sealed class CinemachineCameraRig :
        IPlayerCameraRig,
        ICameraOrientation,
        ICameraImpactFeedback,
        IDisposable
    {
        private readonly CinemachineThirdPersonFollow _thirdPersonFollow;
        private readonly CinemachineCamera _camera;
        private readonly CinemachineImpulseSource _impulseSource;

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
            float initialDistance,
            float impulseDuration)
        {
            if (cameraPrefab == null)
            {
                throw new ArgumentNullException(nameof(cameraPrefab));
            }

            FollowTarget = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            if (float.IsNaN(impulseDuration) || float.IsInfinity(impulseDuration) ||
                impulseDuration <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(impulseDuration));
            }

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
            ConfigureImpulseListener(_camera);
            _impulseSource = ConfigureImpulseSource(
                _camera,
                impulseDuration);
            Distance = initialDistance;
        }

        public void SetOrientation(Quaternion rotation)
        {
            FollowTarget.rotation = rotation;
        }

        void ICameraImpactFeedback.EmitImpact(
            Vector3 worldPoint,
            Vector3 impactDirection,
            float strength)
        {
            if (_disposed || strength <= 0.0f ||
                float.IsNaN(strength) || float.IsInfinity(strength))
            {
                return;
            }

            Vector3 direction = IsFinite(impactDirection) &&
                                impactDirection.sqrMagnitude > 0.0001f
                ? impactDirection.normalized
                : FollowTarget.forward;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                planarDirection.Normalize();
            }

            Vector3 velocity = (Vector3.down * 0.8f - planarDirection * 0.2f).normalized *
                               strength;
            _impulseSource.GenerateImpulseAtPositionWithVelocity(
                IsFinite(worldPoint) ? worldPoint : FollowTarget.position,
                velocity);
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

        private static void ConfigureImpulseListener(CinemachineCamera camera)
        {
            CinemachineImpulseListener listener =
                camera.GetComponent<CinemachineImpulseListener>() ??
                camera.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1.0f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode =
                CinemachineImpulseListener.SignalCombinationModes.UseLargest;
        }

        private static CinemachineImpulseSource ConfigureImpulseSource(
            CinemachineCamera camera,
            float duration)
        {
            var source = camera.gameObject.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseChannel = 1,
                ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                CustomImpulseShape = new AnimationCurve(),
                ImpulseDuration = duration,
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                DissipationDistance = 100.0f,
                DissipationRate = 0.25f,
                PropagationSpeed = 343.0f
            };
            source.DefaultVelocity = Vector3.down;
            return source;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
