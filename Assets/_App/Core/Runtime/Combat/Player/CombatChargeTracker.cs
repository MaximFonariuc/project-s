using System;

namespace _App
{
    internal sealed class CombatChargeTracker
    {
        private readonly ICombatClock _clock;
        private readonly float _holdSeconds;
        private PlayerCombatCommandId _pendingCommand;
        private float _pressedAt;
        private bool _isPressed;

        public CombatChargeTracker(ICombatClock clock, float holdSeconds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (holdSeconds <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(holdSeconds),
                    "Charge hold threshold must be positive.");
            }

            _holdSeconds = holdSeconds;
        }

        public void NotePressed(PlayerCombatCommandId commandId)
        {
            _pendingCommand = commandId;
            _pressedAt = _clock.UnscaledTime;
            _isPressed = true;
        }

        public bool TryResolveRelease(
            PlayerCombatCommandId commandId,
            out PlayerCombatCommandId chargedCommandId)
        {
            chargedCommandId = default;
            if (!_isPressed || _pendingCommand != commandId)
            {
                return false;
            }

            _isPressed = false;
            if (_clock.UnscaledTime - _pressedAt < _holdSeconds)
            {
                return false;
            }

            return TryGetChargedCommand(commandId, out chargedCommandId);
        }

        public void Clear()
        {
            _isPressed = false;
            _pendingCommand = default;
            _pressedAt = 0.0f;
        }

        private static bool TryGetChargedCommand(
            PlayerCombatCommandId commandId,
            out PlayerCombatCommandId chargedCommandId)
        {
            if (commandId == PlayerCombatCommandIds.HeavyAttack)
            {
                chargedCommandId = PlayerCombatCommandIds.ChargedHeavyAttack;
                return true;
            }

            chargedCommandId = default;
            return false;
        }
    }
}