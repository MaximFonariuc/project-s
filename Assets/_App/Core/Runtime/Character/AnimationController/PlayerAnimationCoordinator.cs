using System;
using System.Collections.Generic;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    internal enum PlayerAnimationSource
    {
        Locomotion,
        Combat,
        Defense,
        Traversal,
        Reaction,
        Death
    }

    internal static class PlayerAnimatorActions
    {
        // Presentation capacity only. Each moveset's action graph decides how
        // many of these variants are reachable in its combo chain.
        public const int LightAttackVariantCount = 4;
        public const int AirAttackVariantCount = 3;

        public static readonly AnimationActionId Dodge = new AnimationActionId(1);
        public static readonly AnimationActionId LightAttack = new AnimationActionId(2);
        public static readonly AnimationActionId WeaponTransition = new AnimationActionId(3);
        public static readonly AnimationActionId Reaction = new AnimationActionId(4);
        public static readonly AnimationActionId AirAttack = new AnimationActionId(5);
        public static readonly AnimationActionId SpecialAttack = new AnimationActionId(6);
        public static readonly AnimationActionId HeavyAttack = new AnimationActionId(7);
        public static readonly AnimationActionId Launcher = new AnimationActionId(8);
        public static readonly AnimationActionId AirChase = new AnimationActionId(9);
        public static readonly AnimationActionId Guard = new AnimationActionId(10);
        public static readonly AnimationActionId Block = new AnimationActionId(11);
        public static readonly AnimationActionId Parry = new AnimationActionId(12);
        public static readonly AnimationActionId LadderTraversal = new AnimationActionId(13);


        public const float DeathDuration = 2.40f;

        private const float CallerSuppliedDuration = 0.0f;

        private static readonly AnimationSlotId[] LadderSlots =
        {
            PlayerAnimationSlots.LadderEnterTopFront,
            PlayerAnimationSlots.LadderEnterTopBack,
            PlayerAnimationSlots.LadderEnterBottom,
            PlayerAnimationSlots.LadderIdle,
            PlayerAnimationSlots.LadderAscend,
            PlayerAnimationSlots.LadderDescend,
            PlayerAnimationSlots.LadderExitTop,
            PlayerAnimationSlots.LadderExitBottom
        };

        private static readonly Dictionary<AnimationSlotId, AnimationActionRequest>
            RequestsBySlot = BuildRequestTable();

        public static bool TryCreate(
            AnimationSlotId slot,
            out AnimationActionRequest request)
        {
            return RequestsBySlot.TryGetValue(slot, out request);
        }

        private static Dictionary<AnimationSlotId, AnimationActionRequest> BuildRequestTable()
        {
            var table = new Dictionary<AnimationSlotId, AnimationActionRequest>();
            Register(table, PlayerAnimationSlots.DodgeForward);
            Register(table, PlayerAnimationSlots.DodgeBackward);
            Register(table, PlayerAnimationSlots.HeavyDeath);
            for (int i = 0; i < LadderSlots.Length; i++)
            {
                Register(table, LadderSlots[i]);
            }


            foreach (CharacterReactionStateId state in
                     Enum.GetValues(typeof(CharacterReactionStateId)))
            {
                Register(table, PlayerAnimationSlots.Reaction(state));
                foreach (CharacterReactionKind kind in
                         Enum.GetValues(typeof(CharacterReactionKind)))
                {
                    Register(table, PlayerAnimationSlots.Reaction(kind, state));
                    foreach (CharacterReactionDirection direction in
                             Enum.GetValues(typeof(CharacterReactionDirection)))
                    {
                        Register(table, PlayerAnimationSlots.Reaction(kind, state, direction));
                    }
                }
            }

            return table;
        }

        private static void Register(
            Dictionary<AnimationSlotId, AnimationActionRequest> table,
            AnimationSlotId slot)
        {
            if (!table.ContainsKey(slot) && Resolve(slot, out AnimationActionRequest request))
            {
                table.Add(slot, request);
            }
        }

        private static bool Resolve(
            AnimationSlotId slot,
            out AnimationActionRequest request)
        {
            for (int i = 0; i < LadderSlots.Length; i++)
            {
                if (slot == LadderSlots[i])
                {
                    request = new AnimationActionRequest(
                        LadderTraversal,
                        i,
                        CallerSuppliedDuration);
                    return true;
                }
            }

            if (slot == PlayerAnimationSlots.DodgeForward)
            {
                request = new AnimationActionRequest(Dodge, 0, 1.0f);
                return true;
            }

            if (slot == PlayerAnimationSlots.DodgeBackward)
            {
                request = new AnimationActionRequest(Dodge, 1, 1.0f);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Back))
            {
                request = new AnimationActionRequest(Reaction, 6, CallerSuppliedDuration);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Left))
            {
                request = new AnimationActionRequest(Reaction, 7, CallerSuppliedDuration);
                return true;
            }

            if (IsDirectionalReactionSlot(
                    slot,
                    CharacterReactionStateId.HitStun,
                    CharacterReactionDirection.Right))
            {
                request = new AnimationActionRequest(Reaction, 8, CallerSuppliedDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.HitStun))
            {
                request = new AnimationActionRequest(Reaction, 0, CallerSuppliedDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.Knockdown))
            {
                request = new AnimationActionRequest(Reaction, 1, CallerSuppliedDuration);
                return true;
            }

            if (IsReactionSlot(slot, CharacterReactionStateId.Airborne))
            {
                request = new AnimationActionRequest(Reaction, 2, CallerSuppliedDuration);
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
                request = new AnimationActionRequest(Reaction, 4, CallerSuppliedDuration);
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

            _driver.Reset();
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
            else if (request.ExpectedDuration <= 0.0f)
            {
                Debug.LogError(
                    $"Animation slot '{slot}' requires a caller-supplied duration " +
                    $"but {source} passed none.");
                return AnimationPlayback.Invalid;
            }

            return TryPlayAction(source, in request, out AnimationActionPlayback action)
                ? new AnimationPlayback(action.Sequence, action.Duration)
                : AnimationPlayback.Invalid;
        }

        public bool Supports(AnimationSlotId slot)
        {
            return PlayerAnimatorActions.TryCreate(slot, out _);
        }

        public void Reset()
        {
            _actionOwner = null;
            _driver.CancelAction();
            for (int i = 0; i < _playbackSpeeds.Length; i++)
            {
                _playbackSpeeds[i] = 1.0f;
            }

            _driver.Reset();
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
                PlayerAnimationSource.Defense => 250,
                PlayerAnimationSource.Combat => 200,
                PlayerAnimationSource.Locomotion => 100,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
            };
        }
    }
}
