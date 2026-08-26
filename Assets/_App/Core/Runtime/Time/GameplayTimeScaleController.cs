using System;
using UnityEngine;

namespace _App
{
    public sealed class GameplayTimeScaleController :
        IGameplayTimeScaleRequests,
        IGameplayTimeScaleState,
        IDisposable
    {
        private const int RequestCapacity = 32;

        private struct ActiveRequest
        {
            public float RemainingTime;
            public float Scale;

            public bool IsActive => RemainingTime > 0.0f;

            public void Set(in GameplayTimeScaleRequest request)
            {
                RemainingTime = request.Duration;
                Scale = request.Scale;
            }

            public void Clear()
            {
                RemainingTime = 0.0f;
                Scale = 1.0f;
            }
        }

        private readonly ITimeScaleBackend _backend;
        private readonly ActiveRequest[] _requests = new ActiveRequest[RequestCapacity];

        private float _baselineScale = 1.0f;
        private float _effectiveRequestScale = 1.0f;
        private float _remainingUnscaledTime;
        private int _activeRequestCount;
        private bool _reportedSaturation;
        private bool _enabled;

        public bool IsActive => _enabled && _activeRequestCount > 0;
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
            ClearRequests();
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

            int slot = FindAvailableSlot();
            if (slot < 0)
            {
                slot = FindShortestRequest();
                if (!_reportedSaturation)
                {
                    Debug.LogWarning(
                        $"Gameplay time scale request capacity ({RequestCapacity}) was saturated. " +
                        "The shortest requests will be conservatively merged.");
                    _reportedSaturation = true;
                }

                ActiveRequest existing = _requests[slot];
                _requests[slot].RemainingTime = Mathf.Max(
                    existing.RemainingTime,
                    request.Duration);
                _requests[slot].Scale = Mathf.Min(existing.Scale, request.Scale);
            }
            else
            {
                _requests[slot].Set(in request);
                _activeRequestCount++;
            }

            ApplyEffectiveScale();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || unscaledDeltaTime <= 0.0f)
            {
                return;
            }

            for (int i = 0; i < _requests.Length; i++)
            {
                if (!_requests[i].IsActive)
                {
                    continue;
                }

                _requests[i].RemainingTime = Mathf.Max(
                    0.0f,
                    _requests[i].RemainingTime - unscaledDeltaTime);
                if (_requests[i].IsActive)
                {
                    continue;
                }

                _requests[i].Clear();
                _activeRequestCount--;
            }

            ApplyEffectiveScale();
        }

        public void Dispose()
        {
            Disable();
        }

        private void RestoreBaseline()
        {
            _backend.TimeScale = _baselineScale;
            ClearRequests();
        }

        private int FindAvailableSlot()
        {
            for (int i = 0; i < _requests.Length; i++)
            {
                if (!_requests[i].IsActive)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindShortestRequest()
        {
            int shortestIndex = 0;
            float shortestTime = _requests[0].RemainingTime;
            for (int i = 1; i < _requests.Length; i++)
            {
                if (_requests[i].RemainingTime >= shortestTime)
                {
                    continue;
                }

                shortestIndex = i;
                shortestTime = _requests[i].RemainingTime;
            }

            return shortestIndex;
        }

        private void ApplyEffectiveScale()
        {
            float scale = 1.0f;
            float remainingTime = 0.0f;
            for (int i = 0; i < _requests.Length; i++)
            {
                if (!_requests[i].IsActive)
                {
                    continue;
                }

                scale = Mathf.Min(scale, _requests[i].Scale);
                remainingTime = Mathf.Max(remainingTime, _requests[i].RemainingTime);
            }

            _effectiveRequestScale = scale;
            _remainingUnscaledTime = remainingTime;
            _backend.TimeScale = _baselineScale * _effectiveRequestScale;
        }

        private void ClearRequests()
        {
            for (int i = 0; i < _requests.Length; i++)
            {
                _requests[i].Clear();
            }

            _effectiveRequestScale = 1.0f;
            _remainingUnscaledTime = 0.0f;
            _activeRequestCount = 0;
            _reportedSaturation = false;
        }
    }
}
