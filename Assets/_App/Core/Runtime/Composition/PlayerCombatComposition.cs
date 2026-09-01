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
            PlayerCombatLoadoutProfile combatLoadout = player.CombatLoadoutProfile ??
                throw new InvalidOperationException(
                    $"Player '{player.name}' requires a combat loadout profile.");
            CombatInputBufferSettings inputBufferSettings =
                combatLoadout.CreateInputBufferSettings();
            PlayerAttackRecoilSettings attackRecoilSettings =
                combatLoadout.CreateAttackRecoilSettings();
            var weaponTransitionAction = new PlayerWeaponTransitionAction(
                combatLoadout.CreateWeaponTransitionDefinition());

            builder.Register<ICombatClock, UnityCombatClock>(Lifetime.Scoped);
            builder.RegisterInstance(inputBufferSettings);
            builder.RegisterInstance(attackRecoilSettings);
            builder.Register<CombatInputBuffer>(Lifetime.Scoped);
            builder.Register<CombatMotionController>(Lifetime.Scoped);
            builder.Register<CombatActionExecutor>(Lifetime.Scoped);
            builder.Register<CombatActionSession>(Lifetime.Scoped);
            builder.Register<CombatHitResolver>(Lifetime.Scoped)
                .AsSelf()
                .As<ICombatHitResolver>()
                .As<ICombatHitEvents>();
            builder.Register<IPlayerCombatAnimation>(
                resolver => new PlayerCombatAnimationAdapter(
                    resolver.Resolve<PlayerAnimationCoordinator>(),
                    resolver.Resolve<PlayerAnimatorLoadout>()),
                Lifetime.Scoped);
            builder.Register<PlayerAttackRecoilController>(Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerAttackRecoilState>();
            builder.Register<PlayerAttackRecoilAnimationAdapter>(Lifetime.Scoped);
            builder.Register(
                resolver => new PlayerCombatAvailability(
                    resolver.Resolve<IPlayerCombatActor>(),
                    resolver.Resolve<IPlayerLocomotionState>(),
                    resolver.Resolve<ICharacterReactionState>(),
                    resolver.Resolve<IPlayerDefenseState>(),
                    resolver.Resolve<IPlayerAttackRecoilState>(),
                    resolver.Resolve<IPlayerCombatAnimation>(),
                    resolver.Resolve<ICombatClock>(),
                    player.CharacterType),
                Lifetime.Scoped);
            builder.Register<IPlayerWeaponRig>(
                resolver => new PlayerWeaponRig(
                    player.WeaponLoadout,
                    resolver.Resolve<ICombatHitResolver>()),
                Lifetime.Scoped);
            builder.Register(
                resolver => new PlayerWeaponTransitionSession(
                    resolver.Resolve<IPlayerCombatAnimation>(),
                    resolver.Resolve<CombatActionExecutor>(),
                    weaponTransitionAction,
                    player.CharacterType),
                Lifetime.Scoped);
            builder.Register<PlayerCombatOperationLifetime>(Lifetime.Scoped);

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
                resolver => new PlayerCombatActionPipeline(
                    resolver.Resolve<IPlayerCombatActor>(),
                    resolver.Resolve<PlayerCombatAvailability>(),
                    resolver.Resolve<IPlayerCombatAnimation>(),
                    resolver.Resolve<CombatInputBuffer>(),
                    resolver.Resolve<CombatActionExecutor>(),
                    resolver.Resolve<CombatActionSession>(),
                    resolver.Resolve<PlayerCombatMovesetCatalog>(),
                    resolver.Resolve<ModularStateMachine.IStateMachine<PlayerCombatPhase>>(),
                    player.CharacterType),
                Lifetime.Scoped);

            builder.Register(
                    resolver => new PlayerCombatController(
                        resolver.Resolve<IPlayerInput>(),
                        resolver.Resolve<IPlayerCombatActor>(),
                        resolver.Resolve<PlayerCombatAvailability>(),
                        resolver.Resolve<IPlayerLocomotionEvents>(),
                        resolver.Resolve<IPlayerDefenseState>(),
                        resolver.Resolve<IPlayerAttackRecoilState>(),
                        resolver.Resolve<IPlayerCombatAnimation>(),
                        resolver.Resolve<IPlayerWeaponRig>(),
                        resolver.Resolve<PlayerCombatMovesetCatalog>(),
                        resolver.Resolve<ICombatHitEvents>(),
                        resolver.Resolve<CombatActionExecutor>(),
                        resolver.Resolve<PlayerCombatActionPipeline>(),
                        resolver.Resolve<PlayerWeaponTransitionSession>(),
                        resolver.Resolve<PlayerCombatOperationLifetime>(),
                        resolver.Resolve<ModularStateMachine.IStateMachine<PlayerCombatPhase>>(),
                        player.CharacterType,
                        player.StartingWeapon),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCombatCommands>()
                .As<IPlayerCombatState>();
        }
    }
}
