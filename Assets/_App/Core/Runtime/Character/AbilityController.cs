using System;
using System.Collections.Generic;

namespace _App
{
    public class AbilityController
    {
        private readonly Dictionary<Type, IAbility> _activeAbilities = new();
        private readonly Character _character;
        private readonly AnimationController _animationController;

        public AbilityController(AbilitiesData abilitiesData, Character character, AnimationController animationController)
        {
            _character = character;
            _animationController = animationController;
            InitializeAbilities(abilitiesData);
        }
        
        public void EnableAllAbilities()
        {
            foreach (var ability in _activeAbilities.Values)
            {
                ability.OnEnable();
            }
        }

        public void DisableAllAbilities()
        {
            foreach (var ability in _activeAbilities.Values)
            {
                ability.OnDisable();
            }
        }
        
        private void InitializeAbilities(AbilitiesData abilitiesData)
        {
            foreach (var abilityData in abilitiesData.Abilities)
            {
                if (abilityData is not { IsActive: true })
                {
                    continue;
                }

                IAbility ability = abilityData.Data.CreateAbilityInstance(_character, _animationController);
                if (ability != null)
                {
                    _activeAbilities[ability.GetType()] = ability;
                }
            }
        }

        public T GetAbility<T>() where T : class, IAbility
        {
            if (_activeAbilities.TryGetValue(typeof(T), out IAbility ability))
            {
                return ability as T;
            }
            return null;
        }
        
        public void EnableAbility<T>() where T : IAbility
        {
            if (_activeAbilities.TryGetValue(typeof(T), out IAbility ability))
            {
                ability.OnEnable();
            }
        }

        public void DisableAbility<T>() where T : IAbility
        {
            if (_activeAbilities.TryGetValue(typeof(T), out IAbility ability))
            {
                ability.OnDisable();
            }
        }

        public bool HasAbility<T>() where T : IAbility
        {
            return _activeAbilities.ContainsKey(typeof(T));
        }
    }
}