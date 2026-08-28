using System;
using System.Collections.Generic;

namespace _App
{
    internal sealed class PlayerCombatAvailability
    {
        private readonly IPlayerCombatActor _actor;
        private readonly ICharacterReactionState _reactionState;
        private readonly IPlayerCombatAnimation _animation;
        private readonly ICombatClock _clock;
        private readonly ECharacterType _characterType;
        private readonly Dictionary<PlayerCombatActionId, float> _lastUseTime =
            new Dictionary<PlayerCombatActionId, float>();

        public bool CanEnterAction =>
            !_reactionState.BlocksActions && _actor.CanAttack;

        public bool CanSwitchWeapon =>
            !_reactionState.BlocksActions && !_actor.IsFalling;

        public PlayerCombatAvailability(
            IPlayerCombatActor actor,
            ICharacterReactionState reactionState,
            IPlayerCombatAnimation animation,
            ICombatClock clock,
            ECharacterType characterType)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _reactionState = reactionState ??
                throw new ArgumentNullException(nameof(reactionState));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _characterType = characterType;
        }

        public bool CanUseAction(
            PlayerCombatAction action,
            EWeaponType weaponType)
        {
            if (action == null ||
                (_lastUseTime.TryGetValue(action.Id, out float lastUseTime) &&
                 _clock.ScaledTime - lastUseTime < action.Definition.Cooldown))
            {
                return false;
            }

            return _animation.CanPlayAction(
                _characterType,
                weaponType,
                action);
        }

        public void RecordUse(PlayerCombatAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Definition.Cooldown > 0.0f)
            {
                _lastUseTime[action.Id] = _clock.ScaledTime;
            }
        }
    }
}
