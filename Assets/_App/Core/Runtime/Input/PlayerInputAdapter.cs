using System;
using ModularInput;
using UnityEngine;

namespace _App
{
    public sealed class PlayerInputAdapter : IPlayerInput, IDisposable
    {
        private readonly IInputService _input;
        private bool _enabled;

        public event Action<PlayerCombatCommandId> CombatActionRequested;
        public event Action InteractRequested;
        public event Action<InputSignalPhase> WalkChanged;
        public event Action<InputSignalPhase> SprintChanged;
        public event Action<InputSignalPhase> JumpChanged;
        public event Action PauseRequested;
        public event Action InverseGravityRequested;
        public event Action CursorRequested;
        public event Action LockOnRequested;
        public event Action DodgeRequested;
        public event Action SwitchCombatStateRequested;
        public event Action<InputDeviceKind> ActiveDeviceChanged;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public Vector2 Zoom { get; private set; }
        public float TargetSwitch { get; private set; }
        public InputDeviceKind ActiveDevice { get; private set; }

        public PlayerInputAdapter(IInputService input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _input.SignalChanged += OnSignalChanged;
            _input.Start();

            if (!_input.SetMapEnabled(PlayerInputIds.PlayerMap, true))
            {
                _input.SignalChanged -= OnSignalChanged;
                _input.Stop();
                throw new InvalidOperationException("Player input map was not found in the configured action asset.");
            }

            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.SetMapEnabled(PlayerInputIds.PlayerMap, false);
            _input.SignalChanged -= OnSignalChanged;
            _input.Stop();

            Move = Vector2.zero;
            Look = Vector2.zero;
            Zoom = Vector2.zero;
            TargetSwitch = 0.0f;
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnSignalChanged(InputSignal signal)
        {
            if (signal.MapId != PlayerInputIds.PlayerMap)
            {
                return;
            }

            UpdateActiveDevice(signal);

            InputActionId action = signal.ActionId;
            if (action == PlayerInputIds.Move)
            {
                Move = signal.Value.AsVector2();
            }
            else if (action == PlayerInputIds.Look)
            {
                Look = signal.Value.AsVector2();
            }
            else if (action == PlayerInputIds.Zoom)
            {
                Zoom = signal.Value.AsVector2();
            }
            else if (action == PlayerInputIds.TargetSwitch)
            {
                TargetSwitch = signal.Value.AsAxis();
            }
            else if (action == PlayerInputIds.Walk)
            {
                WalkChanged?.Invoke(signal.Phase);
            }
            else if (action == PlayerInputIds.Jump)
            {
                JumpChanged?.Invoke(signal.Phase);
            }
            else if (action == PlayerInputIds.Sprint)
            {
                SprintChanged?.Invoke(signal.Phase);
            }
            else if (signal.Phase == InputSignalPhase.Performed)
            {
                DispatchPerformed(action);
            }
        }

        private void DispatchPerformed(InputActionId action)
        {
            if (action == PlayerInputIds.Attack)
            {
                CombatActionRequested?.Invoke(PlayerCombatCommandIds.LightAttack);
            }
            else if (action == PlayerInputIds.HeavyAttack)
            {
                CombatActionRequested?.Invoke(PlayerCombatCommandIds.HeavyAttack);
            }
            else if (action == PlayerInputIds.Interact)
            {
                InteractRequested?.Invoke();
            }
            else if (action == PlayerInputIds.Pause)
            {
                PauseRequested?.Invoke();
            }
            else if (action == PlayerInputIds.InverseGravity)
            {
                InverseGravityRequested?.Invoke();
            }
            else if (action == PlayerInputIds.Cursor)
            {
                CursorRequested?.Invoke();
            }
            else if (action == PlayerInputIds.SkillBoneCrash)
            {
                CombatActionRequested?.Invoke(PlayerCombatCommandIds.BoneCrash);
            }
            else if (action == PlayerInputIds.SkillFlashStance)
            {
                CombatActionRequested?.Invoke(PlayerCombatCommandIds.FlashStance);
            }
            else if (action == PlayerInputIds.SpecialAttack)
            {
                CombatActionRequested?.Invoke(PlayerCombatCommandIds.Special);
            }
            else if (action == PlayerInputIds.LockOn)
            {
                LockOnRequested?.Invoke();
            }
            else if (action == PlayerInputIds.Dodge)
            {
                DodgeRequested?.Invoke();
            }
            else if (action == PlayerInputIds.SwitchCombatState)
            {
                SwitchCombatStateRequested?.Invoke();
            }
        }

        private void UpdateActiveDevice(InputSignal signal)
        {
            if (signal.Phase == InputSignalPhase.Canceled ||
                signal.DeviceKind == InputDeviceKind.Unknown ||
                signal.DeviceKind == ActiveDevice)
            {
                return;
            }

            ActiveDevice = signal.DeviceKind;
            ActiveDeviceChanged?.Invoke(ActiveDevice);
        }
    }
}
