using VContainer;

namespace _App
{
    internal static class PlayerCombatFeedbackComposition
    {
        public static void RegisterPlayerCombatFeedback(this IContainerBuilder builder)
        {
            builder.Register(
                resolver => new CombatImpactPresentation(
                    resolver.Resolve<IPlayerCombatActor>().Root,
                    resolver.Resolve<ICameraImpactFeedback>()),
                Lifetime.Scoped);
            builder.Register<CombatFeedbackController>(Lifetime.Scoped);
        }
    }
}
