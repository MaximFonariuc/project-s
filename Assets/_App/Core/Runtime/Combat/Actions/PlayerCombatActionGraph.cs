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

        public PlayerCombatTransitionRule(
            PlayerCombatActionId sourceActionId,
            PlayerCombatCommandId commandId,
            PlayerCombatActionId targetActionId,
            PlayerCombatTransitionWindow window)
        {
            SourceActionId = sourceActionId;
            CommandId = commandId;
            TargetActionId = targetActionId;
            Window = window;
        }
    }

    internal sealed class PlayerCombatActionGraph
    {
        public const int PrototypeComboCapacity = 16;

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
            PlayerCombatCommandId commandId,
            PlayerCombatEnvironment environment,
            in CombatActionFrame frame,
            out PlayerCombatAction action)
        {
            if (source != null &&
                _transitions.TryGetValue(
                    new TransitionKey(source.Id, commandId),
                    out List<PlayerCombatTransitionRule> candidates))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    PlayerCombatTransitionRule transition = candidates[i];
                    if (IsWindowOpen(transition.Window, in frame) &&
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

        public static PlayerCombatActionGraph CreatePrototype(int comboCapacity)
        {
            if (comboCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(comboCapacity));
            }

            var actions = new List<PlayerCombatAction>();
            var entries = new List<PlayerCombatEntryRule>();
            var transitions = new List<PlayerCombatTransitionRule>();
            var comboActions = new List<PlayerCombatAction>(comboCapacity);
            CombatActionDefinition comboDefinition =
                CombatActionAuthoring.CreatePrototypeCombo().Build();

            for (int i = 0; i < comboCapacity; i++)
            {
                var action = new PlayerCombatAction(
                    new PlayerCombatActionId($"combo.{i}"),
                    PlayerCombatEnvironment.Grounded,
                    PlayerCombatPresentation.Combo(i),
                    comboDefinition);
                actions.Add(action);
                comboActions.Add(action);
            }

            if (comboActions.Count > 0)
            {
                entries.Add(new PlayerCombatEntryRule(
                    PlayerCombatCommandIds.LightAttack,
                    comboActions[0].Id));
            }

            for (int i = 0; i < comboActions.Count - 1; i++)
            {
                transitions.Add(new PlayerCombatTransitionRule(
                    comboActions[i].Id,
                    PlayerCombatCommandIds.LightAttack,
                    comboActions[i + 1].Id,
                    PlayerCombatTransitionWindow.Chain));
            }

            var specialAction = new PlayerCombatAction(
                new PlayerCombatActionId("skill.special"),
                PlayerCombatEnvironment.Grounded,
                PlayerCombatPresentation.Named(PlayerCombatAnimationIds.Special),
                CombatActionAuthoring.CreatePrototypeSkill().Build());
            actions.Add(specialAction);
            var namedActions = new List<KeyValuePair<PlayerCombatCommandId, PlayerCombatAction>>
            {
                new KeyValuePair<PlayerCombatCommandId, PlayerCombatAction>(
                    PlayerCombatCommandIds.Special,
                    specialAction)
            };
            entries.Add(new PlayerCombatEntryRule(
                PlayerCombatCommandIds.Special,
                specialAction.Id));

            for (int i = 0; i < comboActions.Count; i++)
            {
                for (int actionIndex = 0; actionIndex < namedActions.Count; actionIndex++)
                {
                    KeyValuePair<PlayerCombatCommandId, PlayerCombatAction> namedAction =
                        namedActions[actionIndex];
                    transitions.Add(new PlayerCombatTransitionRule(
                        comboActions[i].Id,
                        namedAction.Key,
                        namedAction.Value.Id,
                        PlayerCombatTransitionWindow.Cancel));
                }
            }

            for (int sourceIndex = 0; sourceIndex < namedActions.Count; sourceIndex++)
            {
                PlayerCombatAction source = namedActions[sourceIndex].Value;
                if (comboActions.Count > 0)
                {
                    transitions.Add(new PlayerCombatTransitionRule(
                        source.Id,
                        PlayerCombatCommandIds.LightAttack,
                        comboActions[0].Id,
                        PlayerCombatTransitionWindow.Cancel));
                }

                for (int targetIndex = 0; targetIndex < namedActions.Count; targetIndex++)
                {
                    KeyValuePair<PlayerCombatCommandId, PlayerCombatAction> target = namedActions[targetIndex];
                    transitions.Add(new PlayerCombatTransitionRule(
                        source.Id,
                        target.Key,
                        target.Value.Id,
                        PlayerCombatTransitionWindow.Cancel));
                }
            }

            return new PlayerCombatActionGraph(actions, entries, transitions);
        }

        private static bool IsWindowOpen(
            PlayerCombatTransitionWindow window,
            in CombatActionFrame frame)
        {
            return window switch
            {
                PlayerCombatTransitionWindow.Chain => frame.CanChain,
                PlayerCombatTransitionWindow.Cancel => frame.CanCancel,
                _ => false
            };
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

                var key = new TransitionKey(transition.SourceActionId, transition.CommandId);
                if (!lookup.TryGetValue(key, out List<PlayerCombatTransitionRule> candidates))
                {
                    candidates = new List<PlayerCombatTransitionRule>();
                    lookup.Add(key, candidates);
                }

                var targetIds = new List<PlayerCombatActionId>(candidates.Count);
                for (int i = 0; i < candidates.Count; i++)
                {
                    targetIds.Add(candidates[i].TargetActionId);
                }

                EnsureEnvironmentDoesNotOverlap(
                    targetIds,
                    transition.TargetActionId,
                    actions,
                    $"transition from '{transition.SourceActionId}' for command '{transition.CommandId}'",
                    nameof(transitions));
                candidates.Add(transition);
            }

            return lookup;
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
