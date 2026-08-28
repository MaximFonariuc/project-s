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
        private readonly Dictionary<PlayerCombatTransitionKey, List<PlayerCombatTransitionRule>>
            _transitions;

        public PlayerCombatActionGraph(
            IEnumerable<PlayerCombatAction> actions,
            IEnumerable<PlayerCombatEntryRule> entries,
            IEnumerable<PlayerCombatTransitionRule> transitions)
        {
            _actions = PlayerCombatActionGraphCompiler.BuildActions(actions);
            _entries = PlayerCombatActionGraphCompiler.BuildEntries(entries, _actions);
            _transitions = PlayerCombatActionGraphCompiler.BuildTransitions(
                transitions,
                _actions);
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
                    new PlayerCombatTransitionKey(source.Id, command.CommandId),
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
                    !frame.IsHitboxCatchUpPulse &&
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
    }
}
