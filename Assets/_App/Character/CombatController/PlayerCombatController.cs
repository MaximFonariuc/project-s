using Cysharp.Threading.Tasks;

namespace _App
{
    public class PlayerCombatController : CombatControllerBase
    {
        protected override ECharacterType CharacterType { get; }

        public PlayerCombatController(ECharacterType characterType, AnimationController animationController, AnimationsData skillsData) : base(animationController, skillsData)
        {
            CharacterType = characterType;
            SwitchAnimations(characterType).Forget();
            ForceInterrupt();
        }

        public override void TryAttack(ESkillType skillType = ESkillType.Default)
        {
            base.TryAttack(skillType);
        }

        public override void InterruptCombo()
        {
            base.InterruptCombo();
        }

        public override void InterruptSkill()
        {
            base.InterruptSkill();
        }
        
        public void ForceInterrupt()
        {
            base.InterruptCombo();
            base.InterruptSkill();
        }
    }
}