namespace _App
{
    internal static class PlayerActionArbitration
    {
        public static bool HasExternalActionLock(
            bool reactionBlocksActions,
            bool attackRecoilActive)
        {
            return reactionBlocksActions || attackRecoilActive;
        }

        public static PlayerMovementIntent ResolveNonDefenseIntent(
            bool externalActionLock,
            in PlayerMovementIntent combatIntent)
        {
            return externalActionLock ? PlayerMovementIntent.Locked : combatIntent;
        }

        public static bool CanStartDefense(
            bool externalActionLock,
            bool locomotionBlocksActions,
            bool jumpStarting,
            bool dodgeStarting,
            bool locomotionHasPriorityRequest,
            PlayerCombatPhase combatPhase,
            bool combatAllowsGuard)
        {
            if (externalActionLock ||
                locomotionBlocksActions ||
                jumpStarting ||
                dodgeStarting ||
                locomotionHasPriorityRequest)
            {
                return false;
            }

            return combatPhase == PlayerCombatPhase.Ready || combatAllowsGuard;
        }

        public static PlayerMovementIntent ResolveLocomotionIntent(
            bool externalActionLock,
            bool defenseBlocksActions,
            in PlayerMovementIntent defenseIntent,
            in PlayerMovementIntent combatIntent)
        {
            if (externalActionLock)
            {
                return PlayerMovementIntent.Locked;
            }

            return defenseBlocksActions ? defenseIntent : combatIntent;
        }
    }
}
