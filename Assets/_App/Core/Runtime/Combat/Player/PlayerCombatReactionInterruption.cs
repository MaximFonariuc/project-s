using System;

namespace _App
{
    internal sealed class PlayerCombatReactionInterruption : ICharacterReactionInterruption
    {
        private readonly IPlayerCombatCommands _combat;

        public PlayerCombatReactionInterruption(IPlayerCombatCommands combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public void InterruptCurrentAction()
        {
            _combat.InterruptAttack();
        }
    }
}
