using System;
using UnityEngine;

namespace _App
{
    public sealed class GameplayTimeScaleController :
        IGameplayTimeScaleRequests,
        IGameplayTimeScaleState,
        IDisposable
    {
        private readonly ITimeScaleBackend _backend;

        private float _baselineScale = 1.0f;
        private float _requestScale = 1.0f;
        private float _remainingUnscaledTime;
        private bool _enabled;

        public bool IsActive => _enabled && _remainingUnscaledTime > 0.0f;
        public float CurrentScale => _backend.TimeScale;
        public float RemainingUnscaledTime => _remainingUnscaledTime;

        public GameplayTimeScaleController(ITimeScaleBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _baselineScale = Mathf.Max(0.0f, _backend.TimeScale);
            _requestScale = 1.0f;
            _remainingUnscaledTime = 0.0f;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            RestoreBaseline();
            _enabled = false;
        }

        public void Request(in GameplayTimeScaleRequest request)
        {
            if (!_enabled)
            {
                throw new InvalidOperationException(
                    "Gameplay time scale controller must be enabled before accepting requests.");
            }

            bool wasActive = IsActive;
            _remainingUnscaledTime = Mathf.Max(_remainingUnscaledTime, request.Duration);
            _requestScale = wasActive
                ? Mathf.Min(_requestScale, request.Scale)
                : request.Scale;
            _backend.TimeScale = _baselineScale * _requestScale;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || unscaledDeltaTime <= 0.0f)
            {
                return;
            }

            _remainingUnscaledTime = Mathf.Max(
                0.0f,
                _remainingUnscaledTime - unscaledDeltaTime);
            if (_remainingUnscaledTime <= 0.0f)
            {
                RestoreBaseline();
            }
        }

        public void Dispose()
        {
            Disable();
        }

        private void RestoreBaseline()
        {
            _backend.TimeScale = _baselineScale;
            _requestScale = 1.0f;
            _remainingUnscaledTime = 0.0f;
        }
    }
}
