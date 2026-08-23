using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal enum PlayerAnimationSource
    {
        Locomotion,
        Combat,
        Traversal,
        Reaction,
        Death
    }

    internal static class PlayerAnimatorActions
    {
        public static readonly AnimationActionId Dodge = new AnimationActionId(1);
        public static readonly AnimationActionId LightAttack = new AnimationActionId(2);
        public static readonly AnimationActionId WeaponTransition = new AnimationActionId(3);
        public static readonly AnimationActionId Reaction = new AnimationActionId(4);

        public const float DodgeDuration = 0.78f;
        public const float WeaponTransitionDuration = 1.00f;
        public const float HitStunDuration = 0.40f;
        public const float KnockdownDuration = 0.80f;
        public const float GetUpDuration = 1.8333334f;
        public const float AirborneReactionDuration = 0.45f;
        public const float DeathDuration = 2.40f;

        public static bool TryCreate(
            AnimationSlotId slot,
            out AnimationActionRequest request)
        {
            if (slot == PlayerAnimationSlots.DodgeForward)
            {
                request = new AnimationActionRequest(Dodge, 0, DodgeDuration);
                return true;
            }

            if (slot == PlayerAnimationSlots.DodgeBackward)
            {
                request = new AnimationActionRequest(Dodge, 1, DodgeDuration);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Back))
            {
                request = new AnimationActionRequest(Reaction, 6, HitStunDuration);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Left))
            {
                request = new AnimationActionRequest(Reaction, 7, HitStunDuration);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Right))
            {
                request = new AnimationActionRequest(Reaction, 8, HitStunDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.HitStun))
            {
                request = new AnimationActionRequest(Reaction, 0, HitStunDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.Knockdown))
            {
                request = new AnimationActionRequest(Reaction, 1, KnockdownDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.Airborne))
            {
                request = new AnimationActionRequest(Reaction, 2, AirborneReactionDuration);
                return true;
            }

            if (slot == PlayerAnimationSlots.HeavyDeath)
            {
                request = new AnimationActionRequest(Reaction, 5, DeathDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.Dead))
            {
                request = new AnimationActionRequest(Reaction, 3, DeathDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.GetUp))
            {
                request = new AnimationActionRequest(Reaction, 4, GetUpDuration);
                return true;
            }

            request = default;
            return false;
        }

        private static bool IsReactionSlot(
            AnimationSlotId slot,
            CharacterReactionStateId state)
        {
            string value = slot.Value;
            string prefix = PlayerAnimationSlots.Reaction(state).Value;
            return string.Equals(value, prefix, StringComparison.Ordinal) ||
                   value?.StartsWith(prefix + ".", StringComparison.Ordinal) == true;
        }

        private static bool IsDirectionalReactionSlot(
            AnimationSlotId slot,
            CharacterReactionStateId state,
            CharacterReactionDirection direction)
        {
            string value = slot.Value;
            string prefix = $"{PlayerAnimationSlots.Reaction(state).Value}.{direction}";
            return string.Equals(value, prefix, StringComparison.Ordinal) ||
                   value?.StartsWith(prefix + ".", StringComparison.Ordinal) == true;
        }
    }

    /// <summary>
    /// Coordinates semantic action ownership. It never selects clips or performs
    /// state transitions; those decisions belong to the Animator Controller.
    /// </summary>
    internal sealed class PlayerAnimationCoordinator
    {
        private readonly ICharacterAnimationDriver _driver;
        private readonly float[] _playbackSpeeds =
            new float[Enum.GetValues(typeof(PlayerAnimationSource)).Length];

        private PlayerAnimationSource? _actionOwner;

        public PlayerAnimationCoordinator(ICharacterAnimationDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            for (int i = 0; i < _playbackSpeeds.Length; i++)
            {
                _playbackSpeeds[i] = 1.0f;
            }
        }

        public bool TryPlayAction(
            PlayerAnimationSource source,
            in AnimationActionRequest request,
            out AnimationActionPlayback playback)
        {
            if (_actionOwner.HasValue &&
                _actionOwner.Value != source &&
                GetPriority(_actionOwner.Value) > GetPriority(source))
            {
                playback = default;
                return false;
            }

            playback = _driver.PlayAction(in request);
            if (!playback.IsValid)
            {
                return false;
            }

            _actionOwner = source;
            _driver.SetPlaybackSpeed(_playbackSpeeds[(int)source]);
            return true;
        }

        public AnimationPlayback CrossFade(
            PlayerAnimationSource source,
            AnimationSlotId slot,
            float expectedDurationOverride = 0.0f)
        {
            if (!PlayerAnimatorActions.TryCreate(slot, out AnimationActionRequest request))
            {
                return AnimationPlayback.Invalid;
            }

            if (expectedDurationOverride > 0.0f)
            {
                request = new AnimationActionRequest(
                    request.Action,
                    request.Variant,
                    expectedDurationOverride);
            }

            return TryPlayAction(source, in request, out AnimationActionPlayback action)
                ? new AnimationPlayback(action.Sequence, action.Duration)
                : AnimationPlayback.Invalid;
        }

        public bool Supports(AnimationSlotId slot)
        {
            return PlayerAnimatorActions.TryCreate(slot, out _);
        }

        public void StopAllActions()
        {
            _actionOwner = null;
            _driver.CancelAction();
        }

        public void Cancel(PlayerAnimationSource source)
        {
            if (_actionOwner == source)
            {
                _actionOwner = null;
                _driver.CancelAction();
            }
        }

        public void ReleaseAll(PlayerAnimationSource source)
        {
            if (_actionOwner == source)
            {
                _actionOwner = null;
                _driver.CompleteAction();
                _driver.SetPlaybackSpeed(1.0f);
            }

            _playbackSpeeds[(int)source] = 1.0f;
        }

        public void SetPlaybackSpeed(PlayerAnimationSource source, float speed)
        {
            float clampedSpeed = Mathf.Max(0.0f, speed);
            _playbackSpeeds[(int)source] = clampedSpeed;
            if (_actionOwner == source)
            {
                _driver.SetPlaybackSpeed(clampedSpeed);
            }
        }

        private static int GetPriority(PlayerAnimationSource source)
        {
            return source switch
            {
                PlayerAnimationSource.Death => 500,
                PlayerAnimationSource.Reaction => 400,
                PlayerAnimationSource.Traversal => 300,
                PlayerAnimationSource.Combat => 200,
                PlayerAnimationSource.Locomotion => 100,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
            };
        }
    }
}
