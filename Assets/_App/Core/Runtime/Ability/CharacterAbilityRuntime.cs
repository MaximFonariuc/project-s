using System;
using System.Collections.Generic;

namespace _App
{
    public interface ICharacterAbilityLifecycle
    {
        void Enable();
        void Disable();
    }

    internal sealed class CharacterAbilityRuntime : ICharacterAbilityLifecycle, IDisposable
    {
        private readonly Dictionary<Type, IAbility> _abilities = new();
        private bool _enabled;

        public CharacterAbilityRuntime(
            AbilitiesData abilitiesData,
            Character character,
            AnimationController animationController)
        {
            if (abilitiesData == null)
            {
                throw new ArgumentNullException(nameof(abilitiesData));
            }

            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (animationController == null)
            {
                throw new ArgumentNullException(nameof(animationController));
            }

            IReadOnlyList<Ability> definitions = abilitiesData.Abilities;
            if (definitions == null)
            {
                return;
            }

            foreach (Ability definition in definitions)
            {
                if (definition is not { IsActive: true })
                {
                    continue;
                }

                if (definition.Data == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(AbilitiesData)} contains an active ability without authoring data.");
                }

                IAbility ability = definition.Data.CreateAbilityInstance(character, animationController);
                if (ability != null)
                {
                    _abilities[ability.GetType()] = ability;
                }
            }
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            foreach (IAbility ability in _abilities.Values)
            {
                ability.OnEnable();
            }

            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            foreach (IAbility ability in _abilities.Values)
            {
                ability.OnDisable();
            }

            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        internal T Get<T>() where T : class, IAbility
        {
            return _abilities.TryGetValue(typeof(T), out IAbility ability)
                ? ability as T
                : null;
        }
    }
}
