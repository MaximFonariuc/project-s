using System;
using System.Collections.Generic;

namespace _App
{
    internal sealed class PlayerCombatCommandInbox
    {
        private readonly Queue<PlayerCombatCommandId> _commands;
        private readonly int _capacity;
        private bool _weaponSwitchRequested;

        public PlayerCombatCommandInbox(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _commands = new Queue<PlayerCombatCommandId>(capacity);
        }

        public bool TryEnqueue(PlayerCombatCommandId commandId)
        {
            if (string.IsNullOrEmpty(commandId.Value) || _commands.Count >= _capacity)
            {
                return false;
            }

            _commands.Enqueue(commandId);
            return true;
        }

        public bool TryDequeue(out PlayerCombatCommandId commandId)
        {
            if (_commands.Count == 0)
            {
                commandId = default;
                return false;
            }

            commandId = _commands.Dequeue();
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
