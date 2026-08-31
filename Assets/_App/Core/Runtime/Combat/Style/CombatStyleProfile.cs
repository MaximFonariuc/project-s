using System;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "CombatStyleProfile",
        menuName = "Combat/Style Profile")]
    public sealed class CombatStyleProfile : ScriptableObject
    {
        [Header("Scoring")]
        [SerializeField, Min(0.0f)] private float _pointsPerHit = 20.0f;
        [SerializeField, Min(0.0f)] private float _pointsPerDamage = 1.5f;
        [SerializeField, Range(0.05f, 1.0f)] private float _repeatActionMultiplier = 0.65f;
        [SerializeField, Range(0.05f, 1.0f)] private float _minimumVarietyMultiplier = 0.25f;
        [SerializeField, Range(1, 12)] private int _recentActionWindow = 6;

        [Header("Reaction Bonuses")]
        [SerializeField, Min(1.0f)] private float _heavyMultiplier = 1.2f;
        [SerializeField, Min(1.0f)] private float _launcherMultiplier = 1.35f;
        [SerializeField, Min(1.0f)] private float _slamMultiplier = 1.4f;

        [Header("Flow")]
        [SerializeField, Min(0.0f)] private float _decayDelay = 2.5f;
        [SerializeField, Min(0.01f)] private float _scoreDecayPerSecond = 140.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float _incomingDamageRetention = 0.35f;

        [Header("Rank Thresholds")]
        [SerializeField, Min(1.0f)] private float _rankC = 180.0f;
        [SerializeField, Min(1.0f)] private float _rankB = 420.0f;
        [SerializeField, Min(1.0f)] private float _rankA = 760.0f;
        [SerializeField, Min(1.0f)] private float _rankS = 1150.0f;
        [SerializeField, Min(1.0f)] private float _rankSS = 1650.0f;
        [SerializeField, Min(1.0f)] private float _rankSSS = 2250.0f;

        internal CombatStyleSettings CreateSettings()
        {
            return new CombatStyleSettings(
                _pointsPerHit,
                _pointsPerDamage,
                _repeatActionMultiplier,
                _minimumVarietyMultiplier,
                _recentActionWindow,
                _heavyMultiplier,
                _launcherMultiplier,
                _slamMultiplier,
                _decayDelay,
                _scoreDecayPerSecond,
                _incomingDamageRetention,
                _rankC,
                _rankB,
                _rankA,
                _rankS,
                _rankSS,
                _rankSSS);
        }

        private void OnValidate()
        {
            _pointsPerHit = Mathf.Max(0.0f, _pointsPerHit);
            _pointsPerDamage = Mathf.Max(0.0f, _pointsPerDamage);
            _repeatActionMultiplier = Mathf.Clamp(_repeatActionMultiplier, 0.05f, 1.0f);
            _minimumVarietyMultiplier = Mathf.Clamp(_minimumVarietyMultiplier, 0.05f, 1.0f);
            _recentActionWindow = Mathf.Clamp(_recentActionWindow, 1, 12);
            _heavyMultiplier = Mathf.Max(1.0f, _heavyMultiplier);
            _launcherMultiplier = Mathf.Max(1.0f, _launcherMultiplier);
            _slamMultiplier = Mathf.Max(1.0f, _slamMultiplier);
            _decayDelay = Mathf.Max(0.0f, _decayDelay);
            _scoreDecayPerSecond = Mathf.Max(0.01f, _scoreDecayPerSecond);
            _incomingDamageRetention = Mathf.Clamp01(_incomingDamageRetention);

            _rankC = Mathf.Max(1.0f, _rankC);
            _rankB = Mathf.Max(_rankC + 1.0f, _rankB);
            _rankA = Mathf.Max(_rankB + 1.0f, _rankA);
            _rankS = Mathf.Max(_rankA + 1.0f, _rankS);
            _rankSS = Mathf.Max(_rankS + 1.0f, _rankSS);
            _rankSSS = Mathf.Max(_rankSS + 1.0f, _rankSSS);
        }
    }

    internal sealed class CombatStyleSettings
    {
        public float PointsPerHit { get; }
        public float PointsPerDamage { get; }
        public float RepeatActionMultiplier { get; }
        public float MinimumVarietyMultiplier { get; }
        public int RecentActionWindow { get; }
        public float HeavyMultiplier { get; }
        public float LauncherMultiplier { get; }
        public float SlamMultiplier { get; }
        public float DecayDelay { get; }
        public float ScoreDecayPerSecond { get; }
        public float IncomingDamageRetention { get; }
        public float RankC { get; }
        public float RankB { get; }
        public float RankA { get; }
        public float RankS { get; }
        public float RankSS { get; }
        public float RankSSS { get; }
        public float MaximumScore { get; }

        public CombatStyleSettings(
            float pointsPerHit,
            float pointsPerDamage,
            float repeatActionMultiplier,
            float minimumVarietyMultiplier,
            int recentActionWindow,
            float heavyMultiplier,
            float launcherMultiplier,
            float slamMultiplier,
            float decayDelay,
            float scoreDecayPerSecond,
            float incomingDamageRetention,
            float rankC,
            float rankB,
            float rankA,
            float rankS,
            float rankSS,
            float rankSSS)
        {
            RequireFinite(pointsPerHit, nameof(pointsPerHit));
            RequireFinite(pointsPerDamage, nameof(pointsPerDamage));
            RequireFinite(repeatActionMultiplier, nameof(repeatActionMultiplier));
            RequireFinite(minimumVarietyMultiplier, nameof(minimumVarietyMultiplier));
            RequireFinite(heavyMultiplier, nameof(heavyMultiplier));
            RequireFinite(launcherMultiplier, nameof(launcherMultiplier));
            RequireFinite(slamMultiplier, nameof(slamMultiplier));
            RequireFinite(decayDelay, nameof(decayDelay));
            RequireFinite(scoreDecayPerSecond, nameof(scoreDecayPerSecond));
            RequireFinite(incomingDamageRetention, nameof(incomingDamageRetention));
            RequireFinite(rankC, nameof(rankC));
            RequireFinite(rankB, nameof(rankB));
            RequireFinite(rankA, nameof(rankA));
            RequireFinite(rankS, nameof(rankS));
            RequireFinite(rankSS, nameof(rankSS));
            RequireFinite(rankSSS, nameof(rankSSS));

            if (pointsPerHit < 0.0f || pointsPerDamage < 0.0f ||
                repeatActionMultiplier <= 0.0f || repeatActionMultiplier > 1.0f ||
                minimumVarietyMultiplier <= 0.0f || minimumVarietyMultiplier > 1.0f ||
                recentActionWindow <= 0 ||
                heavyMultiplier < 1.0f || launcherMultiplier < 1.0f || slamMultiplier < 1.0f ||
                decayDelay < 0.0f || scoreDecayPerSecond <= 0.0f ||
                incomingDamageRetention < 0.0f || incomingDamageRetention > 1.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pointsPerHit),
                    "Combat style settings contain an out-of-range value.");
            }

            if (!(rankC > 0.0f && rankB > rankC && rankA > rankB &&
                  rankS > rankA && rankSS > rankS && rankSSS > rankSS))
            {
                throw new ArgumentException("Combat style rank thresholds must be strictly increasing.");
            }

            PointsPerHit = pointsPerHit;
            PointsPerDamage = pointsPerDamage;
            RepeatActionMultiplier = repeatActionMultiplier;
            MinimumVarietyMultiplier = minimumVarietyMultiplier;
            RecentActionWindow = recentActionWindow;
            HeavyMultiplier = heavyMultiplier;
            LauncherMultiplier = launcherMultiplier;
            SlamMultiplier = slamMultiplier;
            DecayDelay = decayDelay;
            ScoreDecayPerSecond = scoreDecayPerSecond;
            IncomingDamageRetention = incomingDamageRetention;
            RankC = rankC;
            RankB = rankB;
            RankA = rankA;
            RankS = rankS;
            RankSS = rankSS;
            RankSSS = rankSSS;
            MaximumScore = rankSSS * 1.35f;
        }

        public CombatStyleRank ResolveRank(float score)
        {
            if (score <= 0.0f)
            {
                return CombatStyleRank.None;
            }

            if (score >= RankSSS)
            {
                return CombatStyleRank.SSS;
            }

            if (score >= RankSS)
            {
                return CombatStyleRank.SS;
            }

            if (score >= RankS)
            {
                return CombatStyleRank.S;
            }

            if (score >= RankA)
            {
                return CombatStyleRank.A;
            }

            if (score >= RankB)
            {
                return CombatStyleRank.B;
            }

            return score >= RankC ? CombatStyleRank.C : CombatStyleRank.D;
        }

        public float GetRankProgress(float score)
        {
            CombatStyleRank rank = ResolveRank(score);
            return rank switch
            {
                CombatStyleRank.None => 0.0f,
                CombatStyleRank.D => Mathf.InverseLerp(0.0f, RankC, score),
                CombatStyleRank.C => Mathf.InverseLerp(RankC, RankB, score),
                CombatStyleRank.B => Mathf.InverseLerp(RankB, RankA, score),
                CombatStyleRank.A => Mathf.InverseLerp(RankA, RankS, score),
                CombatStyleRank.S => Mathf.InverseLerp(RankS, RankSS, score),
                CombatStyleRank.SS => Mathf.InverseLerp(RankSS, RankSSS, score),
                CombatStyleRank.SSS => 1.0f,
                _ => 0.0f
            };
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
