using System;
using UnityEngine;

namespace _App
{
    public enum CharacterReactionStateId
    {
        Stable = 0,
        HitStun = 1,
        Knockdown = 2,
        Airborne = 3,
        Dead = 4,
        GetUp = 5
    }

    public readonly struct CharacterReactionSnapshot
    {
        public static CharacterReactionSnapshot Invalid => default;

        public bool IsValid { get; }
        public CharacterReactionStateId State { get; }
        public CombatHitResult Hit { get; }
        public float CurrentPoise { get; }
        public float MaxPoise { get; }
        public float RemainingDuration { get; }
        public bool BlocksActions => IsValid && State != CharacterReactionStateId.Stable;

        internal CharacterReactionSnapshot(
            CharacterReactionStateId state,
            in CombatHitResult hit,
            float currentPoise,
            float maxPoise,
            float remainingDuration)
        {
            IsValid = true;
            State = state;
            Hit = hit;
            CurrentPoise = Mathf.Max(0.0f, currentPoise);
            MaxPoise = Mathf.Max(0.0f, maxPoise);
            RemainingDuration = Mathf.Max(0.0f, remainingDuration);
        }
    }

    public interface ICharacterReactionState
    {
        event Action<CharacterReactionStateId> StateChanged;
        event Action<CharacterReactionSnapshot> ReactionStarted;

        CharacterReactionStateId? CurrentState { get; }
        CharacterReactionSnapshot Snapshot { get; }
        float CurrentPoise { get; }
        float MaxPoise { get; }
        bool BlocksActions { get; }
    }

    internal readonly struct CharacterReactionContext
    {
        public CombatHitResult Hit { get; }
        public float Duration { get; }

        public CharacterReactionContext(in CombatHitResult hit, float duration)
        {
            Hit = hit;
            Duration = Mathf.Max(0.0f, duration);
        }
    }

    internal interface ICharacterReactionPresentation
    {
        void Present(in CharacterReactionSnapshot snapshot);
        void Reset();
    }

}
