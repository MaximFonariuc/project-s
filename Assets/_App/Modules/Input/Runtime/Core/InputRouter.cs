using System;
using System.Collections.Generic;

namespace ModularInput
{
    public sealed class InputRouter : IInputService
    {
        private readonly List<IInputSource> _sources = new List<IInputSource>();
        private readonly Dictionary<SourceActionKey, SourceState> _sourceStates =
            new Dictionary<SourceActionKey, SourceState>();
        private readonly Dictionary<InputActionId, InputSignal> _currentSignals =
            new Dictionary<InputActionId, InputSignal>();

        private long _sequence;
        private bool _disposed;

        public event Action<InputSignal> SignalChanged;

        public bool IsRunning { get; private set; }

        public InputRouter(IInputSource primarySource)
        {
            AttachSource(primarySource ?? throw new ArgumentNullException(nameof(primarySource)));
        }

        public bool AttachSource(IInputSource source)
        {
            ThrowIfDisposed();

            if (source == null || !source.SourceId.IsValid || FindSource(source.SourceId) != null)
            {
                return false;
            }

            _sources.Add(source);
            if (IsRunning)
            {
                source.SignalEmitted += OnSignalEmitted;
                source.Start();
            }

            return true;
        }

        public bool DetachSource(InputSourceId sourceId)
        {
            ThrowIfDisposed();

            IInputSource source = FindSource(sourceId);
            if (source == null)
            {
                return false;
            }

            source.SignalEmitted -= OnSignalEmitted;
            source.Stop();
            _sources.Remove(source);
            RemoveSourceState(sourceId);
            source.Dispose();
            return true;
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            foreach (IInputSource source in _sources)
            {
                source.SignalEmitted += OnSignalEmitted;
                source.Start();
            }
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            foreach (IInputSource source in _sources)
            {
                source.SignalEmitted -= OnSignalEmitted;
                source.Stop();
            }

            IsRunning = false;
            _sourceStates.Clear();
            _currentSignals.Clear();
        }

        public bool TryReadValue(InputActionId actionId, out InputValue value)
        {
            if (_currentSignals.TryGetValue(actionId, out InputSignal signal))
            {
                value = signal.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool SetMapEnabled(InputMapId mapId, bool enabled)
        {
            bool changed = false;
            foreach (IInputSource source in _sources)
            {
                changed |= source.SetMapEnabled(mapId, enabled);
            }

            return changed;
        }

        public void DisableAllMaps()
        {
            foreach (IInputSource source in _sources)
            {
                source.DisableAllMaps();
            }
        }

        public bool ApplyBindingOverride(InputActionId actionId, int bindingIndex, string controlPath)
        {
            foreach (IInputSource source in _sources)
            {
                if (source is IInputBindingOverrides bindings &&
                    bindings.ApplyBindingOverride(actionId, bindingIndex, controlPath))
                {
                    return true;
                }
            }

            return false;
        }

        public bool RemoveBindingOverride(InputActionId actionId, int bindingIndex)
        {
            foreach (IInputSource source in _sources)
            {
                if (source is IInputBindingOverrides bindings &&
                    bindings.RemoveBindingOverride(actionId, bindingIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public string SaveBindingOverrides()
        {
            foreach (IInputSource source in _sources)
            {
                if (source is IInputBindingOverrides bindings)
                {
                    return bindings.SaveBindingOverrides();
                }
            }

            return string.Empty;
        }

        public bool LoadBindingOverrides(string json, bool removeExisting = true)
        {
            foreach (IInputSource source in _sources)
            {
                if (source is IInputBindingOverrides bindings && bindings.LoadBindingOverrides(json, removeExisting))
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            foreach (IInputSource source in _sources)
            {
                source.Dispose();
            }

            _sources.Clear();
            _disposed = true;
        }

        private void OnSignalEmitted(InputSignal signal)
        {
            if (!signal.SourceId.IsValid || !signal.ActionId.IsValid)
            {
                return;
            }

            SourceActionKey key = new SourceActionKey(signal.SourceId, signal.ActionId);
            bool active = signal.Phase != InputSignalPhase.Canceled;
            _sourceStates[key] = new SourceState(signal, ++_sequence, active);
            PublishCurrent(signal.ActionId, signal);
        }

        private void PublishCurrent(InputActionId actionId, InputSignal fallback)
        {
            bool found = false;
            SourceState winner = default;

            foreach (KeyValuePair<SourceActionKey, SourceState> pair in _sourceStates)
            {
                if (pair.Key.ActionId != actionId || !pair.Value.Active)
                {
                    continue;
                }

                if (!found || pair.Value.Sequence > winner.Sequence)
                {
                    found = true;
                    winner = pair.Value;
                }
            }

            InputSignal current = found
                ? winner.Signal.AsCurrent(winner.Signal.Phase == InputSignalPhase.Started
                    ? InputSignalPhase.Started
                    : InputSignalPhase.Performed)
                : fallback.AsCanceled();

            if (found)
            {
                _currentSignals[actionId] = current;
            }
            else
            {
                _currentSignals.Remove(actionId);
            }

            SignalChanged?.Invoke(current);
        }

        private void RemoveSourceState(InputSourceId sourceId)
        {
            Dictionary<InputActionId, InputSignal> affectedActions = new Dictionary<InputActionId, InputSignal>();
            List<SourceActionKey> keysToRemove = new List<SourceActionKey>();

            foreach (KeyValuePair<SourceActionKey, SourceState> pair in _sourceStates)
            {
                if (pair.Key.SourceId == sourceId)
                {
                    keysToRemove.Add(pair.Key);
                    affectedActions[pair.Key.ActionId] = pair.Value.Signal;
                }
            }

            foreach (SourceActionKey key in keysToRemove)
            {
                _sourceStates.Remove(key);
            }

            foreach (KeyValuePair<InputActionId, InputSignal> pair in affectedActions)
            {
                PublishCurrent(pair.Key, pair.Value);
            }
        }

        private IInputSource FindSource(InputSourceId sourceId)
        {
            foreach (IInputSource source in _sources)
            {
                if (source.SourceId == sourceId)
                {
                    return source;
                }
            }

            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InputRouter));
            }
        }

        private readonly struct SourceActionKey : IEquatable<SourceActionKey>
        {
            public InputSourceId SourceId { get; }
            public InputActionId ActionId { get; }

            public SourceActionKey(InputSourceId sourceId, InputActionId actionId)
            {
                SourceId = sourceId;
                ActionId = actionId;
            }

            public bool Equals(SourceActionKey other) => SourceId == other.SourceId && ActionId == other.ActionId;
            public override bool Equals(object obj) => obj is SourceActionKey other && Equals(other);
            public override int GetHashCode() => (SourceId.GetHashCode() * 397) ^ ActionId.GetHashCode();
        }

        private readonly struct SourceState
        {
            public InputSignal Signal { get; }
            public long Sequence { get; }
            public bool Active { get; }

            public SourceState(InputSignal signal, long sequence, bool active)
            {
                Signal = signal;
                Sequence = sequence;
                Active = active;
            }
        }
    }
}
