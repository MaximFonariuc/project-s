using System;
using System.Collections.Generic;

namespace _App
{
    internal readonly struct PlayerCombatEntryRule
    {
        public PlayerCombatCommandId CommandId { get; }
        public PlayerCombatActionId TargetActionId { get; }

        public PlayerCombatEntryRule(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId targetActionId)
        {
            CommandId = commandId;
            TargetActionId = targetActionId;
        }
    }

    internal readonly struct PlayerCombatTransitionRule
    {
        public PlayerCombatActionId SourceActionId { get; }
        public PlayerCombatCommandId CommandId { get; }
        public PlayerCombatActionId TargetActionId { get; }
        public PlayerCombatTransitionWindow Window { get; }
        public PlayerCombatTransitionCondition Condition { get; }
        public NormalizedActionWindow AcceptanceWindow { get; }
        public NormalizedActionWindow InputWindow { get; }

        public PlayerCombatTransitionRule(
            PlayerCombatActionId sourceActionId,
            PlayerCombatCommandId commandId,
            PlayerCombatActionId targetActionId,
            PlayerCombatTransitionWindow window,
            PlayerCombatTransitionCondition condition = PlayerCombatTransitionCondition.Any,
            NormalizedActionWindow acceptanceWindow = default,
            NormalizedActionWindow inputWindow = default)
        {
            SourceActionId = sourceActionId;
            CommandId = commandId;
            TargetActionId = targetActionId;
            Window = window;
            Condition = condition;
            AcceptanceWindow = acceptanceWindow;
            InputWindow = inputWindow;
        }
    }

    internal sealed class PlayerCombatActionGraph
    {
        private readonly Dictionary<PlayerCombatActionId, PlayerCombatAction> _actions;
        private readonly Dictionary<PlayerCombatCommandId, List<PlayerCombatActionId>> _entries;
        private readonly Dictionary<TransitionKey, List<PlayerCombatTransitionRule>> _transitions;

        public PlayerCombatActionGraph(
            IEnumerable<PlayerCombatAction> actions,
            IEnumerable<PlayerCombatEntryRule> entries,
            IEnumerable<PlayerCombatTransitionRule> transitions)
        {
            _actions = BuildActions(actions);
            _entries = BuildEntries(entries, _actions);
            _transitions = BuildTransitions(transitions, _actions);
        }

        public bool TryResolveEntry(
            PlayerCombatCommandId commandId,
            PlayerCombatEnvironment environment,
            out PlayerCombatAction action)
        {
            if (_entries.TryGetValue(commandId, out List<PlayerCombatActionId> candidates))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (_actions.TryGetValue(candidates[i], out action) &&
                        action.Supports(environment))
                    {
                        return true;
                    }
                }
            }

            action = null;
            return false;
        }

        public bool TryResolveTransition(
            PlayerCombatAction source,
            in PlayerCombatCommandSample command,
            PlayerCombatEnvironment environment,
            in CombatActionFrame frame,
            CombatActionOutcome outcome,
            out PlayerCombatAction action)
        {
            if (source != null &&
                command.SourceActionId == source.Id &&
                _transitions.TryGetValue(
                    new TransitionKey(source.Id, command.CommandId),
                    out List<PlayerCombatTransitionRule> candidates))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    PlayerCombatTransitionRule transition = candidates[i];
                    if (IsWindowOpen(
                            in transition,
                            in frame,
                            command.SourceNormalizedTime) &&
                        IsConditionSatisfied(
                            transition.Condition,
                            source.Definition.HitboxWindow,
                            in frame,
                            outcome) &&
                        _actions.TryGetValue(transition.TargetActionId, out action) &&
                        action.Supports(environment))
                    {
                        return true;
                    }
                }
            }

            action = null;
            return false;
        }

        private static bool IsWindowOpen(
            in PlayerCombatTransitionRule transition,
            in CombatActionFrame frame,
            float inputNormalizedTime)
        {
            bool sourceWindowOpen = transition.Window switch
            {
                PlayerCombatTransitionWindow.Chain => frame.CanChain,
                PlayerCombatTransitionWindow.Cancel => frame.CanCancel,
                _ => false
            };

            return sourceWindowOpen &&
                   (!transition.AcceptanceWindow.IsEnabled ||
                    transition.AcceptanceWindow.Contains(frame.NormalizedTime)) &&
                   (!transition.InputWindow.IsEnabled ||
                    transition.InputWindow.Contains(inputNormalizedTime));
        }

        private static bool IsConditionSatisfied(
            PlayerCombatTransitionCondition condition,
            in NormalizedActionWindow hitboxWindow,
            in CombatActionFrame frame,
            CombatActionOutcome outcome)
        {
            return condition switch
            {
                PlayerCombatTransitionCondition.Any => true,
                PlayerCombatTransitionCondition.OnHit =>
                    outcome != null && outcome.IsTracking && outcome.HasConfirmedHit,
                PlayerCombatTransitionCondition.OnWhiff =>
                    outcome != null &&
                    outcome.IsTracking &&
                    !outcome.HasConfirmedHit &&
                    HasHitboxResolved(in hitboxWindow, in frame),
                _ => false
            };
        }

        private static bool HasHitboxResolved(
            in NormalizedActionWindow hitboxWindow,
            in CombatActionFrame frame)
        {
            return !hitboxWindow.IsEnabled || frame.NormalizedTime > hitboxWindow.End;
        }

        private static Dictionary<PlayerCombatActionId, PlayerCombatAction> BuildActions(
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
                    throw new ArgumentException("Combat graph contains a null action.", nameof(actions));
                }

                if (!lookup.TryAdd(action.Id, action))
                {
                    throw new ArgumentException($"Duplicate combat action '{action.Id}'.", nameof(actions));
                }
            }

            if (lookup.Count == 0)
            {
                throw new ArgumentException("Combat graph requires at least one action.", nameof(actions));
            }

            return lookup;
        }

        private static Dictionary<PlayerCombatCommandId, List<PlayerCombatActionId>> BuildEntries(
            IEnumerable<PlayerCombatEntryRule> entries,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var lookup = new Dictionary<PlayerCombatCommandId, List<PlayerCombatActionId>>();
            foreach (PlayerCombatEntryRule entry in entries)
            {
                if (!actions.ContainsKey(entry.TargetActionId))
                {
                    throw new ArgumentException(
                        $"Combat entry '{entry.CommandId}' targets missing action '{entry.TargetActionId}'.",
                        nameof(entries));
                }

                if (!lookup.TryGetValue(entry.CommandId, out List<PlayerCombatActionId> candidates))
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

        private static Dictionary<TransitionKey, List<PlayerCombatTransitionRule>> BuildTransitions(
            IEnumerable<PlayerCombatTransitionRule> transitions,
            IReadOnlyDictionary<PlayerCombatActionId, PlayerCombatAction> actions)
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            var lookup = new Dictionary<TransitionKey, List<PlayerCombatTransitionRule>>();
            foreach (PlayerCombatTransitionRule transition in transitions)
            {
                if (!actions.ContainsKey(transition.SourceActionId) ||
                    !actions.ContainsKey(transition.TargetActionId))
                {
                    throw new ArgumentException(
                        $"Combat transition '{transition.SourceActionId}' -> '{transition.TargetActionId}' references a missing action.",
                        nameof(transitions));
                }

                ValidateTransitionCondition(transition, actions);
                ValidateTransitionWindow(transition, actions);

                var key = new TransitionKey(transition.SourceActionId, transition.CommandId);
                if (!lookup.TryGetValue(key, out List<PlayerCombatTransitionRule> candidates))
                {
                    candidates = new List<PlayerCombatTransitionRule>();
                    lookup.Add(key, candidates);
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    PlayerCombatTransitionRule candidate = candidates[i];
                    if (ConditionsCanOverlap(candidate.Condition, transition.Condition) &&
                        TransitionWindowsOverlap(candidate, transition, actions) &&
                        (actions[candidate.TargetActionId].Environment &
                         actions[transition.TargetActionId].Environment) != 0)
                    {
                        throw new ArgumentException(
                            $"Combat transition from '{transition.SourceActionId}' for command " +
                            $"'{transition.CommandId}' is ambiguous in its environment and timing window.",
                            nameof(transitions));
                    }
                }
                candidates.Add(transition);
            }

            return lookup;
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

            NormalizedActionWindow transitionWindow = transition.Window switch
            {
                PlayerCombatTransitionWindow.Chain => source.Definition.ChainWindow,
                PlayerCombatTransitionWindow.Cancel => source.Definition.CancelWindow,
                _ => NormalizedActionWindow.Disabled
            };
            if (transitionWindow.IsEnabled && transitionWindow.End <= hitboxWindow.End)
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
                inputWindow.End > actions[transition.SourceActionId].Definition.CompletionTime)
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

            NormalizedActionWindow leftInputWindow =
                GetInputWindow(left, definition);
            NormalizedActionWindow rightInputWindow =
                GetInputWindow(right, definition);
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
            PlayerCombatEnvironment targetEnvironment = actions[targetActionId].Environment;
            for (int i = 0; i < candidates.Count; i++)
            {
                PlayerCombatAction candidate = actions[candidates[i]];
                if ((candidate.Environment & targetEnvironment) != 0)
                {
                    throw new ArgumentException(
                        $"Combat {ruleName} is ambiguous for environment '{targetEnvironment}'.",
                        parameterName);
                }
            }
        }

        private readonly struct TransitionKey : IEquatable<TransitionKey>
        {
            private readonly PlayerCombatActionId _sourceActionId;
            private readonly PlayerCombatCommandId _commandId;

            public TransitionKey(
                PlayerCombatActionId sourceActionId,
                PlayerCombatCommandId commandId)
            {
                _sourceActionId = sourceActionId;
                _commandId = commandId;
            }

            public bool Equals(TransitionKey other)
            {
                return _sourceActionId == other._sourceActionId && _commandId == other._commandId;
            }

            public override bool Equals(object obj)
            {
                return obj is TransitionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_sourceActionId.GetHashCode() * 397) ^ _commandId.GetHashCode();
                }
            }
        }
    }
}
