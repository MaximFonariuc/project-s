using System;
using ModularStateMachine.VContainerIntegration;
using VContainer;

namespace _App
{
    internal static class PlayerCombatComposition
    {
        public static void RegisterPlayerCombat(
            this IContainerBuilder builder,
            ThirdPersonController player)
        {
            PlayerCombatActionProfile actionProfile = player.CombatActionProfile;
            CombatInputBufferSettings inputBufferSettings = actionProfile != null
                ? actionProfile.CreateInputBufferSettings()
                : CombatInputBufferSettings.CreatePrototypeDefaults();
            var weaponTransitionAction = new PlayerWeaponTransitionAction(
                actionProfile != null
                    ? actionProfile.CreateWeaponTransitionDefinition()
                    : CombatActionAuthoring.CreatePrototypeWeaponTransition().Build());

            builder.Register<ICombatClock, UnityCombatClock>(Lifetime.Scoped);
            builder.RegisterInstance(inputBufferSettings);
            builder.Register<CombatInputBuffer>(Lifetime.Scoped);
            builder.RegisterInstance(weaponTransitionAction);
            builder.Register<CombatMotionController>(Lifetime.Scoped);
            builder.Register<CombatActionExecutor>(Lifetime.Scoped);
            builder.Register<CombatHitResolver>(Lifetime.Scoped)
                .AsSelf()
                .As<ICombatHitResolver>()
                .As<ICombatHitEvents>();
            builder.Register<IPlayerCombatAnimation>(
                resolver => new PlayerCombatAnimationAdapter(
                    resolver.Resolve<PlayerAnimationCoordinator>(),
                    resolver.Resolve<PlayerAnimatorLoadout>()),
                Lifetime.Scoped);
            builder.Register(
                _ => actionProfile != null
                    ? actionProfile.CreateActionGraph()
                    : PlayerCombatActionGraph.CreatePrototype(
                        PlayerCombatActionGraph.PrototypeComboCapacity),
                Lifetime.Scoped);
            builder.Register<IPlayerWeaponRig>(
                resolver => new PlayerWeaponRig(
                    player.WeaponHolder,
                    player.WeaponHand,
                    player.WeaponTransform,
                    player.WeaponTransform != null
                        ? player.WeaponTransform.GetComponent<Weapon>()
                        : null,
                    player.UnarmedHitboxes,
                    resolver.Resolve<ICombatHitResolver>()),
                Lifetime.Scoped);

            builder.RegisterStateMachine<PlayerCombatPhase>(Lifetime.Scoped)
                .State<PlayerCombatReadyState>(PlayerCombatPhase.Ready, Lifetime.Scoped)
                .State<PlayerCombatExecutingState>(PlayerCombatPhase.Executing, Lifetime.Scoped)
                .State<PlayerCombatRecoveryState>(PlayerCombatPhase.Recovery, Lifetime.Scoped)
                .State<PlayerCombatWeaponSwitchState>(PlayerCombatPhase.WeaponSwitch, Lifetime.Scoped)
                .State<PlayerCombatInterruptedState>(PlayerCombatPhase.Interrupted, Lifetime.Scoped)
                .Transition(PlayerCombatPhase.Ready, PlayerCombatPhase.Executing)
                .Transition(PlayerCombatPhase.Ready, PlayerCombatPhase.WeaponSwitch)
                .Transition(PlayerCombatPhase.Executing, PlayerCombatPhase.Executing)
                .Transition(PlayerCombatPhase.Executing, PlayerCombatPhase.Recovery)
                .Transition(PlayerCombatPhase.Executing, PlayerCombatPhase.Interrupted)
                .Transition(PlayerCombatPhase.Executing, PlayerCombatPhase.Ready)
                .Transition(PlayerCombatPhase.Recovery, PlayerCombatPhase.Executing)
                .Transition(PlayerCombatPhase.Recovery, PlayerCombatPhase.Interrupted)
                .Transition(PlayerCombatPhase.Recovery, PlayerCombatPhase.Ready)
                .Transition(PlayerCombatPhase.Interrupted, PlayerCombatPhase.Ready)
                .Transition(PlayerCombatPhase.WeaponSwitch, PlayerCombatPhase.Ready);

            builder.Register(
                    resolver => new PlayerCombatController(
                        resolver.Resolve<IPlayerInput>(),
                        resolver.Resolve<IPlayerCombatActor>(),
                        resolver.Resolve<ICharacterReactionState>(),
                        resolver.Resolve<IPlayerLocomotionEvents>(),
                        resolver.Resolve<IPlayerCombatAnimation>(),
                        resolver.Resolve<IPlayerWeaponRig>(),
                        resolver.Resolve<ICombatHitEvents>(),
                        resolver.Resolve<CombatInputBuffer>(),
                        resolver.Resolve<CombatActionExecutor>(),
                        resolver.Resolve<PlayerCombatActionGraph>(),
                        resolver.Resolve<PlayerWeaponTransitionAction>(),
                        resolver.Resolve<ModularStateMachine.IStateMachine<PlayerCombatPhase>>(),
                        resolver.Resolve<ICombatClock>(),
                        player.CharacterType),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCombatCommands>()
                .As<IPlayerCombatState>();
        }
    }
}
