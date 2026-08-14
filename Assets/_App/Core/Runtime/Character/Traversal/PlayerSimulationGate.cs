using System;

namespace _App
{
    internal sealed class PlayerSimulationGate :
        IPlayerSimulationPause,
        IPlayerSimulationLock,
        IDisposable
    {
        private readonly Character _character;

        private int _lockCount;
        private bool _pauseRequested;
        private bool _pauseApplied;
        private bool _disposed;

        public bool IsPauseRequested => _pauseRequested;

        private bool IsPaused => _pauseRequested || _lockCount > 0;

        public PlayerSimulationGate(Character character)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
            _pauseApplied = character.IsPaused;
            _pauseRequested = _pauseApplied;
        }

        public void SetPaused(bool paused)
        {
            ThrowIfDisposed();
            _pauseRequested = paused;
            ApplyState();
        }

        public IDisposable AcquirePause()
        {
            ThrowIfDisposed();
            _lockCount++;
            ApplyState();
            return new PauseLease(this);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lockCount = 0;
            _pauseRequested = false;
        }

        private void ReleasePause()
        {
            if (_disposed || _lockCount == 0)
            {
                return;
            }

            _lockCount--;
            ApplyState();
        }

        private void ApplyState()
        {
            bool shouldPause = IsPaused;
            if (_pauseApplied == shouldPause)
            {
                return;
            }

            _pauseApplied = shouldPause;
            _character.Pause(shouldPause);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerSimulationGate));
            }
        }

        private sealed class PauseLease : IDisposable
        {
            private PlayerSimulationGate _owner;

            public PauseLease(PlayerSimulationGate owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                PlayerSimulationGate owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.ReleasePause();
            }
        }
    }
}
