using System;
using UnityEngine;

namespace _App
{
    internal interface IPlayerTraversalActions
    {
        void Enable();
        void Disable();
        void Interrupt();
        bool IsClimbAvailable { get; }
        void ApplyClimbInput(Vector2 input);
        bool TryBeginClimb();
    }

    internal sealed class PlayerTraversalActions : IPlayerTraversalActions, IPlayerLadderGrip
    {
        private readonly LadderTraversalAction _ladder;
        private bool _enabled;

        public bool IsClimbAvailable => _ladder != null;
        public bool IsGripActive => _ladder?.IsGripActive == true;
        public Ladder ActiveLadder => _ladder?.ActiveLadder;

        public PlayerTraversalActions(
            Character character,
            PlayerAnimationCoordinator animation,
            IPlayerLocomotionState locomotionState,
            CharacterFeatureSet features)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (animation == null)
            {
                throw new ArgumentNullException(nameof(animation));
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
            if (settings != null && animation.Supports(PlayerAnimationSlots.LadderIdle))
            {
                _ladder = new LadderTraversalAction(
                    character,
                    animation,
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

        public void Interrupt()
        {
            _ladder?.Interrupt();
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
