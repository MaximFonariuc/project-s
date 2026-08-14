using System;

namespace _App
{
    public sealed class CombatHitStopController : IDisposable
    {
        private readonly ICombatHitEvents _outgoingHits;
        private readonly Health _localHealth;
        private readonly IGameplayTimeScaleRequests _timeScale;
        private bool _enabled;

        public CombatHitStopController(
            ICombatHitEvents outgoingHits,
            Health localHealth,
            IGameplayTimeScaleRequests timeScale)
        {
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _localHealth = localHealth != null
                ? localHealth
                : throw new ArgumentNullException(nameof(localHealth));
            _timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved += OnHitConfirmed;
            _localHealth.Damaged += OnHitConfirmed;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved -= OnHitConfirmed;
            _localHealth.Damaged -= OnHitConfirmed;
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnHitConfirmed(CombatHitResult hit)
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
