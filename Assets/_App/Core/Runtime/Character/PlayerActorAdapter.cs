using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerActorAdapter :
        IPlayerActorTransform,
        IPlayerTargetingActor,
        IPlayerCombatActor,
        IDamageImmunityTarget,
        IPlayerTraversalActor
    {
        private readonly Character _character;
        private readonly Health _health;

        public Transform Root => _character.transform;
        public Vector3 Position => _character.transform.position;
        public Vector3 Forward => _character.transform.forward;
        public bool CanAttack => _health.IsAlive && _character.CanAttack();
        public bool IsFalling => _character.IsFalling();
        public bool IsClimbing => _character.IsClimbing();
        public ERotationMode RotationMode => _character.RotationMode;
        public bool IsDamageImmune => _health.IsDamageImmune;

        public PlayerActorAdapter(Character character, Health health)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
            _health = health != null
                ? health
                : throw new ArgumentNullException(nameof(health));
        }

        public void SetRotationMode(ERotationMode rotationMode)
        {
            _character.SetRotationMode(rotationMode);
        }

        public void SetDamageImmune(bool immune)
        {
            _health.SetDamageImmune(immune);
        }
    }
}
