using System;
using UnityEngine;

namespace _App
{
    public sealed class CombatHitStopController : IDisposable
    {
        private readonly ICombatHitEvents _outgoingHits;
        private readonly Health _localHealth;
        private readonly Transform _localRoot;
        private readonly IGameplayTimeScaleRequests _timeScale;
        private bool _enabled;

        public CombatHitStopController(
            ICombatHitEvents outgoingHits,
            Health localHealth,
            IPlayerCombatActor localActor,
            IGameplayTimeScaleRequests timeScale)
        {
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _localHealth = localHealth != null
                ? localHealth
                : throw new ArgumentNullException(nameof(localHealth));
            _localRoot = localActor?.Root != null
                ? localActor.Root
                : throw new ArgumentNullException(nameof(localActor));
            _timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved += OnOutgoingHitConfirmed;
            _localHealth.Damaged += OnIncomingHitConfirmed;
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

        private void RequestHitStop(in CombatHitResult hit)
        {
            if (!_enabled || !hit.HitStop.IsEnabled)
            {
                return;
            }

            _timeScale.Request(
                new GameplayTimeScaleRequest(
                    hit.HitStop.Duration,
                    hit.HitStop.TimeScale));
        }
    }
}
