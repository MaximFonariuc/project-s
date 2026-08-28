using ModularAnimation;

namespace _App
{
    public static class PlayerCombatPresentationResolver
    {
        internal static bool TryResolve(
            in PlayerCombatPresentation presentation,
            out AnimationActionId action,
            out int variant)
        {
            return TryResolve(
                presentation.Kind,
                presentation.ComboIndex,
                presentation.AnimationId,
                out action,
                out variant);
        }

        public static bool TryResolve(
            PlayerCombatPresentationKind kind,
            int comboIndex,
            PlayerCombatAnimationId animationId,
            out AnimationActionId action,
            out int variant)
        {
            switch (kind)
            {
                case PlayerCombatPresentationKind.ComboStep
                    when comboIndex >= 0 &&
                         comboIndex < PlayerAnimatorActions.LightAttackVariantCount:
                    action = PlayerAnimatorActions.LightAttack;
                    variant = comboIndex;
                    return true;

                case PlayerCombatPresentationKind.AirComboStep
                    when comboIndex >= 0 &&
                         comboIndex < PlayerAnimatorActions.AirAttackVariantCount:
                    action = PlayerAnimatorActions.AirAttack;
                    variant = comboIndex;
                    return true;

                case PlayerCombatPresentationKind.NamedAnimation:
                    variant = 0;
                    return TryResolveNamed(animationId, out action);

                default:
                    action = default;
                    variant = 0;
                    return false;
            }
        }

        private static bool TryResolveNamed(
            PlayerCombatAnimationId animationId,
            out AnimationActionId action)
        {
            if (animationId == PlayerCombatAnimationIds.HeavyAttack)
            {
                action = PlayerAnimatorActions.HeavyAttack;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.Special)
            {
                action = PlayerAnimatorActions.SpecialAttack;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.Launcher)
            {
                action = PlayerAnimatorActions.Launcher;
                return true;
            }

            if (animationId == PlayerCombatAnimationIds.AirChase)
            {
                action = PlayerAnimatorActions.AirChase;
                return true;
            }

            action = default;
            return false;
        }
    }
}
