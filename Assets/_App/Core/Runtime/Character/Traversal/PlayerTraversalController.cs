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
        private readonly IPlayerTraversalActions _actions;
        private readonly IPlayerCombatCommands _combatCommands;
        private readonly IPlayerCombatState _combatState;
        private readonly ICharacterReactionState _reactionState;
        private readonly IPlayerAttackRecoilState _attackRecoilState;
        private readonly IPlayerDefenseState _defenseState;
        private readonly IPlayerLocomotionCommands _locomotion;
        private readonly IPlayerLocomotionState _locomotionState;
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
            IPlayerTraversalActions actions,
            IPlayerCombatCommands combatCommands,
            IPlayerCombatState combatState,
            ICharacterReactionState reactionState,
            IPlayerAttackRecoilState attackRecoilState,
            IPlayerDefenseState defenseState,
            IPlayerLocomotionCommands locomotion,
            IPlayerLocomotionState locomotionState,
            IPlayerCameraCommands camera)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _simulationLock = simulationLock ?? throw new ArgumentNullException(nameof(simulationLock));
            _interactionProbe = interactionProbe ?? throw new ArgumentNullException(nameof(interactionProbe));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _combatCommands = combatCommands ?? throw new ArgumentNullException(nameof(combatCommands));
            _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
            _reactionState = reactionState ?? throw new ArgumentNullException(nameof(reactionState));
            _attackRecoilState = attackRecoilState ??
                throw new ArgumentNullException(nameof(attackRecoilState));
            _defenseState = defenseState ??
                throw new ArgumentNullException(nameof(defenseState));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _locomotionState = locomotionState ??
                throw new ArgumentNullException(nameof(locomotionState));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _actions.Enable();
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
            _actions.Disable();
        }

        public void Tick()
        {
            if (!_enabled || !_actor.IsClimbing || _input.Move.sqrMagnitude <= ClimbInputThreshold)
            {
                return;
            }

            _actions.ApplyClimbInput(_input.Move);
        }

        public bool TryInteract()
        {
            if (!_enabled || _interactionPending || _reactionState.BlocksActions ||
                _attackRecoilState.IsActive || _defenseState.BlocksActions ||
                _locomotionState.BlocksActions ||
                _combatState.Phase != PlayerCombatPhase.Ready ||
                !_interactionProbe.HasInteractable())
            {
                return false;
            }

            if (!_actions.IsClimbAvailable)
            {
                return false;
            }

            if (_combatState.CurrentWeapon == EWeaponType.Unarmed)
            {
                return _actions.TryBeginClimb();
            }

            int operationVersion = ++_interactionVersion;
            _interactionPending = true;
            SheatheAndClimbAsync(operationVersion).Forget();
            return true;
        }

        public bool TryInvertGravity()
        {
            if (!_enabled || _attackRecoilState.IsActive ||
                _locomotionState.BlocksActions ||
                !_locomotion.TryInvertGravity())
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

        internal void Interrupt()
        {
            if (!_enabled)
            {
                return;
            }

            _interactionVersion++;
            _interactionPending = false;
            ReleaseSimulationPause();
            _actions.Interrupt();
        }

        private async UniTaskVoid SheatheAndClimbAsync(int operationVersion)
        {
            AcquireSimulationPause();
            try
            {
                bool sheathed = await _combatCommands.ToggleWeaponAsync(EWeaponType.Unarmed);
                if (!sheathed || !_enabled || operationVersion != _interactionVersion)
                {
                    return;
                }

                _actions.TryBeginClimb();
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
