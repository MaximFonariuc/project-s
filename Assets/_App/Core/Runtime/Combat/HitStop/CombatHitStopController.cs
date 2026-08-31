using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatHitStopController : IDisposable
    {
        private readonly ICombatHitEvents _outgoingHits;
        private readonly Health _localHealth;
        private readonly IPlayerDefenseState _localDefense;
        private readonly Transform _localRoot;
        private readonly IGameplayTimeScaleRequests _timeScale;
        private readonly CombatFeedbackSettings _feedback;
        private bool _enabled;

        public CombatHitStopController(
            ICombatHitEvents outgoingHits,
            Health localHealth,
            IPlayerDefenseState localDefense,
            IPlayerCombatActor localActor,
            IGameplayTimeScaleRequests timeScale,
            CombatFeedbackSettings feedback)
        {
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _localHealth = localHealth != null
                ? localHealth
                : throw new ArgumentNullException(nameof(localHealth));
            _localDefense = localDefense ??
                throw new ArgumentNullException(nameof(localDefense));
            _localRoot = localActor?.Root != null
                ? localActor.Root
                : throw new ArgumentNullException(nameof(localActor));
            _timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
            _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved += OnOutgoingHitConfirmed;
            _localHealth.Damaged += OnIncomingHitConfirmed;
            _localDefense.Defended += OnIncomingDefenseConfirmed;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved -= OnOutgoingHitConfirmed;
            _localHealth.Damaged -= OnIncomingHitConfirmed;
            _localDefense.Defended -= OnIncomingDefenseConfirmed;
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnOutgoingHitConfirmed(CombatHitResult hit)
        {
            GameObject source = hit.Source;
            if (source == null ||
                (source.transform != _localRoot && !source.transform.IsChildOf(_localRoot)))
            {
                return;
            }

            RequestHitStop(in hit);
        }

        private void OnIncomingHitConfirmed(CombatHitResult hit)
        {
            RequestHitStop(in hit);
        }

        private void OnIncomingDefenseConfirmed(PlayerDefenseResult result)
        {
            CombatHitResult hit = result.Hit;
            RequestHitStop(in hit);
        }

        private void RequestHitStop(in CombatHitResult hit)
        {
            if (!_enabled)
            {
                return;
            }

            CombatHitStop hitStop = _feedback.ResolveHitStop(in hit);
            if (!hitStop.IsEnabled)
            {
                return;
            }

            _timeScale.Request(
                new GameplayTimeScaleRequest(
                    hitStop.Duration,
                    hitStop.TimeScale));
        }
    }
}
