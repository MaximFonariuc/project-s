using System;
namespace _App
{
    public sealed class PlayerActorController : IDisposable
    {
        private readonly IPlayerInput _input;
        private readonly PlayerAnimationCoordinator _animation;
        private readonly ICharacterMovementPolicyLifecycle _movementPolicyLifecycle;
        private readonly PlayerLocomotionController _locomotion;
        private readonly PlayerTargetingController _targeting;
        private readonly PlayerCameraController _camera;
        private readonly PlayerCombatController _combat;
        private readonly PlayerAttackRecoilController _attackRecoil;
        private readonly PlayerAttackRecoilAnimationAdapter _attackRecoilAnimation;
        private readonly PlayerDefenseController _defense;
        private readonly PlayerDefenseImpactController _defenseImpact;
        private readonly PlayerDefenseAnimationAdapter _defenseAnimation;
        private readonly CharacterReactionController _reaction;
        private readonly GameplayTimeScaleController _gameplayTime;
        private readonly CombatHitStopController _hitStop;
        private readonly CombatFeedbackController _feedback;
        private readonly CombatStyleController _style;
        private readonly PlayerTraversalController _traversal;
        private readonly PlayerSimulationPauseController _simulationPause;

        private bool _enabled;

        internal PlayerActorController(
            IPlayerInput input,
            PlayerAnimationCoordinator animation,
            ICharacterMovementPolicyLifecycle movementPolicyLifecycle,
            PlayerLocomotionController locomotion,
            PlayerTargetingController targeting,
            PlayerCameraController camera,
            PlayerCombatController combat,
            PlayerAttackRecoilController attackRecoil,
            PlayerAttackRecoilAnimationAdapter attackRecoilAnimation,
            PlayerDefenseController defense,
            PlayerDefenseImpactController defenseImpact,
            PlayerDefenseAnimationAdapter defenseAnimation,
            CharacterReactionController reaction,
            GameplayTimeScaleController gameplayTime,
            CombatHitStopController hitStop,
            CombatFeedbackController feedback,
            CombatStyleController style,
            PlayerTraversalController traversal,
            PlayerSimulationPauseController simulationPause)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _movementPolicyLifecycle = movementPolicyLifecycle ??
                                       throw new ArgumentNullException(nameof(movementPolicyLifecycle));
            _locomotion = locomotion ?? throw new ArgumentNullException(nameof(locomotion));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _attackRecoil = attackRecoil ??
                throw new ArgumentNullException(nameof(attackRecoil));
            _attackRecoilAnimation = attackRecoilAnimation ??
                throw new ArgumentNullException(nameof(attackRecoilAnimation));
            _defense = defense ?? throw new ArgumentNullException(nameof(defense));
            _defenseImpact = defenseImpact ??
                throw new ArgumentNullException(nameof(defenseImpact));
            _defenseAnimation = defenseAnimation ??
                throw new ArgumentNullException(nameof(defenseAnimation));
            _reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
            _gameplayTime = gameplayTime ?? throw new ArgumentNullException(nameof(gameplayTime));
            _hitStop = hitStop ?? throw new ArgumentNullException(nameof(hitStop));
            _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
            _style = style ?? throw new ArgumentNullException(nameof(style));
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
                _reaction.StateChanged += OnReactionStateChanged;
                _attackRecoil.Started += OnAttackRecoilStarted;
                _gameplayTime.Enable();
                _movementPolicyLifecycle.Enable();
                _locomotion.Enable();
                _targeting.Enable();
                _camera.Enable();
                _attackRecoil.Enable();
                _attackRecoilAnimation.Enable();
                _combat.Enable();
                _defenseAnimation.Enable();
                _defense.Enable();
                _defenseImpact.Enable();
                _reaction.Enable();
                _hitStop.Enable();
                _feedback.Enable();
                _style.Enable();
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
            _reaction.StateChanged -= OnReactionStateChanged;
            _attackRecoil.Started -= OnAttackRecoilStarted;
            _simulationPause.Disable();
            _traversal.Disable();
            _style.Disable();
            _feedback.Disable();
            _hitStop.Disable();
            _reaction.Disable();
            _defenseImpact.Disable();
            _defense.Disable();
            _defenseAnimation.Disable();
            _combat.Disable();
            _attackRecoilAnimation.Disable();
            _attackRecoil.Disable();
            _camera.Disable();
            _targeting.Disable();
            _locomotion.Disable();
            _animation.Reset();
            _movementPolicyLifecycle.Disable();
            _gameplayTime.Disable();
            _enabled = false;
        }

        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            _gameplayTime.Tick(unscaledDeltaTime);
            _reaction.Tick(scaledDeltaTime);
            _attackRecoil.Tick(scaledDeltaTime);
            _combat.Advance(scaledDeltaTime);
            bool externalActionLock =
                _reaction.BlocksActions || _attackRecoil.IsActive;
            PlayerMovementIntent nonDefenseMovementIntent = externalActionLock
                ? PlayerMovementIntent.Locked
                : _combat.MovementIntent;
            PlayerLocomotionSnapshot locomotionSnapshot = _locomotion.Snapshot;
            _defense.Tick(
                scaledDeltaTime,
                !externalActionLock &&
                !locomotionSnapshot.IsJumpStarting &&
                !locomotionSnapshot.IsDodgeStarting &&
                !_locomotion.HasPriorityActionRequest(
                    in nonDefenseMovementIntent) &&
                (_combat.Phase == PlayerCombatPhase.Ready ||
                 _combat.MovementIntent.AllowGuard));
            _defenseAnimation.Tick(scaledDeltaTime);
            _traversal.Tick();
            _locomotion.SetMovementIntent(
                externalActionLock
                    ? PlayerMovementIntent.Locked
                    : _defense.BlocksActions
                        ? _defense.MovementIntent
                        : _combat.MovementIntent);
            _locomotion.Tick(scaledDeltaTime, unscaledDeltaTime);
            _combat.ResolveCommands();
            _style.Tick(scaledDeltaTime);
            _targeting.Tick(unscaledDeltaTime);
            _camera.TickPresentation(scaledDeltaTime);
        }

        public void LateTick(float unscaledDeltaTime)
        {
            if (_enabled)
            {
                _camera.LateTick(unscaledDeltaTime);
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

        private void OnReactionStateChanged(CharacterReactionStateId state)
        {
            if (state == CharacterReactionStateId.Stable)
            {
                _locomotion.SetMovementIntent(_combat.MovementIntent);
                return;
            }

            _attackRecoil.Interrupt();
            _combat.InterruptAttack(CombatActionEndReason.Reaction);
            _defense.Interrupt();
            if (state == CharacterReactionStateId.Dead)
            {
                _combat.DiscardPendingCommands();
            }

            _traversal.Interrupt();
            _locomotion.InterruptActions();
            _locomotion.SetMovementIntent(PlayerMovementIntent.Locked);
        }

        private void OnAttackRecoilStarted(PlayerAttackRecoilSnapshot snapshot)
        {
            _defense.Interrupt();
            _traversal.Interrupt();
            _locomotion.InterruptActions();
            _locomotion.SetMovementIntent(PlayerMovementIntent.Locked);
        }
    }
}
