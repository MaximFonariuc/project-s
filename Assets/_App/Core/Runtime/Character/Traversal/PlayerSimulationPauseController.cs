using System;

namespace _App
{
    public sealed class PlayerSimulationPauseController : IDisposable
    {
        private readonly IPlayerInput _input;
        private readonly IPlayerSimulationPause _simulation;

        private bool _enabled;

        internal PlayerSimulationPauseController(
            IPlayerInput input,
            IPlayerSimulationPause simulation)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _input.PauseRequested += Toggle;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.PauseRequested -= Toggle;
            _enabled = false;
        }

        public void Toggle()
        {
            if (_enabled)
            {
                _simulation.SetPaused(!_simulation.IsPauseRequested);
            }
        }

        public void Dispose()
        {
            Disable();
        }
    }
}
