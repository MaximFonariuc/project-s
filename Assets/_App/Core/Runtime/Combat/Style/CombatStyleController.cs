using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatStyleController : ICombatStyleState, IDisposable
    {
        private readonly ICombatHitEvents _outgoingHits;
        private readonly Health _localHealth;
        private readonly Transform _localRoot;
        private readonly CombatStyleSettings _settings;
        private readonly CombatActionId[] _recentActions;

        private float _score;
        private float _timeSinceAward;
        private int _comboHitCount;
        private int _recentActionCount;
        private int _nextRecentAction;
        private int _varietyCount;
        private bool _enabled;

        public event Action<CombatStyleSnapshot> Changed;

        public CombatStyleSnapshot Snapshot => CreateSnapshot();

        public CombatStyleController(
            ICombatHitEvents outgoingHits,
            Health localHealth,
            IPlayerCombatActor localActor,
            CombatStyleSettings settings)
        {
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _localHealth = localHealth != null
                ? localHealth
                : throw new ArgumentNullException(nameof(localHealth));
            _localRoot = localActor?.Root != null
                ? localActor.Root
                : throw new ArgumentNullException(nameof(localActor));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _recentActions = new CombatActionId[_settings.RecentActionWindow];
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            Reset(publish: false);
            _outgoingHits.HitResolved += OnHitResolved;
            _localHealth.Damaged += OnLocalDamaged;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved -= OnHitResolved;
            _localHealth.Damaged -= OnLocalDamaged;
            _enabled = false;
            Reset(publish: true);
        }

        public void Tick(float scaledDeltaTime)
        {
            if (!_enabled || _score <= 0.0f)
            {
                return;
            }

            float deltaTime = Mathf.Max(0.0f, scaledDeltaTime);
            float previousTimeSinceAward = _timeSinceAward;
            _timeSinceAward += deltaTime;
            if (_timeSinceAward < _settings.DecayDelay)
            {
                return;
            }

            bool chainExpired = _comboHitCount > 0;
            if (chainExpired)
            {
                ClearChain();
            }

            float decayStart = Mathf.Max(
                previousTimeSinceAward,
                _settings.DecayDelay);
            float decayDeltaTime = Mathf.Max(
                0.0f,
                _timeSinceAward - decayStart);
            float previousScore = _score;
            _score = Mathf.Max(
                0.0f,
                _score - _settings.ScoreDecayPerSecond * decayDeltaTime);
            if (_score <= 0.0f)
            {
                ClearChain();
            }

            if (chainExpired || !Mathf.Approximately(previousScore, _score))
            {
                PublishChanged();
            }
        }

        public void Dispose()
        {
            Disable();
            Changed = null;
        }

        private void OnHitResolved(CombatHitResult hit)
        {
            if (!_enabled || hit.Outcome != CombatHitOutcome.Damaged ||
                !IsLocalSource(hit.Source) || hit.Damage <= 0 ||
                string.IsNullOrEmpty(hit.ActionId.Value))
            {
                return;
            }

            int repeatCount = CountRecent(hit.ActionId);
            float varietyMultiplier = Mathf.Max(
                _settings.MinimumVarietyMultiplier,
                Mathf.Pow(_settings.RepeatActionMultiplier, repeatCount));
            float reactionMultiplier = ResolveReactionMultiplier(hit.ReactionId);
            float award = (_settings.PointsPerHit + hit.Damage * _settings.PointsPerDamage) *
                          varietyMultiplier * reactionMultiplier;

            _score = Mathf.Min(_settings.MaximumScore, _score + award);
            _comboHitCount++;
            _timeSinceAward = 0.0f;
            RecordRecent(hit.ActionId);
            PublishChanged();
        }

        private void OnLocalDamaged(CombatHitResult hit)
        {
            if (!_enabled || _score <= 0.0f)
            {
                return;
            }

            _score *= _settings.IncomingDamageRetention;
            _timeSinceAward = _settings.DecayDelay;
            ClearChain();
            if (_score <= 0.0f)
            {
                _score = 0.0f;
            }

            PublishChanged();
        }

        private float ResolveReactionMultiplier(CombatReactionId reactionId)
        {
            if (reactionId == CombatReactionIds.Slam)
            {
                return _settings.SlamMultiplier;
            }

            if (reactionId == CombatReactionIds.Launcher)
            {
                return _settings.LauncherMultiplier;
            }

            return reactionId == CombatReactionIds.Heavy
                ? _settings.HeavyMultiplier
                : 1.0f;
        }

        private int CountRecent(CombatActionId actionId)
        {
            int count = 0;
            for (int i = 0; i < _recentActionCount; i++)
            {
                if (_recentActions[i] == actionId)
                {
                    count++;
                }
            }

            return count;
        }

        private void RecordRecent(CombatActionId actionId)
        {
            _recentActions[_nextRecentAction] = actionId;
            _nextRecentAction = (_nextRecentAction + 1) % _recentActions.Length;
            _recentActionCount = Mathf.Min(_recentActionCount + 1, _recentActions.Length);
            _varietyCount = CountDistinctRecentActions();
        }

        private int CountDistinctRecentActions()
        {
            int distinct = 0;
            for (int i = 0; i < _recentActionCount; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++)
                {
                    if (_recentActions[j] == _recentActions[i])
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    distinct++;
                }
            }

            return distinct;
        }

        private CombatStyleSnapshot CreateSnapshot()
        {
            return new CombatStyleSnapshot(
                _settings.ResolveRank(_score),
                _score,
                _comboHitCount,
                _varietyCount,
                _timeSinceAward >= _settings.DecayDelay);
        }

        private void Reset(bool publish)
        {
            bool hadState = _score > 0.0f || _comboHitCount > 0 || _recentActionCount > 0;
            _score = 0.0f;
            _timeSinceAward = 0.0f;
            ClearChain();
            if (publish && hadState)
            {
                PublishChanged();
            }
        }

        private void ClearChain()
        {
            Array.Clear(_recentActions, 0, _recentActions.Length);
            _comboHitCount = 0;
            _recentActionCount = 0;
            _nextRecentAction = 0;
            _varietyCount = 0;
        }

        private void PublishChanged()
        {
            CombatStyleSnapshot snapshot = CreateSnapshot();
            SafeEvent.Publish(Changed, snapshot, _localRoot);
        }

        private bool IsLocalSource(GameObject source)
        {
            return source != null &&
                   (source.transform == _localRoot || source.transform.IsChildOf(_localRoot));
        }
    }
}
