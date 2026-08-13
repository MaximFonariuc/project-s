using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal interface IPlayerTraversalActions
    {
        void Enable();
        void Disable();
        bool IsClimbAvailable { get; }
        void ApplyClimbInput(Vector2 input);
        bool TryBeginClimb();
    }

    internal sealed class PlayerTraversalActions : IPlayerTraversalActions
    {
        private readonly LadderTraversalAction _ladder;
        private bool _enabled;

        public bool IsClimbAvailable => _ladder != null;

        public PlayerTraversalActions(
            Character character,
            ICharacterAnimationDriver animationDriver,
            IPlayerLocomotionState locomotionState,
            CharacterFeatureSet features)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (animationDriver == null)
            {
                throw new ArgumentNullException(nameof(animationDriver));
            }

            if (locomotionState == null)
            {
                throw new ArgumentNullException(nameof(locomotionState));
            }

            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            LadderTraversalSettings settings = features.GetEnabled<LadderTraversalSettings>();
            if (settings != null)
            {
                _ladder = new LadderTraversalAction(
                    character,
                    animationDriver,
                    locomotionState,
                    settings);
            }
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _ladder?.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _ladder?.Disable();
            _enabled = false;
        }

        public void ApplyClimbInput(Vector2 input)
        {
            _ladder?.OnClimbInput(input);
        }

        public bool TryBeginClimb()
        {
            if (!_enabled || _ladder == null)
            {
                return false;
            }

            _ladder.BeginClimb();
            return true;
        }
    }
}
