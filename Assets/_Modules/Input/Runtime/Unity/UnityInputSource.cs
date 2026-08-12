using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace ModularInput.Unity
{
    public sealed class UnityInputSource : IInputSource, IInputBindingOverrides
    {
        private static readonly InputSourceId DefaultSourceId = new InputSourceId("UnityInputSystem");

        private readonly InputActionAsset _actions;
        private bool _disposed;

        public event Action<InputSignal> SignalEmitted;

        public InputSourceId SourceId { get; }
        public bool IsRunning { get; private set; }

        public UnityInputSource(InputActionAsset actions)
            : this(actions, DefaultSourceId) { }

        public UnityInputSource(InputActionAsset actions, InputSourceId sourceId)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (!sourceId.IsValid)
            {
                throw new ArgumentException("Input source id must be valid.", nameof(sourceId));
            }

            SourceId = sourceId;
            _actions = UnityEngine.Object.Instantiate(actions);
            _actions.name = $"{actions.name} (Runtime)";
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return;
            }

            foreach (InputAction action in _actions)
            {
                action.started += OnStarted;
                action.performed += OnPerformed;
                action.canceled += OnCanceled;
            }

            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _actions.Disable();
            foreach (InputAction action in _actions)
            {
                action.started -= OnStarted;
                action.performed -= OnPerformed;
                action.canceled -= OnCanceled;
            }

            IsRunning = false;
        }

        public bool SetMapEnabled(InputMapId mapId, bool enabled)
        {
            ThrowIfDisposed();

            InputActionMap map = _actions.FindActionMap(mapId.Value);
            if (map == null)
            {
                return false;
            }

            if (enabled)
            {
                map.Enable();
            }
            else
            {
                map.Disable();
            }

            return true;
        }

        public void DisableAllMaps()
        {
            if (!_disposed)
            {
                _actions.Disable();
            }
        }

        public bool ApplyBindingOverride(InputActionId actionId, int bindingIndex, string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath) || !TryGetBinding(actionId, bindingIndex, out InputAction action))
            {
                return false;
            }

            action.ApplyBindingOverride(bindingIndex, controlPath);
            return true;
        }

        public bool RemoveBindingOverride(InputActionId actionId, int bindingIndex)
        {
            if (!TryGetBinding(actionId, bindingIndex, out InputAction action))
            {
                return false;
            }

            action.RemoveBindingOverride(bindingIndex);
            return true;
        }

        public string SaveBindingOverrides()
        {
            ThrowIfDisposed();
            return _actions.SaveBindingOverridesAsJson();
        }

        public bool LoadBindingOverrides(string json, bool removeExisting = true)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(json))
            {
                if (removeExisting)
                {
                    _actions.RemoveAllBindingOverrides();
                }

                return true;
            }

            _actions.LoadBindingOverridesFromJson(json, removeExisting);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            if (_actions != null)
            {
                UnityEngine.Object.Destroy(_actions);
            }

            _disposed = true;
        }

        private void OnStarted(InputAction.CallbackContext context) => Emit(context, InputSignalPhase.Started);
        private void OnPerformed(InputAction.CallbackContext context) => Emit(context, InputSignalPhase.Performed);
        private void OnCanceled(InputAction.CallbackContext context) => Emit(context, InputSignalPhase.Canceled);

        private void Emit(InputAction.CallbackContext context, InputSignalPhase phase)
        {
            InputAction action = context.action;
            InputActionMap map = action.actionMap;
            InputControl control = context.control;

            InputSignal signal = new InputSignal(
                SourceId,
                new InputMapId(map.id),
                new InputActionId(action.id),
                ReadValue(context, phase),
                phase,
                GetDeviceKind(control?.device),
                context.time,
                Time.frameCount,
                map.name,
                action.name,
                control?.path);

            SignalEmitted?.Invoke(signal);
        }

        private static InputValue ReadValue(InputAction.CallbackContext context, InputSignalPhase phase)
        {
            if (phase == InputSignalPhase.Canceled)
            {
                return default;
            }

            if (context.action.type == InputActionType.Button || context.valueType == typeof(bool))
            {
                return InputValue.FromButton(context.ReadValueAsButton());
            }

            Type valueType = context.valueType;
            if (valueType == typeof(float))
            {
                return InputValue.FromAxis(context.ReadValue<float>());
            }

            if (valueType == typeof(Vector2))
            {
                return InputValue.FromVector2(context.ReadValue<Vector2>());
            }

            if (valueType == typeof(Vector3))
            {
                return InputValue.FromVector3(context.ReadValue<Vector3>());
            }

            if (valueType == typeof(Quaternion))
            {
                return InputValue.FromQuaternion(context.ReadValue<Quaternion>());
            }

            return default;
        }

        private static InputDeviceKind GetDeviceKind(InputDevice device)
        {
            if (device == null)
            {
                return InputDeviceKind.Unknown;
            }

            if (!device.native)
            {
                return InputDeviceKind.Virtual;
            }

            if (device is Keyboard || device is Mouse)
            {
                return InputDeviceKind.KeyboardMouse;
            }

            if (device is Gamepad)
            {
                return InputDeviceKind.Gamepad;
            }

            if (device is Touchscreen)
            {
                return InputDeviceKind.Touch;
            }

            if (device is Joystick)
            {
                return InputDeviceKind.Joystick;
            }

            if (device is XRController)
            {
                return InputDeviceKind.XR;
            }

            return InputDeviceKind.Unknown;
        }

        private bool TryGetBinding(InputActionId actionId, int bindingIndex, out InputAction action)
        {
            ThrowIfDisposed();

            action = _actions.FindAction(actionId.Value);
            return action != null && bindingIndex >= 0 && bindingIndex < action.bindings.Count;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnityInputSource));
            }
        }
    }
}
