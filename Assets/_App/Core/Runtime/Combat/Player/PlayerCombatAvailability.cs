using System;
using System.Collections.Generic;

namespace _App
{
    internal sealed class PlayerCombatAvailability
    {
        private readonly IPlayerCombatActor _actor;
        private readonly ICharacterReactionState _reactionState;
        private readonly IPlayerDefenseState _defenseState;
        private readonly IPlayerCombatAnimation _animation;
        private readonly ICombatClock _clock;
        private readonly ECharacterType _characterType;
        private readonly Dictionary<PlayerCombatCooldownKey, float> _lastUseTime = new();

        public bool CanEnterAction =>
            !_reactionState.BlocksActions && !_defenseState.IsGuarding &&
            _actor.CanAttack;

        public bool CanBufferEntry =>
            _reactionState.CurrentState != CharacterReactionStateId.Dead &&
            !_defenseState.IsGuarding;

        public bool CanSwitchWeapon =>
            !_reactionState.BlocksActions && !_defenseState.IsGuarding &&
            !_actor.IsFalling;

        public PlayerCombatAvailability(
            IPlayerCombatActor actor,
            ICharacterReactionState reactionState,
            IPlayerDefenseState defenseState,
            IPlayerCombatAnimation animation,
            ICombatClock clock,
            ECharacterType characterType)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _reactionState = reactionState ??
                throw new ArgumentNullException(nameof(reactionState));
            _defenseState = defenseState ??
                throw new ArgumentNullException(nameof(defenseState));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _characterType = characterType;
        }

        public bool CanUseAction(
            PlayerCombatAction action,
            EWeaponType weaponType)
        {
            var cooldownKey = new PlayerCombatCooldownKey(weaponType, action?.Id ?? default);
            if (action == null ||
                (_lastUseTime.TryGetValue(cooldownKey, out float lastUseTime) &&
                 _clock.ScaledTime - lastUseTime < action.Definition.Cooldown))
            {
                return false;
            }

            return _animation.CanPlayAction(
                _characterType,
                weaponType,
                action);
        }

        public void RecordUse(PlayerCombatAction action, EWeaponType weaponType)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Definition.Cooldown > 0.0f)
            {
                _lastUseTime[new PlayerCombatCooldownKey(weaponType, action.Id)] =
                    _clock.ScaledTime;
            }
        }

        private readonly struct PlayerCombatCooldownKey : IEquatable<PlayerCombatCooldownKey>
        {
            private readonly EWeaponType _weaponType;
            private readonly PlayerCombatActionId _actionId;

            public PlayerCombatCooldownKey(
                EWeaponType weaponType,
                PlayerCombatActionId actionId)
            {
                _weaponType = weaponType;
                _actionId = actionId;
            }

            public bool Equals(PlayerCombatCooldownKey other)
            {
                return _weaponType == other._weaponType && _actionId == other._actionId;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerCombatCooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine((int)_weaponType, _actionId);
            }
        }
    }
}
