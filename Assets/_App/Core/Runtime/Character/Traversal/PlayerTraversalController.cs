using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    public sealed class PlayerTraversalController : IPlayerTraversalCommands, IDisposable
    {
        private const float ClimbInputThreshold = 0.01f;

        private readonly IPlayerInput _input;
        private readonly IPlayerTraversalActor _actor;
        private readonly IPlayerSimulationLock _simulationLock;
        private readonly IPlayerInteractionProbe _interactionProbe;
        private readonly AbilityController _abilities;
        private readonly IPlayerCombatCommands _combatCommands;
        private readonly IPlayerCombatState _combatState;
        private readonly IPlayerLocomotionCommands _locomotion;
        private readonly IPlayerCameraCommands _camera;

        private int _interactionVersion;
        private IDisposable _simulationPauseLease;
        private bool _interactionPending;
        private bool _enabled;

        internal PlayerTraversalController(
            IPlayerInput input,
            IPlayerTraversalActor actor,
            IPlayerSimulationLock simulationLock,
            IPlayerInteractionProbe interactionProbe,
            AbilityController abilities,
            IPlayerCombatCommands combatCommands,
            IPlayerCombatState combatState,
            IPlayerLocomotionCommands locomotion,
            IPlayerCameraCommands camera)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _simulationLock = simulationLock ?? throw new ArgumentNullException(nameof(simulationLock));
            _interactionProbe = interactionProbe ?? throw new ArgumentNullException(nameof(interactionProbe));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _combatCommands = combatCommands ?? throw new ArgumentNullException(nameof(combatCommands));
            _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _input.InteractRequested += OnInteractRequested;
            _input.InverseGravityRequested += OnInverseGravityRequested;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _input.InteractRequested -= OnInteractRequested;
            _input.InverseGravityRequested -= OnInverseGravityRequested;
            _enabled = false;
            _interactionVersion++;
            _interactionPending = false;
            ReleaseSimulationPause();
        }

        public void Tick()
        {
            if (!_enabled || !_actor.IsClimbing || _input.Move.sqrMagnitude <= ClimbInputThreshold)
            {
                return;
            }

            _abilities.GetAbility<LadderClimbAbility>()?.OnClimbInput(_input.Move);
        }

        public bool TryInteract()
        {
            if (!_enabled || _interactionPending || !_interactionProbe.HasInteractable())
            {
                return false;
            }

            LadderClimbAbility ladder = _abilities.GetAbility<LadderClimbAbility>();
            if (ladder == null)
            {
                return false;
            }

            if (_combatState.CurrentWeapon == EWeaponType.Unarmed)
            {
                ladder.Climb().Forget();
                return true;
            }

            int operationVersion = ++_interactionVersion;
            _interactionPending = true;
            SheatheAndClimbAsync(ladder, operationVersion).Forget();
            return true;
        }

        public bool TryInvertGravity()
        {
            if (!_enabled || !_locomotion.TryInvertGravity())
            {
                return false;
            }

            _camera.ToggleGravityOrientation();
            return true;
        }

        public void Dispose()
        {
            Disable();
        }

        private async UniTaskVoid SheatheAndClimbAsync(
            LadderClimbAbility ladder,
            int operationVersion)
        {
            AcquireSimulationPause();
            try
            {
                bool sheathed = await _combatCommands.ToggleWeaponAsync(EWeaponType.Unarmed);
                if (!sheathed || !_enabled || operationVersion != _interactionVersion)
                {
                    return;
                }

                ladder.Climb().Forget();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (operationVersion == _interactionVersion)
                {
                    _interactionPending = false;
                    ReleaseSimulationPause();
                }
            }
        }

        private void AcquireSimulationPause()
        {
            _simulationPauseLease?.Dispose();
            _simulationPauseLease = _simulationLock.AcquirePause();
        }

        private void ReleaseSimulationPause()
        {
            _simulationPauseLease?.Dispose();
            _simulationPauseLease = null;
        }

        private void OnInteractRequested()
        {
            TryInteract();
        }

        private void OnInverseGravityRequested()
        {
            TryInvertGravity();
        }
    }
}
