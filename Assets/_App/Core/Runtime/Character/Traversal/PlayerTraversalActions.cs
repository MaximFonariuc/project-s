using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    internal interface IPlayerTraversalActions
    {
        bool IsClimbAvailable { get; }
        void ApplyClimbInput(Vector2 input);
        bool TryBeginClimb();
    }

    internal sealed class PlayerTraversalActionAdapter : IPlayerTraversalActions
    {
        private readonly LadderClimbAbility _ladder;

        public bool IsClimbAvailable => _ladder != null;

        public PlayerTraversalActionAdapter(CharacterAbilityRuntime abilities)
        {
            if (abilities == null)
            {
                throw new ArgumentNullException(nameof(abilities));
            }

            _ladder = abilities.Get<LadderClimbAbility>();
        }

        public void ApplyClimbInput(Vector2 input)
        {
            _ladder?.OnClimbInput(input);
        }

        public bool TryBeginClimb()
        {
            if (_ladder == null)
            {
                return false;
            }

            _ladder.Climb().Forget();
            return true;
        }
    }
}
