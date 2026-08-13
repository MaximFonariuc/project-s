using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "AbilitiesData", menuName = "Abilities/Abilities Data")]
    public class AbilitiesData : ScriptableObject
    {
        [field: SerializeField] public List<Ability> Abilities { get; private set;}
    }

    public abstract class AbilityData : ScriptableObject
    {
        public abstract IAbility CreateAbilityInstance(Character character, AnimationController animationController);
    }
    
    [Serializable]
    public class Ability
    {
        public bool IsActive;
        public AbilityData Data;
    }
}