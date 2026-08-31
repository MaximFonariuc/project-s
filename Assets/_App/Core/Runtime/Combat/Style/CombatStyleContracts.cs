using System;

namespace _App
{
    public enum CombatStyleRank
    {
        None = 0,
        D = 1,
        C = 2,
        B = 3,
        A = 4,
        S = 5,
        SS = 6,
        SSS = 7
    }

    public readonly struct CombatStyleSnapshot
    {
        public static CombatStyleSnapshot Empty => default;

        public bool IsActive { get; }
        public CombatStyleRank Rank { get; }
        public float Score { get; }
        public int ComboHitCount { get; }
        public int VarietyCount { get; }
        public bool IsDecaying { get; }

        internal CombatStyleSnapshot(
            CombatStyleRank rank,
            float score,
            int comboHitCount,
            int varietyCount,
            bool isDecaying)
        {
            IsActive = rank != CombatStyleRank.None && score > 0.0f;
            Rank = rank;
            Score = Math.Max(0.0f, score);
            ComboHitCount = Math.Max(0, comboHitCount);
            VarietyCount = Math.Max(0, varietyCount);
            IsDecaying = IsActive && isDecaying;
        }
    }

    public interface ICombatStyleState
    {
        event Action<CombatStyleSnapshot> Changed;

        CombatStyleSnapshot Snapshot { get; }
    }
}
