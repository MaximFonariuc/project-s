using System;

namespace _App
{
    public sealed class PlayerActorController : IDisposable
    {
        private readonly IPlayerInput _input;
        private readonly ICharacterMovementPolicyLifecycle _movementPolicyLifecycle;
        private readonly PlayerLocomotionController _locomotion;
        private readonly PlayerTargetingController _targeting;
        private readonly PlayerCameraController _camera;
        private readonly PlayerCombatController _combat;
        private readonly PlayerTraversalController _traversal;
        private readonly PlayerSimulationPauseController _simulationPause;

        private bool _enabled;

        public PlayerActorController(
            IPlayerInput input,
            ICharacterMovementPolicyLifecycle movementPolicyLifecycle,
            PlayerLocomotionController locomotion,
            PlayerTargetingController targeting,
            PlayerCameraController camera,
            PlayerCombatController combat,
            PlayerTraversalController traversal,
            PlayerSimulationPauseController simulationPause)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _movementPolicyLifecycle = movementPolicyLifecycle ??
                                       throw new ArgumentNullException(nameof(movementPolicyLifecycle));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _traversal = traversal ?? throw new ArgumentNullException(nameof(traversal));
            _simulationPause = simulationPause ?? throw new ArgumentNullException(nameof(simulationPause));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _enabled = true;
            try
            {
                _movementPolicyLifecycle.Enable();
                _locomotion.Enable();
                _targeting.Enable();
                _camera.Enable();
                _combat.Enable();
                _traversal.Enable();
                _simulationPause.Enable();
                _input.Enable();
            }
            catch
            {
                Disable();
                throw;
            }
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.Disable();
            _simulationPause.Disable();
            _traversal.Disable();
            _combat.Disable();
            _camera.Disable();
            _targeting.Disable();
            _locomotion.Disable();
            _movementPolicyLifecycle.Disable();
            _enabled = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            _traversal.Tick();
            _locomotion.SetMovementIntent(_combat.MovementIntent);
            _locomotion.Tick(deltaTime);
            _targeting.Tick();
        }

        public void LateTick(float unscaledDeltaTime, float scaledDeltaTime)
        {
            if (_enabled)
            {
                _camera.LateTick(unscaledDeltaTime, scaledDeltaTime);
            }
        }

        public bool TryInvertGravity()
        {
            return _enabled && _traversal.TryInvertGravity();
        }

        public void Dispose()
        {
            Disable();
        }
    }
}
