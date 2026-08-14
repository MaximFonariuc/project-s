using System;
using ModularInput;
using UnityEngine;

namespace _App
{
    public interface IPlayerInput
    {
        event Action<PlayerCombatCommandId> CombatActionRequested;
        event Action InteractRequested;
        event Action<InputSignalPhase> WalkChanged;
        event Action<InputSignalPhase> SprintChanged;
        event Action<InputSignalPhase> JumpChanged;
        event Action PauseRequested;
        event Action InverseGravityRequested;
        event Action CursorRequested;
        event Action LockOnRequested;
        event Action DodgeRequested;
        event Action SwitchCombatStateRequested;
        event Action<InputDeviceKind> ActiveDeviceChanged;

        Vector2 Move { get; }
        Vector2 Look { get; }
        Vector2 Zoom { get; }
        InputDeviceKind ActiveDevice { get; }

        void Enable();
        void Disable();
    }
}
