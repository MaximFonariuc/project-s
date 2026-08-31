using System;
using System.Collections.Generic;

namespace _App
{
    internal readonly struct PlayerCombatTransitionKey :
        IEquatable<PlayerCombatTransitionKey>
    {
        private readonly PlayerCombatActionId _sourceActionId;
        private readonly PlayerCombatCommandId _commandId;

        public PlayerCombatTransitionKey(
            PlayerCombatActionId sourceActionId,
            PlayerCombatCommandId commandId)
        {
            _sourceActionId = sourceActionId;
            _commandId = commandId;
        }

        public bool Equals(PlayerCombatTransitionKey other)
        {
            return _sourceActionId == other._sourceActionId &&
                   _commandId == other._commandId;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerCombatTransitionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_sourceActionId.GetHashCode() * 397) ^
                       _commandId.GetHashCode();
            }
        }
    }

    internal static class PlayerCombatActionGraphCompiler
    {
        public static Dictionary<PlayerCombatActionId, PlayerCombatAction> BuildActions(
            IEnumerable<PlayerCombatAction> actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var lookup = new Dictionary<PlayerCombatActionId, PlayerCombatAction>();
            foreach (PlayerCombatAction action in actions)
            {
                if (action == null)
                {
                    throw new ArgumentException(
                        "Combat graph contains a null action.",
                        nameof(actions));
                }

                if (!lookup.TryAdd(action.Id, action))
                {
                    throw new ArgumentException(
                        $"Duplicate combat action '{action.Id}'.",
                        nameof(actions));
                }
            }

            if (lookup.Count == 0)
            {
                throw new ArgumentException(
                    "Combat graph requires at least one action.",
                    nameof(actions));
            }

            return lookup;
        }

        public static Dictionary<PlayerCombatCommandId, List<PlayerCombatActionId>>
            BuildEntries(
                IEnumerable<PlayerCombatEntryRule> entries,
                IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var lookup =
                new Dictionary<PlayerCombatCommandId, List<PlayerCombatActionId>>();
            foreach (PlayerCombatEntryRule entry in entries)
            {
                if (!actions.ContainsKey(entry.TargetActionId))
                {
                    throw new ArgumentException(
                        $"Combat entry '{entry.CommandId}' targets missing action " +
                        $"'{entry.TargetActionId}'.",
                        nameof(entries));
                }

                if (!lookup.TryGetValue(
                        entry.CommandId,
                        out List<PlayerCombatActionId> candidates))
                {
                    candidates = new List<PlayerCombatActionId>();
                    lookup.Add(entry.CommandId, candidates);
                }

                EnsureEnvironmentDoesNotOverlap(
                    candidates,
                    entry.TargetActionId,
                    actions,
                    $"entry command '{entry.CommandId}'",
                    nameof(entries));
                candidates.Add(entry.TargetActionId);
            }

            return lookup;
        }

        public static Dictionary<PlayerCombatTransitionKey, List<PlayerCombatTransitionRule>>
            BuildTransitions(
                IEnumerable<PlayerCombatTransitionRule> transitions,
                IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            var lookup =
                new Dictionary<PlayerCombatTransitionKey, List<PlayerCombatTransitionRule>>();
            foreach (PlayerCombatTransitionRule transition in transitions)
            {
                if (!actions.ContainsKey(transition.SourceActionId) ||
                    !actions.ContainsKey(transition.TargetActionId))
                {
                    throw new ArgumentException(
                        $"Combat transition '{transition.SourceActionId}' -> " +
                        $"'{transition.TargetActionId}' references a missing action.",
                        nameof(transitions));
                }

                ValidateTransitionCondition(transition, actions);
                ValidateTransitionWindow(transition, actions);

                var key = new PlayerCombatTransitionKey(
                    transition.SourceActionId,
                    transition.CommandId);
                if (!lookup.TryGetValue(
                        key,
                        out List<PlayerCombatTransitionRule> candidates))
                {
                    candidates = new List<PlayerCombatTransitionRule>();
                    lookup.Add(key, candidates);
                }

                EnsureTransitionDoesNotOverlap(
                    transition,
                    candidates,
                    actions,
                    nameof(transitions));
                candidates.Add(transition);
            }

            return lookup;
        }

        public static void ValidateReachability(
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions,
            IReadOnlyDictionary<PlayerCombatCommandId, List<PlayerCombatActionId>> entries,
            IReadOnlyDictionary<
                PlayerCombatTransitionKey,
                List<PlayerCombatTransitionRule>> transitions)
        {
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

            var reachable = new HashSet<PlayerCombatActionId>();
            var pending = new Queue<PlayerCombatActionId>();
            foreach (List<PlayerCombatActionId> entryActions in entries.Values)
            {
                for (int i = 0; i < entryActions.Count; i++)
                {
                    if (reachable.Add(entryActions[i]))
                    {
                        pending.Enqueue(entryActions[i]);
                    }
                }
            }

            var outgoing = new Dictionary<
                PlayerCombatActionId,
                List<PlayerCombatActionId>>();
            foreach (List<PlayerCombatTransitionRule> rules in transitions.Values)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    PlayerCombatTransitionRule rule = rules[i];
                    if (!outgoing.TryGetValue(
                            rule.SourceActionId,
                            out List<PlayerCombatActionId> targets))
                    {
                        targets = new List<PlayerCombatActionId>();
                        outgoing.Add(rule.SourceActionId, targets);
                    }

                    targets.Add(rule.TargetActionId);
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

            if (reachable.Count == actions.Count)
            {
                return;
            }

            var unreachable = new List<string>();
            foreach (PlayerCombatActionId actionId in actions.Keys)
            {
                if (!reachable.Contains(actionId))
                {
                    unreachable.Add(actionId.ToString());
                }
            }

            unreachable.Sort(StringComparer.Ordinal);
            throw new ArgumentException(
                "Combat graph contains actions that cannot be reached from any entry: " +
                string.Join(", ", unreachable) + ".",
                nameof(actions));
        }

        private static void EnsureTransitionDoesNotOverlap(
            in PlayerCombatTransitionRule transition,
            IReadOnlyList<PlayerCombatTransitionRule> candidates,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions,
            string parameterName)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                PlayerCombatTransitionRule candidate = candidates[i];
                if (!ConditionsCanOverlap(candidate.Condition, transition.Condition) ||
                    !TransitionWindowsOverlap(candidate, transition, actions) ||
                    (actions[candidate.TargetActionId].Environment &
                     actions[transition.TargetActionId].Environment) == 0)
                {
                    continue;
                }

                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' for command " +
                    $"'{transition.CommandId}' is ambiguous in its environment and " +
                    "timing window.",
                    parameterName);
            }
        }

        private static void ValidateTransitionCondition(
            in PlayerCombatTransitionRule transition,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            PlayerCombatAction source = actions[transition.SourceActionId];
            NormalizedActionWindow hitboxWindow = source.Definition.HitboxWindow;
            if (transition.Condition == PlayerCombatTransitionCondition.OnHit &&
                !hitboxWindow.IsEnabled)
            {
                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' requires a hit, " +
                    "but its source action has no hitbox window.",
                    "transitions");
            }

            if (transition.Condition != PlayerCombatTransitionCondition.OnWhiff ||
                !hitboxWindow.IsEnabled)
            {
                return;
            }

            NormalizedActionWindow transitionWindow = GetSourceWindow(
                transition.Window,
                source.Definition);
            if (transitionWindow.IsEnabled &&
                transitionWindow.End <= hitboxWindow.End)
            {
                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' requires a whiff, " +
                    $"but its {transition.Window} window closes before the hitbox is resolved.",
                    "transitions");
            }
        }

        private static void ValidateTransitionWindow(
            in PlayerCombatTransitionRule transition,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            NormalizedActionWindow sourceWindow = GetSourceWindow(
                transition.Window,
                actions[transition.SourceActionId].Definition);
            if (!sourceWindow.IsEnabled)
            {
                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' uses a disabled " +
                    $"{transition.Window} window.",
                    "transitions");
            }

            NormalizedActionWindow acceptanceWindow = transition.AcceptanceWindow;
            if (acceptanceWindow.IsEnabled &&
                (acceptanceWindow.Start < sourceWindow.Start ||
                 acceptanceWindow.End > sourceWindow.End))
            {
                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' has an acceptance " +
                    $"window outside its {transition.Window} window.",
                    "transitions");
            }

            NormalizedActionWindow inputWindow = transition.InputWindow;
            if (inputWindow.IsEnabled &&
                inputWindow.End >
                actions[transition.SourceActionId].Definition.CompletionTime)
            {
                throw new ArgumentException(
                    $"Combat transition from '{transition.SourceActionId}' has an input " +
                    "window after its source action completes.",
                    "transitions");
            }
        }

        private static bool TransitionWindowsOverlap(
            in PlayerCombatTransitionRule left,
            in PlayerCombatTransitionRule right,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            CombatActionDefinition definition = actions[left.SourceActionId].Definition;
            NormalizedActionWindow leftExecutionWindow =
                GetExecutionWindow(left, definition);
            NormalizedActionWindow rightExecutionWindow =
                GetExecutionWindow(right, definition);
            if (!WindowsOverlap(in leftExecutionWindow, in rightExecutionWindow))
            {
                return false;
            }

            NormalizedActionWindow leftInputWindow = GetInputWindow(left, definition);
            NormalizedActionWindow rightInputWindow = GetInputWindow(right, definition);
            return WindowsOverlap(in leftInputWindow, in rightInputWindow);
        }

        private static NormalizedActionWindow GetExecutionWindow(
            in PlayerCombatTransitionRule transition,
            in CombatActionDefinition definition)
        {
            return transition.AcceptanceWindow.IsEnabled
                ? transition.AcceptanceWindow
                : GetSourceWindow(transition.Window, definition);
        }

        private static NormalizedActionWindow GetInputWindow(
            in PlayerCombatTransitionRule transition,
            in CombatActionDefinition definition)
        {
            return transition.InputWindow.IsEnabled
                ? transition.InputWindow
                : new NormalizedActionWindow(0.0f, definition.CompletionTime);
        }

        private static bool WindowsOverlap(
            in NormalizedActionWindow left,
            in NormalizedActionWindow right)
        {
            return left.Start <= right.End && right.Start <= left.End;
        }

        private static NormalizedActionWindow GetSourceWindow(
            PlayerCombatTransitionWindow window,
            in CombatActionDefinition definition)
        {
            return window switch
            {
                PlayerCombatTransitionWindow.Chain => definition.ChainWindow,
                PlayerCombatTransitionWindow.Cancel => definition.CancelWindow,
                _ => NormalizedActionWindow.Disabled
            };
        }

        private static bool ConditionsCanOverlap(
            PlayerCombatTransitionCondition left,
            PlayerCombatTransitionCondition right)
        {
            return left == PlayerCombatTransitionCondition.Any ||
                   right == PlayerCombatTransitionCondition.Any ||
                   left == right;
        }

        private static void EnsureEnvironmentDoesNotOverlap(
            IReadOnlyList<PlayerCombatActionId> candidates,
            PlayerCombatActionId targetActionId,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions,
            string ruleName,
            string parameterName)
        {
            PlayerCombatEnvironment targetEnvironment =
                actions[targetActionId].Environment;
            for (int i = 0; i < candidates.Count; i++)
            {
                PlayerCombatAction candidate = actions[candidates[i]];
                if ((candidate.Environment & targetEnvironment) == 0)
                {
                    continue;
                }

                throw new ArgumentException(
                    $"Combat {ruleName} is ambiguous for environment " +
                    $"'{targetEnvironment}'.",
                    parameterName);
            }
        }
    }
}
