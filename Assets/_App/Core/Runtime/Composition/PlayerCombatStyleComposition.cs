using VContainer;

namespace _App
{
    internal static class PlayerCombatStyleComposition
    {
        public static void RegisterPlayerCombatStyle(this IContainerBuilder builder)
        {
            builder.Register<CombatStyleController>(Lifetime.Scoped)
                .AsSelf()
                .As<ICombatStyleState>();
        }
    }
}
