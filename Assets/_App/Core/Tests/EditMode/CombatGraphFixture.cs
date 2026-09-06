using System.Collections.Generic;
using _App;

namespace ProjectS.Core.Tests
{
    internal static class CombatGraphFixture
    {
        public static readonly PlayerCombatCommandId Light =
            new PlayerCombatCommandId("attack.light");
        public static readonly PlayerCombatCommandId Heavy =
            new PlayerCombatCommandId("attack.heavy");

        public static PlayerCombatActionId Action(string id) =>
            new PlayerCombatActionId(id);

        public static CombatActionDefinition Definition(
            NormalizedActionWindow chain = default,
            NormalizedActionWindow cancel = default,
            NormalizedActionWindow hitbox = default,
            float cooldown = 0.0f)
        {
            if (!hitbox.IsEnabled)
            {
                hitbox = new NormalizedActionWindow(0.2f, 0.35f);
            }

            var hit = new CombatHitDefinition(
                10,
                1.0f,
                0.0f,
                0.0f,
                CombatReactionIds.Light,
                CombatHitStop.Disabled,
                CombatHitRepeatPolicy.OncePerAction);

            return new CombatActionDefinition(
                hitbox,
                chain,
                cancel,
                NormalizedActionWindow.Disabled,
                NormalizedActionWindow.Disabled,
                hit,
                1.0f,
                0.8f,
                1.0f,
                false,
                0.0f,
                cooldown,
                PlayerMovementIntent.Locked,
                CombatMotionDefinition.Disabled);
        }

        public static PlayerCombatAction Attack(
            string id,
            PlayerCombatEnvironment environment = PlayerCombatEnvironment.Grounded,
            int comboIndex = 0,
            CombatActionDefinition? definition = null)
        {
            return new PlayerCombatAction(
                Action(id),
                environment,
                environment == PlayerCombatEnvironment.Airborne
                    ? PlayerCombatPresentation.AirCombo(comboIndex)
                    : PlayerCombatPresentation.Combo(comboIndex),
                definition ?? Definition());
        }

        public static PlayerCombatActionGraph Graph(
            IEnumerable<PlayerCombatAction> actions,
            IEnumerable<PlayerCombatEntryRule> entries,
            IEnumerable<PlayerCombatTransitionRule> transitions)
        {
            return new PlayerCombatActionGraph(actions, entries, transitions);
        }

        public static CombatActionOutcome Outcome() => new CombatActionOutcome();

        public static CombatActionFrame Frame(
            float normalizedTime = 0.5f,
            bool canChain = false,
            bool canCancel = false,
            bool hitboxOpen = false,
            bool inRecovery = false,
            bool catchUpPulse = false)
        {
            return new CombatActionFrame(
                normalizedTime,
                hitboxOpen,
                catchUpPulse,
                canChain,
                canCancel,
                false,
                false,
                inRecovery,
                false,
                false);
        }
    }
}
