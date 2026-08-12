using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _App
{
    public interface IInputHandler
    {
        public Action<EContextStates> OnCrouchChanged { get; set; }
        public Action<EContextStates> OnWalkingChanged { get; set; }
        public Action<EContextStates> OnSprintChanged { get; set; }
        public Action<EContextStates> OnJumpChanged { get; set; }
        
        public Action<ESkillType> OnSkillPerformed { get; set; }
        
        public Action OnSwitchCombatStatePerformed { get; set; }
        public Action OnInverseGravityPerformed { get; set; }
        public Action OnInteractPerformed { get; set; }
        public Action OnAttackPerformed { get; set; }
        public Action OnCursorPerformed { get; set; }
        public Action OnLockOnPerformed { get; set; }
        public Action OnPausePerformed { get; set; }
        public Action OnDodgePerformed { get; set; }
        
        public Vector2 MoveInput { get; }
        public Vector2 LookInput { get; }
        public Vector2 ZoomInput { get; }
        void OnEnable();
        void OnDisable();
    }

    public class InputHandler : IInputHandler, InputSystem.IPlayerActions
    {
        public Action<EContextStates> OnCrouchChanged { get; set; }
        public Action<EContextStates> OnWalkingChanged { get; set; }
        public Action<EContextStates> OnSprintChanged { get; set; }
        public Action<EContextStates> OnJumpChanged { get; set; }
        
        public Action<ESkillType> OnSkillPerformed { get; set; }
        
        public Action OnSwitchCombatStatePerformed { get; set; }
        public Action OnInverseGravityPerformed { get; set; }
        public Action OnInteractPerformed { get; set; }
        public Action OnAttackPerformed { get; set; }
        public Action OnCursorPerformed { get; set; }
        public Action OnLockOnPerformed { get; set; }
        public Action OnPausePerformed { get; set; }
        public Action OnDodgePerformed { get; set; }

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public Vector2 ZoomInput { get; private set; }

        private InputSystem _controls;

        public void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new InputSystem();
                _controls.Player.SetCallbacks(this);
            }

            _controls.Player.Enable();
        }

        public void OnDisable()
        {
            _controls.Player.Disable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnZoom(InputAction.CallbackContext context)
        {
            ZoomInput = context.ReadValue<Vector2>();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnAttackPerformed.Invoke();
            }
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnInteractPerformed.Invoke();
            }
        }

        public void OnWalk(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                OnWalkingChanged.Invoke(EContextStates.Started);
            }
            else if (context.performed)
            {
                OnWalkingChanged.Invoke(EContextStates.Performed);
            }
            else if (context.canceled)
            {
                OnWalkingChanged.Invoke(EContextStates.Canceled);
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                OnJumpChanged.Invoke(EContextStates.Started);
            }
            else if (context.performed)
            {
                OnJumpChanged.Invoke(EContextStates.Performed);
            }
            else if (context.canceled)
            {
                OnJumpChanged.Invoke(EContextStates.Canceled);
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                OnSprintChanged.Invoke(EContextStates.Started);
            }
            else if (context.canceled)
            {
                OnSprintChanged.Invoke(EContextStates.Canceled);
            }
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnPausePerformed.Invoke();
            }        
        }

        public void OnInverseGravity(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnInverseGravityPerformed.Invoke();
            }        
        }

        public void OnCursor(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnCursorPerformed.Invoke();
            }
        }

        public void OnSkill(InputAction.CallbackContext context)
        {
            if (!context.performed || !int.TryParse(context.control.name, out var skillNumber))
            {
                return;
            }

            OnSkillPerformed.Invoke((ESkillType)skillNumber);
        }

        public void OnLockOn(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnLockOnPerformed.Invoke();
            }
        }

        public void OnDodge(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnDodgePerformed.Invoke();
            }
        }

        public void OnSwitchCombatState(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnSwitchCombatStatePerformed.Invoke();
            }
        }

        public void OnSpecialAttack(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            OnSkillPerformed.Invoke(ESkillType.Special);        
        }
    }

    public enum EContextStates
    {
        Started = 0,
        Performed = 1,
        Canceled = 2
    }
}