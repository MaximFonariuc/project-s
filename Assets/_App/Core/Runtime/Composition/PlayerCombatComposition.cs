using System;
using ModularAnimation;
using ModularAnimation.Unity;
using VContainer;

namespace _App
{
    internal static class PlayerCombatComposition
    {
        public static void RegisterPlayerCombat(
            this IContainerBuilder builder,
            ThirdPersonController player)
        {
            if (player.CombatAnimations == null)
            {
                throw new InvalidOperationException("Player combat requires an AnimationsData reference.");
            }

            builder.Register<ICombatClock, UnityCombatClock>(Lifetime.Scoped);
            builder.Register<CombatInputBuffer>(Lifetime.Scoped);
            builder.Register<IPlayerCombatAnimation>(
                resolver => new PlayerCombatAnimationAdapter(
                    resolver.Resolve<ICharacterAnimationDriver>(),
                    resolver.Resolve<MecanimCharacterAnimationDriver>(),
                    player.CombatAnimations),
                Lifetime.Scoped);
            builder.Register<IPlayerWeaponRig>(
                _ => new PlayerWeaponRig(
                    player.WeaponHolder,
                    player.WeaponHand,
                    player.WeaponTransform,
                    player.WeaponTransform != null
                        ? player.WeaponTransform.GetComponent<Weapon>()
                        : null),
                Lifetime.Scoped);
            builder.Register(
                    resolver => new PlayerCombatController(
                        resolver.Resolve<IPlayerInput>(),
                        resolver.Resolve<IPlayerCombatActor>(),
                        resolver.Resolve<IPlayerLocomotionEvents>(),
                        resolver.Resolve<IPlayerCombatAnimation>(),
                        resolver.Resolve<IPlayerWeaponRig>(),
                        resolver.Resolve<CombatInputBuffer>(),
                        resolver.Resolve<ICombatClock>(),
                        player.CharacterType),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCombatCommands>()
                .As<IPlayerCombatState>();
        }
    }
}
