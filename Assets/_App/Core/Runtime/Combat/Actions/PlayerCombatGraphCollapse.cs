using System;
using System.Collections.Generic;

namespace _App
{
    internal static class PlayerCombatGraphCollapse
    {
        public static void Apply(
            IReadOnlyCollection<PlayerCombatActionId> unavailableActionIds,
            List<PlayerCombatAction> actions,
            List<PlayerCombatEntryRule> entries,
            List<PlayerCombatTransitionRule> transitions,
            string profileName)
        {
            if (unavailableActionIds == null)
            {
                throw new ArgumentNullException(nameof(unavailableActionIds));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            var unavailable = new HashSet<PlayerCombatActionId>(unavailableActionIds);
            actions.RemoveAll(action => unavailable.Contains(action.Id));
            entries.RemoveAll(entry => unavailable.Contains(entry.TargetActionId));
            transitions.RemoveAll(transition =>
                unavailable.Contains(transition.SourceActionId) ||
                unavailable.Contains(transition.TargetActionId));

            PruneUnreachable(actions, entries, transitions);

            if (actions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Combat action profile '{profileName}' has no reachable action " +
                    "left after removing the ones this weapon does not provide.");
            }
        }

        private static void PruneUnreachable(
            List<PlayerCombatAction> actions,
            List<PlayerCombatEntryRule> entries,
            List<PlayerCombatTransitionRule> transitions)
        {
            var outgoing = new Dictionary<
                PlayerCombatActionId,
                List<PlayerCombatActionId>>();
            for (int i = 0; i < transitions.Count; i++)
            {
                PlayerCombatTransitionRule transition = transitions[i];
                if (!outgoing.TryGetValue(
                        transition.SourceActionId,
                        out List<PlayerCombatActionId> targets))
                {
                    targets = new List<PlayerCombatActionId>();
                    outgoing.Add(transition.SourceActionId, targets);
                }

                targets.Add(transition.TargetActionId);
            }

            var reachable = new HashSet<PlayerCombatActionId>();
            var pending = new Queue<PlayerCombatActionId>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (reachable.Add(entries[i].TargetActionId))
                {
                    pending.Enqueue(entries[i].TargetActionId);
                }
            }

            while (pending.Count > 0)
            {
                PlayerCombatActionId source = pending.Dequeue();
                if (!outgoing.TryGetValue(
                        source,
                        out List<PlayerCombatActionId> targets))
                {
                    continue;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    if (reachable.Add(targets[i]))
                    {
                        pending.Enqueue(targets[i]);
                    }
                }
            }

            actions.RemoveAll(action => !reachable.Contains(action.Id));
            transitions.RemoveAll(transition =>
                !reachable.Contains(transition.SourceActionId) ||
                !reachable.Contains(transition.TargetActionId));
        }
    }
}