using System;
using UnityEngine;

namespace _App
{
    public sealed class PlayerCameraController :
        IPlayerCameraCommands,
        IPlayerCameraState,
        IDisposable
    {
        private const float DistanceSmoothTime = 0.1f;
        private const float FieldOfViewSmoothTime = 0.15f;
        private const float ShoulderSmoothTime = 0.15f;
        private const float TargetRotationSmoothTime = 0.08f;
        private const float DefaultRecenterPitch = 10.0f;

        private readonly IPlayerInput _input;
        private readonly IPlayerActorTransform _actor;
        private readonly IPlayerCameraRig _rig;
        private readonly IPlayerTargetingState _targeting;
        private readonly IPlayerCameraPresentation _presentation;
        private readonly PlayerCameraSettings _settings;

        private float _targetYaw;
        private float _targetPitch;
        private float _yawSmoothVelocity;
        private float _pitchSmoothVelocity;
        private float _userDistance;
        private float _distanceOffset;
        private float _distanceSmoothVelocity;
        private float _baseFieldOfView;
        private float _fieldOfViewOffset;
        private float _fieldOfViewSmoothVelocity;
        private float _targetShoulderSide;
        private float _shoulderSmoothVelocity;
        private bool _enabled;

        public event Action<PlayerCameraMode> ModeChanged;

        public PlayerCameraMode Mode { get; private set; }
        public bool IsCursorActive { get; private set; }
        public bool IsGravityInverted { get; private set; }

        public PlayerCameraController(
            IPlayerInput input,
            IPlayerActorTransform actor,
            IPlayerCameraRig rig,
            IPlayerTargetingState targeting,
            IPlayerCameraPresentation presentation,
            PlayerCameraSettings settings)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _rig = rig ?? throw new ArgumentNullException(nameof(rig));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _settings = settings;

            Vector3 initialAngles = _rig.FollowTarget.rotation.eulerAngles;
            _targetYaw = NormalizeAngle(initialAngles.y);
            _targetPitch = Mathf.Clamp(
                NormalizeAngle(initialAngles.x),
                _settings.MinPitch,
                _settings.MaxPitch);
            _userDistance = _settings.DefaultDistance;
            _rig.Distance = _userDistance;
            _baseFieldOfView = _rig.FieldOfView;
            _targetShoulderSide = _rig.ShoulderSide;
            Mode = PlayerCameraMode.FreeLook;
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _input.CursorRequested += ToggleCursor;
            _enabled = true;
            SetCursorActive(false);
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.CursorRequested -= ToggleCursor;
            _enabled = false;
            SetMode(PlayerCameraMode.FreeLook);
            SetCursorActive(false);
        }

        public void LateTick(float unscaledDeltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            bool hasTarget = _targeting.IsTargetLocked && _targeting.CurrentTarget != null;
            SetMode(hasTarget ? PlayerCameraMode.TargetLock : PlayerCameraMode.FreeLook);

            if (!IsCursorActive)
            {
                if (hasTarget)
                {
                    TrackTarget(_targeting.AimPosition, unscaledDeltaTime);
                }
                else
                {
                    ApplyFreeLookInput();
                }

                ApplyZoomInput();
                _rig.SetOrientation(Quaternion.Euler(
                    _targetPitch,
                    _targetYaw,
                    IsGravityInverted ? -180.0f : 0.0f));
            }

            float targetDistance = Mathf.Clamp(
                _userDistance + _distanceOffset,
                _settings.MinDistance,
                _settings.MaxDistance);
            _rig.Distance = Mathf.SmoothDamp(
                _rig.Distance,
                targetDistance,
                ref _distanceSmoothVelocity,
                DistanceSmoothTime,
                Mathf.Infinity,
                unscaledDeltaTime);

            float targetFieldOfView = Mathf.Clamp(_baseFieldOfView + _fieldOfViewOffset, 1.0f, 179.0f);
            _rig.FieldOfView = Mathf.SmoothDamp(
                _rig.FieldOfView,
                targetFieldOfView,
                ref _fieldOfViewSmoothVelocity,
                FieldOfViewSmoothTime,
                Mathf.Infinity,
                unscaledDeltaTime);

            _rig.ShoulderSide = Mathf.SmoothDamp(
                _rig.ShoulderSide,
                _targetShoulderSide,
                ref _shoulderSmoothVelocity,
                ShoulderSmoothTime,
                Mathf.Infinity,
                unscaledDeltaTime);

        }

        public void TickPresentation(float deltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            bool hasTarget =
                _targeting.IsTargetLocked && _targeting.CurrentTarget != null;
            _presentation.Tick(
                hasTarget,
                _targeting.AimPosition,
                Mathf.Max(0.0f, deltaTime));
        }

        public void Recenter()
        {
            Vector3 forward = Vector3.ProjectOnPlane(_actor.Forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _targetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            _targetPitch = Mathf.Clamp(DefaultRecenterPitch, _settings.MinPitch, _settings.MaxPitch);
            _yawSmoothVelocity = 0.0f;
            _pitchSmoothVelocity = 0.0f;
        }

        public void SwapShoulder()
        {
            _targetShoulderSide = _targetShoulderSide >= 0.5f ? 0.0f : 1.0f;
        }

        public void SetFraming(float distanceOffset, float fieldOfViewOffset)
        {
            _distanceOffset = distanceOffset;
            _fieldOfViewOffset = fieldOfViewOffset;
        }

        public void ClearFraming()
        {
            _distanceOffset = 0.0f;
            _fieldOfViewOffset = 0.0f;
        }

        public void ToggleGravityOrientation()
        {
            IsGravityInverted = !IsGravityInverted;
        }

        public void Dispose()
        {
            Disable();
            ModeChanged = null;
        }

        private void ApplyFreeLookInput()
        {
            Vector2 look = Vector2.Scale(_input.Look, _settings.LookSensitivity);
            bool useDefaultAxes = IsGravityInverted
                ? !_settings.InvertLook
                : _settings.InvertLook;

            float yaw = useDefaultAxes ? look.x : -look.x;
            float pitch = useDefaultAxes ? -look.y : look.y;

            if (!Mathf.Approximately(yaw, 0.0f))
            {
                _targetYaw = MathLib.ClampAngle(_targetYaw + yaw, -180.0f, 180.0f);
            }

            if (!Mathf.Approximately(pitch, 0.0f))
            {
                _targetPitch = MathLib.ClampAngle(
                    _targetPitch + pitch,
                    _settings.MinPitch,
                    _settings.MaxPitch);
            }
        }

        private void ApplyZoomInput()
        {
            float zoom = _input.Zoom.y * _settings.LookSensitivity.y;
            if (Mathf.Approximately(zoom, 0.0f))
            {
                return;
            }

            _userDistance = Mathf.Clamp(
                _userDistance - zoom,
                _settings.MinDistance,
                _settings.MaxDistance);
        }

        private void TrackTarget(Vector3 targetPosition, float deltaTime)
        {
            Vector3 direction = targetPosition - _rig.Position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 angles = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles;
            float yaw = NormalizeAngle(angles.y);
            float pitch = Mathf.Clamp(
                NormalizeAngle(angles.x),
                _settings.MinPitch,
                _settings.MaxPitch);

            _targetYaw = Mathf.SmoothDampAngle(
                _targetYaw,
                yaw,
                ref _yawSmoothVelocity,
                TargetRotationSmoothTime,
                Mathf.Infinity,
                deltaTime);
            _targetPitch = Mathf.SmoothDampAngle(
                _targetPitch,
                pitch,
                ref _pitchSmoothVelocity,
                TargetRotationSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private void ToggleCursor()
        {
            SetCursorActive(!IsCursorActive);
        }

        private void SetCursorActive(bool active)
        {
            if (IsCursorActive != active)
            {
                ResetAngularSmoothing();
            }

            IsCursorActive = active;
            Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = active;
        }

        private void SetMode(PlayerCameraMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            ResetAngularSmoothing();
            SafeEvent.Publish(ModeChanged, mode, _actor.Root);
        }

        private void ResetAngularSmoothing()
        {
            _yawSmoothVelocity = 0.0f;
            _pitchSmoothVelocity = 0.0f;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180.0f ? angle - 360.0f : angle;
        }
    }
}
