using ModularStateMachine;

namespace _App
{
    internal sealed class PlayerCombatReadyState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }
    }

    internal abstract class PlayerCombatActionState : IState<PlayerCombatAction>
    {
        public PlayerCombatAction Action { get; private set; }

        public void Enter(PlayerCombatAction action)
        {
            Action = action;
        }

        public void Exit()
        {
            Action = null;
        }
    }

    internal sealed class PlayerCombatExecutingState : PlayerCombatActionState
    {
    }

    internal sealed class PlayerCombatRecoveryState : PlayerCombatActionState
    {
    }

    internal sealed class PlayerCombatWeaponSwitchState : IState<EWeaponType>
    {
        public EWeaponType TargetWeapon { get; private set; }

        public void Enter(EWeaponType targetWeapon)
        {
            TargetWeapon = targetWeapon;
        }

        public void Exit()
        {
            TargetWeapon = EWeaponType.None;
        }
    }

    internal sealed class PlayerCombatInterruptedState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }
    }
}
