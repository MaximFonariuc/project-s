using System;
using System.Collections.Generic;

namespace _App
{
    internal readonly struct PlayerCombatCommandRequest
    {
        public PlayerCombatCommandId CommandId { get; }
        public PlayerCombatActionId SourceActionId { get; }
        public float SourceNormalizedTime { get; }
        public bool HasSourceAction { get; }

        private PlayerCombatCommandRequest(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId sourceActionId,
            float sourceNormalizedTime,
            bool hasSourceAction)
        {
            if (string.IsNullOrEmpty(commandId.Value))
            {
                throw new ArgumentException(
                    "Combat command request requires a command id.",
                    nameof(commandId));
            }

            if (hasSourceAction && string.IsNullOrEmpty(sourceActionId.Value))
            {
                throw new ArgumentException(
                    "A bound combat command request requires a source action id.",
                    nameof(sourceActionId));
            }

            if (sourceNormalizedTime < 0.0f || sourceNormalizedTime > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceNormalizedTime));
            }

            CommandId = commandId;
            SourceActionId = sourceActionId;
            SourceNormalizedTime = sourceNormalizedTime;
            HasSourceAction = hasSourceAction;
        }

        public static PlayerCombatCommandRequest Unbound(
            PlayerCombatCommandId commandId)
        {
            return new PlayerCombatCommandRequest(
                commandId,
                default,
                0.0f,
                false);
        }

        public static PlayerCombatCommandRequest Bound(
            PlayerCombatCommandId commandId,
            PlayerCombatActionId sourceActionId,
            float sourceNormalizedTime)
        {
            return new PlayerCombatCommandRequest(
                commandId,
                sourceActionId,
                sourceNormalizedTime,
                true);
        }
    }

    internal sealed class PlayerCombatCommandInbox
    {
        private readonly Queue<PlayerCombatCommandRequest> _commands;
        private readonly int _capacity;
        private bool _weaponSwitchRequested;

        public PlayerCombatCommandInbox(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _commands = new Queue<PlayerCombatCommandRequest>(capacity);
        }

        public bool TryEnqueue(in PlayerCombatCommandRequest request)
        {
            if (string.IsNullOrEmpty(request.CommandId.Value) ||
                _commands.Count >= _capacity)
            {
                return false;
            }

            _commands.Enqueue(request);
            return true;
        }

        public bool TryDequeue(out PlayerCombatCommandRequest request)
        {
            if (_commands.Count == 0)
            {
                request = default;
                return false;
            }

            request = _commands.Dequeue();
            return true;
        }

        public void RequestWeaponSwitch()
        {
            _weaponSwitchRequested = true;
        }

        public bool ConsumeWeaponSwitchRequest()
        {
            bool requested = _weaponSwitchRequested;
            _weaponSwitchRequested = false;
            return requested;
        }

        public void Clear()
        {
            _commands.Clear();
            _weaponSwitchRequested = false;
        }
    }
}
