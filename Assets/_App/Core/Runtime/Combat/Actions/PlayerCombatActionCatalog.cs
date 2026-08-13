using System;
using System.Collections.Generic;

namespace _App
{
    internal sealed class PlayerCombatActionCatalog
    {
        private readonly CombatActionDefinition _defaultCombo;
        private readonly Dictionary<int, CombatActionDefinition> _comboOverrides;
        private readonly Dictionary<ESkillType, CombatActionDefinition> _skills;
        private readonly CombatActionDefinition _weaponTransition;

        public PlayerCombatActionCatalog(
            CombatActionDefinition defaultCombo,
            Dictionary<int, CombatActionDefinition> comboOverrides,
            Dictionary<ESkillType, CombatActionDefinition> skills,
            CombatActionDefinition weaponTransition)
        {
            _defaultCombo = defaultCombo;
            _comboOverrides = comboOverrides ?? throw new ArgumentNullException(nameof(comboOverrides));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _weaponTransition = weaponTransition;
        }

        public CombatActionDefinition GetCombo(int comboIndex)
        {
            return _comboOverrides.TryGetValue(comboIndex, out CombatActionDefinition definition)
                ? definition
                : _defaultCombo;
        }

        public bool TryGetSkill(ESkillType skillType, out CombatActionDefinition definition)
        {
            return _skills.TryGetValue(skillType, out definition);
        }

        public CombatActionDefinition GetWeaponTransition()
        {
            return _weaponTransition;
        }

        public static PlayerCombatActionCatalog CreatePrototypeDefaults()
        {
            var skills = new Dictionary<ESkillType, CombatActionDefinition>
            {
                [ESkillType.Special] = CombatActionAuthoring.CreatePrototypeSkill().Build()
            };

            return new PlayerCombatActionCatalog(
                CombatActionAuthoring.CreatePrototypeCombo().Build(),
                new Dictionary<int, CombatActionDefinition>(),
                skills,
                CombatActionAuthoring.CreatePrototypeWeaponTransition().Build());
        }
    }
}
