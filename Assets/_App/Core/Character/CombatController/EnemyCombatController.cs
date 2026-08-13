using Cysharp.Threading.Tasks;

namespace _App
{
    public class EnemyCombatController : CombatControllerBase
    {
        protected override ECharacterType CharacterType { get; }

        public EnemyCombatController(ECharacterType characterType, AnimationController animationController, AnimationsData skillsData) : base(animationController, skillsData)
        {
            CharacterType = characterType;
            SwitchAnimations(characterType, EWeaponType.OneHand).Forget();
            ForceInterrupt();
        }
        
        public override void TryAttack(ESkillType skillType = ESkillType.Default)
        {
            if (IsSkillAttack) return;
            base.TryAttack(skillType);
        }

        public void ForceInterrupt()
        {
            base.InterruptCombo();
            base.InterruptSkill();
        }
    }
}